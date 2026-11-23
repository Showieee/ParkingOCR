using System.Reflection;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using ParkingOCR.Core.Models;

namespace ParkingOCR.Core.Services;

/// <summary>
/// Deep-learning license plate detector based on fine-tuned YOLOv11-nano ONNX model.
/// Runs offline, reliably locating European license plates under trees, shadows, angles, and reflections.
/// </summary>
public class OnnxPlateDetector : IPlateDetector, IDisposable
{
    private readonly InferenceSession _session;
    private readonly PlateDetector _fallbackDetector = new();
    private readonly bool _ownsSession;

    public OnnxPlateDetector(string? modelPath = null)
    {
        if (!string.IsNullOrEmpty(modelPath) && File.Exists(modelPath))
        {
            _session = new InferenceSession(modelPath);
            _ownsSession = true;
            return;
        }

        // Try AppContext directory candidate
        var baseDir = AppContext.BaseDirectory;
        var diskCandidate = Path.Combine(baseDir, "Resources", "yolo_plate.onnx");
        if (File.Exists(diskCandidate))
        {
            _session = new InferenceSession(diskCandidate);
            _ownsSession = true;
            return;
        }

        // Fallback to embedded resource
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("ParkingOCR.Core.Resources.yolo_plate.onnx")
            ?? throw new InvalidOperationException("Embedded resource 'ParkingOCR.Core.Resources.yolo_plate.onnx' not found.");

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        _session = new InferenceSession(ms.ToArray());
        _ownsSession = true;
    }

    public IList<DetectedPlateRegion> DetectPlates(Mat image, OcrOptions? options = null)
    {
        if (image.Empty()) return [];

        int origW = image.Cols;
        int origH = image.Rows;
        double aspect = (double)origW / origH;

        // 1. If image is already a tight crop of a license plate (e.g. 2.2 to 6.5 aspect ratio),
        // include it as a high-confidence candidate
        var results = new List<DetectedPlateRegion>();
        if (aspect >= 2.2 && aspect <= 6.5 && origW >= 80 && origH >= 20)
        {
            bool hasBlue = CheckBlueStrip(image, 0.20);
            bool isYellow = CheckYellowPlate(image);
            bool hasSwiss = CheckSwissShield(image);

            results.Add(new DetectedPlateRegion(
                image.Clone(),
                new PlateRectangle(0, 0, origW, origH),
                hasBlue,
                0.95,
                isYellow,
                hasSwiss
            ));
        }

        // 2. YOLOv11 Letterbox Preprocessing (640x640)
        const int inputDim = 640;
        double scale = Math.Min((double)inputDim / origW, (double)inputDim / origH);
        int nw = Math.Max(1, (int)(origW * scale));
        int nh = Math.Max(1, (int)(origH * scale));

        using Mat resized = new();
        Cv2.Resize(image, resized, new Size(nw, nh), 0, 0, InterpolationFlags.Linear);

        using Mat padded = new(inputDim, inputDim, MatType.CV_8UC3, Scalar.Black);
        Rect roi = new(0, 0, nw, nh);
        using (Mat padRoi = new(padded, roi))
        {
            resized.CopyTo(padRoi);
        }

        // Create RGB normalized tensor [1, 3, 640, 640]
        var inputTensor = new DenseTensor<float>([1, 3, inputDim, inputDim]);
        unsafe
        {
            for (int y = 0; y < inputDim; y++)
            {
                Vec3b* ptr = (Vec3b*)padded.Ptr(y);
                for (int x = 0; x < inputDim; x++)
                {
                    // YOLO expects RGB normalized to [0, 1]
                    inputTensor[0, 0, y, x] = ptr[x].Item2 / 255.0f; // R
                    inputTensor[0, 1, y, x] = ptr[x].Item1 / 255.0f; // G
                    inputTensor[0, 2, y, x] = ptr[x].Item0 / 255.0f; // B
                }
            }
        }

        // Run YOLOv11 inference
        var inputName = _session.InputMetadata.Keys.First();
        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, inputTensor) };

        using var outputs = _session.Run(inputs);
        var outputTensor = outputs.First().AsTensor<float>();

        // Output shape is [1, 5, 8400]
        var dims = outputTensor.Dimensions;
        int numPredictions = dims[2]; // 8400

        var candidateBoxes = new List<Rect>();
        var candidateScores = new List<float>();

        float minConfidence = options?.ConfidenceThreshold ?? 0.25f;

        for (int i = 0; i < numPredictions; i++)
        {
            float conf = outputTensor[0, 4, i];
            if (conf >= minConfidence)
            {
                float cx = outputTensor[0, 0, i];
                float cy = outputTensor[0, 1, i];
                float bw = outputTensor[0, 2, i];
                float bh = outputTensor[0, 3, i];

                // Convert from letterbox padded coords back to original image
                int x1 = Math.Max(0, (int)((cx - bw / 2.0f) / scale));
                int y1 = Math.Max(0, (int)((cy - bh / 2.0f) / scale));
                int x2 = Math.Min(origW, (int)((cx + bw / 2.0f) / scale));
                int y2 = Math.Min(origH, (int)((cy + bh / 2.0f) / scale));

                int w = x2 - x1;
                int h = y2 - y1;

                if (w >= 20 && h >= 8)
                {
                    candidateBoxes.Add(new Rect(x1, y1, w, h));
                    candidateScores.Add(conf);
                }
            }
        }

        // Apply NMS to deduplicate boxes
        if (candidateBoxes.Count > 0)
        {
            var keepIndices = Nms(candidateBoxes, candidateScores, 0.45f);
            foreach (int idx in keepIndices)
            {
                var r = candidateBoxes[idx];
                float score = candidateScores[idx];

                // Tight crop matching YOLO detection box
                int px = r.X;
                int py = r.Y;
                int pw = r.Width;
                int ph = r.Height;

                Rect cropRect = new(px, py, pw, ph);
                using Mat plateCrop = new(image, cropRect);

                bool hasBlue = CheckBlueStrip(plateCrop, 0.20);
                bool isYellow = CheckYellowPlate(plateCrop);
                bool hasSwiss = CheckSwissShield(plateCrop);

                results.Add(new DetectedPlateRegion(
                    plateCrop.Clone(),
                    new PlateRectangle(px, py, pw, ph),
                    hasBlue,
                    score,
                    isYellow,
                    hasSwiss
                ));
            }
        }

        // 3. Fallback to classical heuristic if YOLO found no candidates
        if (results.Count == 0)
        {
            var heuristicCandidates = _fallbackDetector.DetectPlates(image, options);
            foreach (var hc in heuristicCandidates)
            {
                results.Add(hc);
            }
        }

        // Sort descending by confidence score
        return results.OrderByDescending(r => r.ConfidenceScore).ToList();
    }

    public static bool CheckBlueStrip(Mat plate, double leftPortionRatio = 0.20)
    {
        if (plate.Channels() < 3 || plate.Cols < 20 || plate.Rows < 10)
            return false;

        int stripWidth = Math.Max(4, (int)(plate.Cols * leftPortionRatio));
        Rect stripRect = new(0, 0, stripWidth, plate.Rows);
        using Mat strip = new(plate, stripRect);

        int blueCount = 0;
        int total = strip.Cols * strip.Rows;
        unsafe
        {
            for (int y = 0; y < strip.Rows; y++)
            {
                Vec3b* ptr = (Vec3b*)strip.Ptr(y);
                for (int x = 0; x < strip.Cols; x++)
                {
                    byte b = ptr[x].Item0;
                    byte g = ptr[x].Item1;
                    byte r = ptr[x].Item2;
                    if (b > 35 && b > r + 14 && b > g + 9)
                    {
                        blueCount++;
                    }
                }
            }
        }

        return (double)blueCount / total >= 0.10;
    }

    public static bool CheckYellowPlate(Mat plate)
    {
        if (plate.Channels() < 3 || plate.Cols < 20 || plate.Rows < 10)
            return false;

        int yellowCount = 0;
        int total = plate.Cols * plate.Rows;
        unsafe
        {
            for (int y = 0; y < plate.Rows; y++)
            {
                Vec3b* ptr = (Vec3b*)plate.Ptr(y);
                for (int x = 0; x < plate.Cols; x++)
                {
                    byte b = ptr[x].Item0;
                    byte g = ptr[x].Item1;
                    byte r = ptr[x].Item2;
                    if (r > 130 && g > 130 && b < 100 && (r + g) > (2 * b + 40))
                    {
                        yellowCount++;
                    }
                }
            }
        }

        return (double)yellowCount / total >= 0.25;
    }

    public static bool CheckSwissShield(Mat plate)
    {
        if (plate.Channels() < 3 || plate.Cols < 30 || plate.Rows < 15)
            return false;

        if (CheckYellowPlate(plate))
            return false;

        int checkWidth = Math.Max(6, (int)(plate.Cols * 0.18));
        Rect leftRect = new(0, 0, checkWidth, plate.Rows);
        using Mat leftStrip = new(plate, leftRect);

        int redCount = 0;
        int total = leftStrip.Cols * leftStrip.Rows;
        unsafe
        {
            for (int y = 0; y < leftStrip.Rows; y++)
            {
                Vec3b* ptr = (Vec3b*)leftStrip.Ptr(y);
                for (int x = 0; x < leftStrip.Cols; x++)
                {
                    byte b = ptr[x].Item0;
                    byte g = ptr[x].Item1;
                    byte r = ptr[x].Item2;
                    if (r > 120 && r > g + 35 && r > b + 35)
                    {
                        redCount++;
                    }
                }
            }
        }

        return (double)redCount / total >= 0.08;
    }

    private static List<int> Nms(List<Rect> boxes, List<float> scores, float iouThreshold)
    {
        var order = Enumerable.Range(0, boxes.Count)
            .OrderByDescending(i => scores[i])
            .ToList();

        var keep = new List<int>();
        while (order.Count > 0)
        {
            int current = order[0];
            keep.Add(current);
            order.RemoveAt(0);

            for (int i = order.Count - 1; i >= 0; i--)
            {
                int next = order[i];
                float iou = ComputeIoU(boxes[current], boxes[next]);
                if (iou > iouThreshold)
                {
                    order.RemoveAt(i);
                }
            }
        }
        return keep;
    }

    private static float ComputeIoU(Rect a, Rect b)
    {
        int interX1 = Math.Max(a.X, b.X);
        int interY1 = Math.Max(a.Y, b.Y);
        int interX2 = Math.Min(a.Right, b.Right);
        int interY2 = Math.Min(a.Bottom, b.Bottom);

        int interW = Math.Max(0, interX2 - interX1);
        int interH = Math.Max(0, interY2 - interY1);
        int interArea = interW * interH;

        if (interArea == 0) return 0f;

        int unionArea = a.Width * a.Height + b.Width * b.Height - interArea;
        return unionArea > 0 ? (float)interArea / unionArea : 0f;
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
