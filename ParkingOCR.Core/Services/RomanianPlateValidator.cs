using System.Text.RegularExpressions;
using ParkingOCR.Core.Models;

namespace ParkingOCR.Core.Services;

/// <summary>
/// Romanian license plate syntax validator and country strip cleaner.
/// Extends EuropeanPlateValidator to provide Romanian-specific county validation
/// while supporting full European validation.
/// </summary>
public partial class RomanianPlateValidator : EuropeanPlateValidator, IRomanianPlateValidator
{
    public static readonly HashSet<string> ValidCounties = RomanianCounties;

    public static readonly HashSet<string> SpecialPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "CD", "CO", "TC", "MAI", "A"
    };

    public bool IsValidCounty(string countyCode)
    {
        if (string.IsNullOrWhiteSpace(countyCode)) return false;
        return ValidCounties.Contains(countyCode);
    }

    public PlateValidationResult ValidateAndClean(string rawText)
    {
        return base.ValidateAndClean(rawText, null);
    }

    public string ApplySyntaxCorrections(string text)
    {
        return base.ApplySyntaxCorrections(text, "RO");
    }
}
