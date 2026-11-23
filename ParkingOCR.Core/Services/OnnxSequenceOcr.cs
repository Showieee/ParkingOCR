using System.Reflection;
using System.Text;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using ParkingOCR.Core.Models;

namespace ParkingOCR.Core.Services;

/// <summary>
/// Deep-learning text sequence recognizer based on PaddleOCR Latin CRNN ONNX model.
/// Performs end-to-end sequence recognition with CTC decoding, eliminating character segmentation errors.
/// </summary>
public class OnnxSequenceOcr : IPlateSequenceOcr, IDisposable
{
    private readonly InferenceSession _session;
    private readonly List<string> _dict;
    private readonly bool _ownsSession;

    public OnnxSequenceOcr(string? modelPath = null, string? dictPath = null)
    {
        var baseDir = AppContext.BaseDirectory;
        var diskModel = !string.IsNullOrEmpty(modelPath) && File.Exists(modelPath)
            ? modelPath
            : Path.Combine(baseDir, "Resources", "rec.onnx");

        var diskDict = !string.IsNullOrEmpty(dictPath) && File.Exists(dictPath)
            ? dictPath
            : Path.Combine(baseDir, "Resources", "dict.txt");

        if (File.Exists(diskModel) && File.Exists(diskDict))
        {
            _session = new InferenceSession(diskModel);
            _dict = LoadDictFromLines(File.ReadAllLines(diskDict));
            _ownsSession = true;
            return;
        }

        // Fallback to embedded resources
        var assembly = Assembly.GetExecutingAssembly();

        using var dictStream = assembly.GetManifestResourceStream("ParkingOCR.Core.Resources.dict.txt")
            ?? throw new InvalidOperationException("Embedded resource 'ParkingOCR.Core.Resources.dict.txt' not found.");

        using var reader = new StreamReader(dictStream, Encoding.UTF8);
        var lines = new List<string>();
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            lines.Add(line);
        }
        _dict = LoadDictFromLines(lines);

        using var modelStream = assembly.GetManifestResourceStream("ParkingOCR.Core.Resources.rec.onnx")
            ?? throw new InvalidOperationException("Embedded resource 'ParkingOCR.Core.Resources.rec.onnx' not found.");

        using var ms = new MemoryStream();
        modelStream.CopyTo(ms);
        _session = new InferenceSession(ms.ToArray());
        _ownsSession = true;
    }

    private static List<string> LoadDictFromLines(IEnumerable<string> lines)
    {
        // CTC index 0 is blank
        var dict = new List<string> { "<blank>" };
        foreach (var l in lines)
        {
            var trimmed = l.TrimEnd('\r', '\n');
            dict.Add(trimmed);
        }
        // Last index is space
        dict.Add(" ");
        return dict;
    }

    public SequenceOcrResult Recognize(Mat plateCrop)
    {
        if (plateCrop.Empty() || plateCrop.Cols < 10 || plateCrop.Rows < 10)
        {
            return new SequenceOcrResult(string.Empty, 0f, []);
        }

        int origH = plateCrop.Rows;
        int origW = plateCrop.Cols;

        // Resize to fixed height 48, proportional width
        const int targetH = 48;
        int targetW = Math.Max(48, (int)(origW * ((double)targetH / origH)));

        using Mat resized = new();
        Cv2.Resize(plateCrop, resized, new Size(targetW, targetH), 0, 0, InterpolationFlags.Linear);

        // Normalize: (pixel / 255.0 - 0.5) / 0.5
        var inputTensor = new DenseTensor<float>([1, 3, targetH, targetW]);
        unsafe
        {
            for (int y = 0; y < targetH; y++)
            {
                Vec3b* ptr = (Vec3b*)resized.Ptr(y);
                for (int x = 0; x < targetW; x++)
                {
                    // Match BGR order normalized with mean 0.5, std 0.5
                    inputTensor[0, 0, y, x] = ((ptr[x].Item0 / 255.0f) - 0.5f) / 0.5f;
                    inputTensor[0, 1, y, x] = ((ptr[x].Item1 / 255.0f) - 0.5f) / 0.5f;
                    inputTensor[0, 2, y, x] = ((ptr[x].Item2 / 255.0f) - 0.5f) / 0.5f;
                }
            }
        }

        var inputName = _session.InputMetadata.Keys.First();
        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, inputTensor) };

        using var outputs = _session.Run(inputs);
        var outputTensor = outputs.First().AsTensor<float>();

        // Output shape: [1, seq_len, num_classes]
        var dims = outputTensor.Dimensions;
        int seqLen = dims[1];
        int numClasses = dims[2];

        var charBoxes = new List<CharacterBox>();
        var sb = new StringBuilder();
        float totalConf = 0f;
        int count = 0;

        int prevClass = 0;

        for (int t = 0; t < seqLen; t++)
        {
            int bestClass = 0;
            float maxScore = float.NegativeInfinity;

            for (int c = 0; c < numClasses; c++)
            {
                float score = outputTensor[0, t, c];
                if (score > maxScore)
                {
                    maxScore = score;
                    bestClass = c;
                }
            }

            // Softmax approximation / sigmoid confidence
            float conf = 1.0f / (1.0f + MathF.Exp(-Math.Clamp(maxScore, -10f, 10f)));

            // CTC collapse: skip blank (0) and consecutive duplicates
            if (bestClass != 0 && bestClass != prevClass)
            {
                if (bestClass < _dict.Count)
                {
                    string strChar = _dict[bestClass];
                    char ch = strChar.Length > 0 ? strChar[0] : ' ';
                    sb.Append(ch);

                    if (char.IsLetterOrDigit(ch))
                    {
                        totalConf += conf;
                        count++;

                        // Approximate bounding box of this character in plate coordinates
                        int charW = Math.Max(8, (int)((1.2 / seqLen) * origW));
                        int charX = Math.Max(0, (int)(((double)t / seqLen) * origW) - charW / 4);
                        int charY = Math.Max(0, (int)(origH * 0.08));
                        int charH = Math.Min(origH - charY, (int)(origH * 0.84));

                        charBoxes.Add(new CharacterBox(
                            ch,
                            conf,
                            new PlateRectangle(charX, charY, charW, charH),
                            false
                        ));
                    }
                }
            }

            prevClass = bestClass;
        }

        string rawText = sb.ToString();
        float avgConf = count > 0 ? totalConf / count : 0f;

        return new SequenceOcrResult(rawText, avgConf, charBoxes);
    }

    public void Dispose()
    {
        if (_ownsSession)
        {
            _session.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}
