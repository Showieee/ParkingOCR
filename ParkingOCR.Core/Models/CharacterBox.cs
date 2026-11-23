namespace ParkingOCR.Core.Models;

/// <summary>
/// Details about an individual recognized character on a license plate.
/// </summary>
public record CharacterBox(
    char Character,
    float Confidence,
    PlateRectangle Box,
    bool IsCountryCode = false
);
