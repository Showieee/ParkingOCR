using OpenCvSharp;
using ParkingOCR.Core.Models;

namespace ParkingOCR.Core.Services;

/// <summary>
/// Candidate plate region detected in an image.
/// </summary>
public record DetectedPlateRegion(
    Mat PlateCrop,
    PlateRectangle BoundingBox,
    bool HasEurobandStrip,
    double ConfidenceScore,
    bool IsYellowPlate = false,
    bool HasSwissShield = false
) : IDisposable
{
    public void Dispose()
    {
        PlateCrop?.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Service responsible for locating and extracting candidate license plate regions from an image.
/// </summary>
public interface IPlateDetector
{
    /// <summary>
    /// Detects candidate license plate regions in the input image, ordered by confidence.
    /// Handles both Euroband plates (blue strip) and non-Euroband plates (Swiss plates, yellow plates, older/cropped plates).
    /// </summary>
    /// <param name="image">Source image (BGR).</param>
    /// <param name="options">OCR options.</param>
    /// <returns>List of candidate plate regions.</returns>
    IList<DetectedPlateRegion> DetectPlates(Mat image, OcrOptions? options = null);
}
