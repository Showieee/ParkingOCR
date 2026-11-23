using ParkingOCR.Core.Models;

namespace ParkingOCR.Core.Services;

/// <summary>
/// Service responsible for validating, parsing, and normalizing Romanian license plate text.
/// Extends IEuropeanPlateValidator for full European compatibility.
/// </summary>
public interface IRomanianPlateValidator : IEuropeanPlateValidator
{
    /// <summary>
    /// Parses and cleans raw OCR text, separating the country prefix ('RO') and validating Romanian plate format.
    /// </summary>
    PlateValidationResult ValidateAndClean(string rawText);

    /// <summary>
    /// Checks whether the specified prefix or token is a Romanian county code.
    /// </summary>
    bool IsValidCounty(string countyCode);

    /// <summary>
    /// Attempts to apply OCR error heuristics based on Romanian plate syntax rules.
    /// </summary>
    string ApplySyntaxCorrections(string rawText);
}
