namespace ParkingOCR.Core.Models;

/// <summary>
/// Complete outcome of a license plate recognition operation.
/// </summary>
public class LicensePlateResult
{
    /// <summary>
    /// True if a license plate candidate was detected and text parsed.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Final normalized plate text without country prefix (e.g., "B 123 ABC" or "CJ 12 ABC").
    /// </summary>
    public string PlateText { get; set; } = string.Empty;

    /// <summary>
    /// Raw concatenated OCR string before country strip removal or formatting.
    /// </summary>
    public string RawText { get; set; } = string.Empty;

    /// <summary>
    /// Detected country code, e.g. "RO" (or null if not present or not detected).
    /// </summary>
    public string? CountryCode { get; set; }

    /// <summary>
    /// Confidence score between 0.0 and 1.0.
    /// </summary>
    public float Confidence { get; set; }

    /// <summary>
    /// Bounding box of the plate in the original image coordinates.
    /// </summary>
    public PlateRectangle? BoundingBox { get; set; }

    /// <summary>
    /// Individual recognized characters and their relative positions.
    /// </summary>
    public List<CharacterBox> Characters { get; set; } = [];

    /// <summary>
    /// Romanian / European syntax validation details.
    /// </summary>
    public PlateValidationResult Validation { get; set; } = new();

    /// <summary>
    /// JPEG encoded cropped plate image (for UI preview).
    /// </summary>
    public byte[]? PlateCropJpeg { get; set; }

    /// <summary>
    /// JPEG encoded annotated source image with drawn bounding box and label.
    /// </summary>
    public byte[]? ProcessedImageJpeg { get; set; }

    /// <summary>
    /// Total processing time in milliseconds.
    /// </summary>
    public long ProcessingTimeMs { get; set; }

    /// <summary>
    /// Diagnostic / error message if recognition failed or had warnings.
    /// </summary>
    public string? Message { get; set; }
}
