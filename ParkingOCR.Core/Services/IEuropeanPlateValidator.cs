using ParkingOCR.Core.Models;

namespace ParkingOCR.Core.Services;

/// <summary>
/// Service responsible for validating, parsing, and normalizing European license plate text,
/// including filtering and extracting country identifiers (e.g. 'RO', 'D', 'F', 'I', 'E', 'NL', 'PL', 'CH', 'UK', etc.)
/// and handling plates without Eurobands (e.g. Swiss plates, older plates, cropped plates).
/// </summary>
public interface IEuropeanPlateValidator
{
    /// <summary>
    /// Parses and cleans raw OCR text across European formats, separating the country prefix
    /// and normalizing the plate string.
    /// </summary>
    /// <param name="rawText">Raw OCR string.</param>
    /// <param name="detectedCountryCode">Optional country code detected from Euroband or image features.</param>
    /// <returns>Validation result containing parsed country, region, numbers, letters, and normalized plate.</returns>
    PlateValidationResult ValidateAndClean(string rawText, string? detectedCountryCode = null);

    /// <summary>
    /// Strips leading European country identifier from plate text if present.
    /// </summary>
    string StripCountryPrefix(string text, out string? extractedCountryCode);

    /// <summary>
    /// Checks whether the specified prefix or token is a known European country code.
    /// </summary>
    bool IsKnownCountryCode(string code);

    /// <summary>
    /// Returns the full country name for a given country code (e.g. "RO" -> "Romania", "CH" -> "Switzerland").
    /// </summary>
    string GetCountryName(string countryCode);

    /// <summary>
    /// Attempts to apply OCR error heuristics based on European plate syntax rules.
    /// </summary>
    string ApplySyntaxCorrections(string rawText, string? countryCode = null);

    /// <summary>
    /// Formats plate text with natural European spacing separating letter and digit groups.
    /// </summary>
    string FormatNaturalPlateSpacing(string text);
}
