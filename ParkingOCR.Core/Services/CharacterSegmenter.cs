using OpenCvSharp;
using ParkingOCR.Core.Models;

namespace ParkingOCR.Core.Services;

/// <summary>
/// Segments character regions using CLAHE contrast normalization, dual-thresholding (Otsu + Adaptive),
/// hierarchical component analysis (CComp), and stroke merging.
/// </summary>
public class CharacterSegmenter : ICharacterSegmenter
{
    private const int TargetHeight = 80;

    public IList<SegmentedCharacter> SegmentCharacters(Mat plateCrop, bool hasEuroband = false, bool hasSwissShield = false)
    {
        var characters = new List<SegmentedCharacter>();
        if (plateCrop == null || plateCrop.Empty() || plateCrop.Cols < 20 || plateCrop.Rows < 10)
        {
            return characters;
        }

        // 1. Normalize size: scale to fixed height TargetHeight
        double scale = (double)TargetHeight / plateCrop.Rows;
        int targetWidth = (int)(plateCrop.Cols * scale);

        using Mat resized = new();
        Cv2.Resize(plateCrop, resized, new Size(targetWidth, TargetHeight), 0, 0, InterpolationFlags.Cubic);

        // 2. Grayscale & CLAHE contrast equalization
        using Mat gray = new();
        if (resized.Channels() > 1)
        {
            Cv2.CvtColor(resized, gray, ColorConversionCodes.BGR2GRAY);
        }
        else
        {
            resized.CopyTo(gray);
        }

        using var clahe = Cv2.CreateCLAHE(clipLimit: 2.5, tileGridSize: new Size(8, 8));
        using Mat clGray = new();
        clahe.Apply(gray, clGray);

        int emblemWidth = (hasEuroband || hasSwissShield) ? (int)(targetWidth * 0.13) : 0;

        // Try segmentation with Otsu first, then AdaptiveThreshold
        var boxes = SegmentWithThreshold(clGray, targetWidth, emblemWidth, hasSwissShield, useAdaptive: false);
        if (boxes.Count(b => !b.IsCountryCode) < 4)
        {
            var adaptiveBoxes = SegmentWithThreshold(clGray, targetWidth, emblemWidth, hasSwissShield, useAdaptive: true);
            if (adaptiveBoxes.Count(b => !b.IsCountryCode) > boxes.Count(b => !b.IsCountryCode))
            {
                boxes = adaptiveBoxes;
            }
        }

        // Stroke merging: merge split strokes (e.g. diagonal and vertical parts of '4')
        boxes = MergeSplitStrokes(boxes);

        // Map back to original plate crop coordinates and extract patches (with 1px safety margin)
        foreach (var (r, isCountry) in boxes)
        {
            int origX = Math.Max(0, (int)(r.X / scale));
            int origY = Math.Max(0, (int)(r.Y / scale));
            int origW = Math.Min(plateCrop.Cols - origX, (int)Math.Ceiling(r.Width / scale));
            int origH = Math.Min(plateCrop.Rows - origY, (int)Math.Ceiling(r.Height / scale));

            int pad = 1;
            int px = Math.Max(0, origX - pad);
            int py = Math.Max(0, origY - pad);
            int pw = Math.Min(plateCrop.Cols - px, origW + 2 * pad);
            int ph = Math.Min(plateCrop.Rows - py, origH + 2 * pad);

            if (pw <= 0 || ph <= 0) continue;

            Rect origRect = new(px, py, pw, ph);
            using Mat patch = new(plateCrop, origRect);

            characters.Add(new SegmentedCharacter(
                patch.Clone(),
                new PlateRectangle(origX, origY, origW, origH),
                isCountry
            ));
        }

        return characters;
    }

    private static List<(Rect Rect, bool IsCountryCode)> SegmentWithThreshold(
        Mat gray,
        int targetWidth,
        int eurobandWidth,
        bool hasSwissShield,
        bool useAdaptive)
    {
        using Mat bin = new();
        if (useAdaptive)
        {
            Cv2.AdaptiveThreshold(gray, bin, 255, AdaptiveThresholdTypes.GaussianC, ThresholdTypes.BinaryInv, 15, 7);
        }
        else
        {
            Cv2.Threshold(gray, bin, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);
        }

        // Clear outer borders (3px on all sides)
        Cv2.Rectangle(bin, new Rect(0, 0, targetWidth, 3), Scalar.Black, -1);
        Cv2.Rectangle(bin, new Rect(0, TargetHeight - 3, targetWidth, 3), Scalar.Black, -1);
        Cv2.Rectangle(bin, new Rect(0, 0, 3, TargetHeight), Scalar.Black, -1);
        Cv2.Rectangle(bin, new Rect(targetWidth - 3, 0, 3, TargetHeight), Scalar.Black, -1);

        // Use CComp to capture characters nested inside plate border frames
        Cv2.FindContours(bin, out Point[][] contours, out HierarchyIndex[] hierarchy, RetrievalModes.CComp, ContourApproximationModes.ApproxSimple);

        var candidateBoxes = new List<(Rect Rect, bool IsCountryCode)>();

        for (int i = 0; i < contours.Length; i++)
        {
            if (hierarchy != null && hierarchy.Length > i && hierarchy[i].Parent != -1)
                continue;

            var r = Cv2.BoundingRect(contours[i]);

            // Filter out border elements
            if (r.X <= 2 || r.Right >= targetWidth - 2)
                continue;

            // Character height filter (30% to 92% of plate height)
            if (r.Height < TargetHeight * 0.30 || r.Height > TargetHeight * 0.92)
                continue;

            // Character width filter
            if (r.Width < 4 || r.Width > targetWidth * 0.35)
                continue;

            // Aspect ratio (width / height) - allow up to 1.45 for wide letters like 'W' and 'M'
            double aspect = (double)r.Width / r.Height;
            if (aspect < 0.10 || aspect > 1.45)
                continue;

            // Vertical position: should be centered in the plate
            int centerY = r.Y + r.Height / 2;
            if (Math.Abs(centerY - TargetHeight / 2) > TargetHeight * 0.32)
                continue;

            // Swiss shield markings are excluded as emblems; for Euroband, only small symbols (< 38% height) are emblems,
            // while full-size registration letters (like 'C' in 'CJ 08 ERJ') are preserved
            bool isCountry = eurobandWidth > 0 && (r.X + r.Width * 0.5) <= eurobandWidth &&
                             (hasSwissShield || r.Height <= TargetHeight * 0.38);
            candidateBoxes.Add((r, isCountry));
        }

        // Sort left-to-right
        candidateBoxes.Sort((a, b) => a.Rect.X.CompareTo(b.Rect.X));

        // Deduplicate overlapping boxes
        var filteredBoxes = new List<(Rect Rect, bool IsCountryCode)>();
        foreach (var item in candidateBoxes)
        {
            bool overlap = false;
            for (int i = 0; i < filteredBoxes.Count; i++)
            {
                var existing = filteredBoxes[i].Rect;
                int overlapX = Math.Max(0, Math.Min(existing.Right, item.Rect.Right) - Math.Max(existing.X, item.Rect.X));
                if (overlapX > 0.55 * Math.Min(existing.Width, item.Rect.Width))
                {
                    if (item.Rect.Height > existing.Height)
                    {
                        filteredBoxes[i] = item;
                    }
                    overlap = true;
                    break;
                }
            }
            if (!overlap)
            {
                filteredBoxes.Add(item);
            }
        }

        return filteredBoxes;
    }

    private static List<(Rect Rect, bool IsCountryCode)> MergeSplitStrokes(List<(Rect Rect, bool IsCountryCode)> boxes)
    {
        if (boxes.Count <= 1) return boxes;

        var merged = new List<(Rect Rect, bool IsCountryCode)>();
        int i = 0;
        while (i < boxes.Count)
        {
            var cur = boxes[i];
            if (i + 1 < boxes.Count && !cur.IsCountryCode && !boxes[i + 1].IsCountryCode)
            {
                var next = boxes[i + 1];
                int gap = next.Rect.X - cur.Rect.Right;
                int combW = next.Rect.Right - cur.Rect.X;
                int maxH = Math.Max(cur.Rect.Height, next.Rect.Height);
                int minH = Math.Min(cur.Rect.Height, next.Rect.Height);
                double combAspect = (double)combW / maxH;

                bool hasFragment = minH < 0.60 * maxH && minH < TargetHeight * 0.45;
                if (hasFragment && gap >= -2 && gap <= 4 && combAspect >= 0.28 && combAspect <= 0.85)
                {
                    int minY = Math.Min(cur.Rect.Y, next.Rect.Y);
                    int maxBottom = Math.Max(cur.Rect.Bottom, next.Rect.Bottom);
                    Rect combRect = new(cur.Rect.X, minY, combW, maxBottom - minY);
                    merged.Add((combRect, false));
                    i += 2;
                    continue;
                }
            }

            merged.Add(cur);
            i++;
        }

        return merged;
    }
}
