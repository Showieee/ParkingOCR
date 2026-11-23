using ParkingOCR.Core.Models;

namespace ParkingOCR.Web.Models;

public class OcrRecognitionRequest
{
    public string? ImageBase64 { get; set; }
    public bool FilterCountryCode { get; set; } = true;
    public float ConfidenceThreshold { get; set; } = 0.35f;
    public bool DrawDebugAnnotations { get; set; } = true;
}

public class CharacterBoxDto
{
    public char Character { get; set; }
    public float Confidence { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool IsCountryCode { get; set; }
}

public class OcrRecognitionResponse
{
    public bool Success { get; set; }
    public string PlateText { get; set; } = string.Empty;
    public string RawText { get; set; } = string.Empty;
    public string? CountryCode { get; set; }
    public string? CountryName => Validation?.CountryName;
    public bool HasEuroband => !string.IsNullOrEmpty(CountryCode) && CountryCode != "CH";
    public float Confidence { get; set; }
    public string ConfidencePercentage => $"{Math.Round(Confidence * 100, 1)}%";
    public long ProcessingTimeMs { get; set; }
    public PlateRectangle? BoundingBox { get; set; }
    public List<CharacterBoxDto> Characters { get; set; } = [];
    public PlateValidationResult Validation { get; set; } = new();
    public string? PlateCropBase64 { get; set; }
    public string? ProcessedImageBase64 { get; set; }
    public string? Message { get; set; }
}

public class SampleImageDto
{
    public string Id { get; set; } = string.Empty;
    public string Filename { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ExpectedPlate { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string CountryName { get; set; } = string.Empty;
    public bool HasEuroband { get; set; }
    public string ThumbnailBase64 { get; set; } = string.Empty;
}
