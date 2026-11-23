namespace ParkingOCR.Core.Models;

/// <summary>
/// Result of syntax analysis and country strip filtering for a license plate across European countries.
/// </summary>
public class PlateValidationResult
{
    public bool IsValid { get; set; }
    public string FormattedPlate { get; set; } = string.Empty;
    public string? CountryCode { get; set; }
    public string? CountryName { get; set; }
    public string? County { get; set; }
    public string? Numbers { get; set; }
    public string? Letters { get; set; }
    public string PlateType { get; set; } = "Standard";
    public string? ValidationMessage { get; set; }
}
