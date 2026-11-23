namespace ParkingOCR.Core.Models;

/// <summary>
/// Options for license plate OCR processing.
/// </summary>
public class OcrOptions
{
    /// <summary>
    /// When true, extracts and separates country identifier strips (such as EU/RO).
    /// </summary>
    public bool FilterCountryCode { get; set; } = true;

    /// <summary>
    /// Draw visual bounding boxes and recognized text overlay on ProcessedImageJpeg.
    /// </summary>
    public bool DrawDebugAnnotations { get; set; } = true;

    /// <summary>
    /// Minimum confidence threshold for character candidates (0.0 to 1.0).
    /// </summary>
    public float ConfidenceThreshold { get; set; } = 0.35f;

    /// <summary>
    /// Minimum aspect ratio (width / height) for candidate plate regions.
    /// Standard EU plates are roughly 520x110mm (~4.7:1), but partial crops or angled plates range ~1.8 to 6.5.
    /// </summary>
    public double MinPlateAspectRatio { get; set; } = 1.8;

    /// <summary>
    /// Maximum aspect ratio for candidate plate regions.
    /// </summary>
    public double MaxPlateAspectRatio { get; set; } = 7.0;

    /// <summary>
    /// Minimum width of plate in pixels.
    /// </summary>
    public int MinPlateWidth { get; set; } = 60;

    /// <summary>
    /// Minimum height of plate in pixels.
    /// </summary>
    public int MinPlateHeight { get; set; } = 16;
}
