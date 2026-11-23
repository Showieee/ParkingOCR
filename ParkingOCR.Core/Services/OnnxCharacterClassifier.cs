using System.Reflection;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace ParkingOCR.Core.Services;

/// <summary>
/// ONNX Runtime-powered character classifier using an embedded lightweight CNN model.
/// </summary>
public class OnnxCharacterClassifier : ICharacterClassifier
{
    private static readonly string ClassLabels = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private readonly InferenceSession _session;
    private readonly string _inputName;
    private bool _disposed;

    public OnnxCharacterClassifier(string? modelPath = null)
    {
        SessionOptions sessionOptions = new()
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL
        };

        if (!string.IsNullOrEmpty(modelPath) && File.Exists(modelPath))
        {
            _session = new InferenceSession(modelPath, sessionOptions);
        }
        else
        {
            // Load embedded resource
            var assembly = typeof(OnnxCharacterClassifier).Assembly;
            var resourceName = "ParkingOCR.Core.Resources.char_classifier.onnx";
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Could not load embedded ONNX model resource '{resourceName}'.");

            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            _session = new InferenceSession(ms.ToArray(), sessionOptions);
        }

        _inputName = _session.InputMetadata.Keys.First();
    }

    public (char Character, float Confidence) Classify(Mat charMat)
    {
        var probs = GetClassProbabilities(charMat);
        int maxIdx = 0;
        float maxProb = probs[0];
        for (int i = 1; i < probs.Length; i++)
        {
            if (probs[i] > maxProb)
            {
                maxProb = probs[i];
                maxIdx = i;
            }
        }

        return (ClassLabels[maxIdx], maxProb);
    }

    public IList<(char Character, float Confidence)> ClassifyBatch(IList<Mat> charMats)
    {
        var list = new List<(char Character, float Confidence)>(charMats.Count);
        foreach (var mat in charMats)
        {
            list.Add(Classify(mat));
        }
        return list;
    }

    public float[] GetClassProbabilities(Mat charMat)
    {
        using var prepared = PrepareImage(charMat);
        var inputTensor = MatToTensor(prepared);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(_inputName, inputTensor)
        };

        using var results = _session.Run(inputs);
        var outputTensor = results.First().AsTensor<float>();
        var logits = outputTensor.ToArray();

        return Softmax(logits);
    }

    /// <summary>
    /// Prepares a character patch: normalizes to 32x32, centers content, ensures white stroke on black background.
    /// </summary>
    private static Mat PrepareImage(Mat src)
    {
        Mat gray = new();
        if (src.Channels() > 1)
        {
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        }
        else
        {
            src.CopyTo(gray);
        }

        // Check background intensity at the 4 corners (corners are pure background, never char stroke)
        int h = gray.Rows;
        int w = gray.Cols;
        int c00 = gray.At<byte>(0, 0);
        int c01 = gray.At<byte>(0, w - 1);
        int c10 = gray.At<byte>(h - 1, 0);
        int c11 = gray.At<byte>(h - 1, w - 1);
        int cornerMean = (c00 + c01 + c10 + c11) / 4;

        Mat bin = new();
        if (cornerMean > 120)
        {
            // Dark character on light background -> invert so character is white on black
            Cv2.Threshold(gray, bin, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);
        }
        else
        {
            // Already white character on dark background
            Cv2.Threshold(gray, bin, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
        }
        gray.Dispose();

        // Find bounding box of foreground (white stroke pixels)
        using Mat nonZero = new();
        Cv2.FindNonZero(bin, nonZero);
        Rect contentRect;
        if (nonZero.Total() > 5)
        {
            contentRect = Cv2.BoundingRect(nonZero);
        }
        else
        {
            contentRect = new Rect(0, 0, bin.Cols, bin.Rows);
        }

        using var cropped = new Mat(bin, contentRect);

        // Aspect-ratio-preserving resize to fit in 26x26 inside a 32x32 canvas
        int targetDim = 26;
        double scale = Math.Min((double)targetDim / cropped.Cols, (double)targetDim / cropped.Rows);
        int nw = Math.Max(1, (int)(cropped.Cols * scale));
        int nh = Math.Max(1, (int)(cropped.Rows * scale));

        using var resized = new Mat();
        Cv2.Resize(cropped, resized, new Size(nw, nh), 0, 0, InterpolationFlags.Area);

        Mat canvas = Mat.Zeros(new Size(32, 32), MatType.CV_8UC1);
        int ox = (32 - nw) / 2;
        int oy = (32 - nh) / 2;

        using var roi = new Mat(canvas, new Rect(ox, oy, nw, nh));
        resized.CopyTo(roi);

        bin.Dispose();
        return canvas;
    }

    private static DenseTensor<float> MatToTensor(Mat mat)
    {
        var tensor = new DenseTensor<float>([1, 1, 32, 32]);
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                byte val = mat.At<byte>(y, x);
                tensor[0, 0, y, x] = val / 255.0f;
            }
        }
        return tensor;
    }

    private static float[] Softmax(float[] logits)
    {
        float max = logits.Max();
        float sum = 0f;
        float[] exp = new float[logits.Length];
        for (int i = 0; i < logits.Length; i++)
        {
            exp[i] = MathF.Exp(logits[i] - max);
            sum += exp[i];
        }
        for (int i = 0; i < logits.Length; i++)
        {
            exp[i] /= sum;
        }
        return exp;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _session.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
