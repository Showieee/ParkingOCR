using OpenCvSharp;
using ParkingOCR.Core.Models;

namespace ParkingOCR.Core.Services;

/// <summary>
/// Computer-vision based license plate detector supporting European plates with Euroband
/// as well as plates without Euroband (e.g. Swiss plates, UK yellow plates, older/cropped plates).
/// Uses multi-scale Sobel edge morphology, luminance thresholding, color verification, and geometric scoring.
/// </summary>
public class PlateDetector : IPlateDetector
{
    public IList<DetectedPlateRegion> DetectPlates(Mat image, OcrOptions? options = null)
    {
        options ??= new OcrOptions();
        var candidates = new List<DetectedPlateRegion>();

        if (image == null || image.Empty())
        {
            return candidates;
        }

        int origW = image.Cols;
        int origH = image.Rows;
        double wholeAspect = (double)origW / origH;

        // Check if the input image is already a close-up/cropped plate
        if (wholeAspect >= options.MinPlateAspectRatio && wholeAspect <= options.MaxPlateAspectRatio)
        {
            bool hasBlue = CheckBlueStrip(image, 0.20);
            bool isYellow = CheckYellowPlate(image);
            bool hasSwiss = CheckSwissShield(image);

            double score = hasBlue ? 0.98 : (isYellow || hasSwiss ? 0.95 : 0.92);

            candidates.Add(new DetectedPlateRegion(
                image.Clone(),
                new PlateRectangle(0, 0, origW, origH),
                hasBlue,
                score,
                isYellow,
                hasSwiss
            ));
        }

        // 1. Euroband Anchor Detection: Detect deep blue rectangular strip
        if (image.Channels() >= 3)
        {
            using Mat blueMask = new(origH, origW, MatType.CV_8UC1);
            unsafe
            {
                for (int y = 0; y < origH; y++)
                {
                    Vec3b* srcPtr = (Vec3b*)image.Ptr(y);
                    byte* dstPtr = (byte*)blueMask.Ptr(y);
                    for (int x = 0; x < origW; x++)
                    {
                        byte b = srcPtr[x].Item0;
                        byte g = srcPtr[x].Item1;
                        byte r = srcPtr[x].Item2;
                        if (b > 35 && b > r + 14 && b > g + 9)
                        {
                            dstPtr[x] = 255;
                        }
                        else
                        {
                            dstPtr[x] = 0;
                        }
                    }
                }
            }

            Cv2.FindContours(blueMask, out Point[][] blueContours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
            foreach (var bc in blueContours)
            {
                var br = Cv2.BoundingRect(bc);
                double barRatio = (double)br.Height / Math.Max(1, br.Width);
                int area = br.Width * br.Height;
                if (br.Height >= 20 && br.Height <= 120 && br.Width >= 12 && br.Width <= 60 && barRatio >= 1.2 && barRatio <= 3.2 && area >= 300)
                {
                    int px = Math.Max(0, br.X - 2);
                    int py = Math.Max(0, br.Y - (int)(br.Height * 0.12));
                    int pw = Math.Min(origW - px, (int)(br.Height * 5.2));
                    int ph = Math.Min(origH - py, (int)(br.Height * 1.25));

                    if (pw > 60 && ph > 14)
                    {
                        Rect cropRect = new(px, py, pw, ph);
                        using Mat plateCrop = new(image, cropRect);

                        using Mat cropGray = new();
                        Cv2.CvtColor(plateCrop, cropGray, ColorConversionCodes.BGR2GRAY);
                        double grayMean = Cv2.Mean(cropGray)[0];
                        if (grayMean >= 75)
                        {
                            double score = 0.85 + (grayMean / 255.0) * 0.10;
                            candidates.Add(new DetectedPlateRegion(
                                plateCrop.Clone(),
                                new PlateRectangle(px, py, pw, ph),
                                true,
                                score,
                                false,
                                false
                            ));
                        }
                    }
                }
            }
        }

        // Grayscale conversion
        using Mat gray = new();
        if (image.Channels() > 1)
        {
            Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
        }
        else
        {
            image.CopyTo(gray);
        }

        // 2. Multi-level Brightness Thresholding (handles shaded, normal, and bright plates: 90, 115, 140, 165, 190)
        int[] threshLevels = [90, 115, 140, 165, 190];
        foreach (int tVal in threshLevels)
        {
            using Mat bThresh = new();
            Cv2.Threshold(gray, bThresh, tVal, 255, ThresholdTypes.Binary);
            using Mat bKernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(21, 5));
            Cv2.MorphologyEx(bThresh, bThresh, MorphTypes.Close, bKernel);

            Cv2.FindContours(bThresh, out Point[][] bContours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
            AddContourCandidates(bContours, image, origW, origH, options, candidates);
        }

        // 3. Multi-scale Sobel Horizontal Gradient Closing (finds character edge clusters without requiring Euroband)
        using Mat blurred = new();
        Cv2.GaussianBlur(gray, blurred, new Size(5, 5), 0);
        using Mat gradX = new();
        using Mat gradXAbs = new();
        Cv2.Sobel(blurred, gradX, MatType.CV_16S, 1, 0, 3);
        Cv2.ConvertScaleAbs(gradX, gradXAbs);

        using Mat sThresh = new();
        Cv2.Threshold(gradXAbs, sThresh, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

        Size[] closeSizes = [new Size(25, 5), new Size(35, 7), new Size(45, 7)];
        foreach (var ksize in closeSizes)
        {
            using Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, ksize);
            using Mat morph = new();
            Cv2.MorphologyEx(sThresh, morph, MorphTypes.Close, kernel);

            using Mat cleanKernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
            Cv2.MorphologyEx(morph, morph, MorphTypes.Open, cleanKernel);

            Cv2.FindContours(morph, out Point[][] edgeContours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
            AddContourCandidates(edgeContours, image, origW, origH, options, candidates);
        }

        // Sort descending by score first
        candidates.Sort((a, b) => b.ConfidenceScore.CompareTo(a.ConfidenceScore));

        // Deduplicate overlapping candidate regions using Non-Maximum Suppression (NMS)
        var dedupCandidates = new List<DetectedPlateRegion>();
        foreach (var c in candidates)
        {
            bool duplicate = false;
            foreach (var d in dedupCandidates)
            {
                int ox = Math.Max(c.BoundingBox.X, d.BoundingBox.X);
                int oy = Math.Max(c.BoundingBox.Y, d.BoundingBox.Y);
                int ow = Math.Min(c.BoundingBox.Right, d.BoundingBox.Right) - ox;
                int oh = Math.Min(c.BoundingBox.Bottom, d.BoundingBox.Bottom) - oy;

                if (ow > 0 && oh > 0)
                {
                    int interArea = ow * oh;
                    if (interArea > 0.60 * Math.Min(c.BoundingBox.Area, d.BoundingBox.Area))
                    {
                        duplicate = true;
                        c.Dispose();
                        break;
                    }
                }
            }
            if (!duplicate)
            {
                dedupCandidates.Add(c);
            }
        }

        if (dedupCandidates.Count == 0)
        {
            bool hasBlue = CheckBlueStrip(image, 0.20);
            bool isYellow = CheckYellowPlate(image);
            bool hasSwiss = CheckSwissShield(image);
            dedupCandidates.Add(new DetectedPlateRegion(
                image.Clone(),
                new PlateRectangle(0, 0, origW, origH),
                hasBlue,
                0.75,
                isYellow,
                hasSwiss
            ));
        }

        return dedupCandidates;
    }

    private static void AddContourCandidates(
        Point[][] contours,
        Mat image,
        int origW,
        int origH,
        OcrOptions options,
        List<DetectedPlateRegion> candidates)
    {
        int minArea = options.MinPlateWidth * options.MinPlateHeight;
        int maxArea = (int)(origW * origH * 0.85);

        foreach (var contour in contours)
        {
            var rect = Cv2.BoundingRect(contour);
            if (rect.Width < options.MinPlateWidth || rect.Height < options.MinPlateHeight)
                continue;

            int area = rect.Width * rect.Height;
            if (area < minArea || area > maxArea)
                continue;

            double aspect = (double)rect.Width / rect.Height;
            if (aspect < options.MinPlateAspectRatio || aspect > options.MaxPlateAspectRatio)
                continue;

            int padX = (int)(rect.Width * 0.04);
            int padY = (int)(rect.Height * 0.08);

            int x = Math.Max(0, rect.X - padX);
            int y = Math.Max(0, rect.Y - padY);
            int w = Math.Min(origW - x, rect.Width + 2 * padX);
            int h = Math.Min(origH - y, rect.Height + 2 * padY);

            Rect cropRect = new(x, y, w, h);
            using Mat plateCrop = new(image, cropRect);

            bool hasBlue = CheckBlueStrip(plateCrop, 0.20);
            bool isYellow = CheckYellowPlate(plateCrop);
            bool hasSwiss = CheckSwissShield(plateCrop);

            // Objective scoring:
            // Aspect ratio score (ideal ~4.7)
            double aspectScore = 1.0 - Math.Min(1.0, Math.Abs(aspect - 4.7) / 4.7) * 0.5;

            // Score ranges from 0.68 to 0.86, allowing non-Euroband plates to score competitively (e.g. 0.80+)
            double score = 0.62 + (aspectScore * 0.16) + (hasBlue ? 0.08 : 0.0) + (isYellow || hasSwiss ? 0.06 : 0.0);

            candidates.Add(new DetectedPlateRegion(
                plateCrop.Clone(),
                new PlateRectangle(x, y, w, h),
                hasBlue,
                score,
                isYellow,
                hasSwiss
            ));
        }
    }

    private static bool CheckBlueStrip(Mat plate, double leftPortionRatio)
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

        return (double)blueCount / total >= 0.12;
    }

    private static bool CheckYellowPlate(Mat plate)
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

    private static bool CheckSwissShield(Mat plate)
    {
        if (plate.Channels() < 3 || plate.Cols < 30 || plate.Rows < 15)
            return false;

        // Swiss plates are strictly white background, never yellow
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
}
