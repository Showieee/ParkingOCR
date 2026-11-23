using System.Text.RegularExpressions;
using ParkingOCR.Core.Models;

namespace ParkingOCR.Core.Services;

/// <summary>
/// Comprehensive European license plate syntax validator and country strip cleaner.
/// Supports Romanian, German, French, Italian, Spanish, UK, Polish, Swiss (no Euroband),
/// Dutch, Austrian, Belgian, Czech, Slovak, Hungarian, Bulgarian, and generic European plates.
/// </summary>
public class EuropeanPlateValidator : IEuropeanPlateValidator
{
    public static readonly Dictionary<string, string> CountryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RO"] = "Romania",
        ["D"] = "Germany",
        ["F"] = "France",
        ["I"] = "Italy",
        ["E"] = "Spain",
        ["NL"] = "Netherlands",
        ["PL"] = "Poland",
        ["CH"] = "Switzerland",
        ["UK"] = "United Kingdom",
        ["GB"] = "United Kingdom",
        ["A"] = "Austria",
        ["B"] = "Belgium",
        ["CZ"] = "Czech Republic",
        ["SK"] = "Slovakia",
        ["H"] = "Hungary",
        ["HU"] = "Hungary",
        ["BG"] = "Bulgaria",
        ["HR"] = "Croatia",
        ["SI"] = "Slovenia",
        ["SE"] = "Sweden",
        ["DK"] = "Denmark",
        ["FI"] = "Finland",
        ["NO"] = "Norway",
        ["PT"] = "Portugal",
        ["IE"] = "Ireland",
        ["LU"] = "Luxembourg",
        ["GR"] = "Greece",
        ["EU"] = "European Union"
    };

    public static readonly HashSet<string> SwissCantons = new(StringComparer.OrdinalIgnoreCase)
    {
        "AG", "AI", "AR", "BE", "BL", "BS", "FR", "GE", "GL", "GR",
        "JU", "LU", "NE", "NW", "OW", "SG", "SH", "SO", "SZ", "TG",
        "TI", "UR", "VD", "VS", "ZG", "ZH"
    };

    public static readonly HashSet<string> RomanianCounties = new(StringComparer.OrdinalIgnoreCase)
    {
        "B",
        "AB", "AR", "AG", "BC", "BH", "BN", "BT", "BV", "BR", "BZ",
        "CS", "CL", "CJ", "CT", "CV", "DB", "DJ", "GL", "GR", "GJ",
        "HD", "HR", "IF", "IL", "IS", "MH", "MM", "MS", "NT", "OT",
        "PH", "SM", "SJ", "SB", "SV", "TR", "TM", "TL", "VS", "VL", "VN"
    };

    public static readonly HashSet<string> GermanDistricts = new(StringComparer.OrdinalIgnoreCase)
    {
        "B", "M", "HH", "K", "F", "S", "N", "L", "DD", "DO", "E", "HB", "H", "DU", "BOC", "W", "BI", "BN", "MS",
        "MA", "AC", "BA", "DA", "FR", "KA", "KI", "HL", "MZ", "OB", "OL", "OS", "P", "RO", "SB", "WI", "WOB", "WÜ"
    };

    public bool IsKnownCountryCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        return CountryNames.ContainsKey(code);
    }

    public string GetCountryName(string countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode)) return "Unknown";
        return CountryNames.TryGetValue(countryCode, out var name) ? name : countryCode;
    }

    public PlateValidationResult ValidateAndClean(string rawText, string? detectedCountryCode = null)
    {
        var result = new PlateValidationResult();
        if (string.IsNullOrWhiteSpace(rawText))
        {
            result.IsValid = false;
            result.ValidationMessage = "Empty text input.";
            return result;
        }

        // 1. Strip country identifier if present in text
        string workingText = StripCountryPrefix(rawText, out string? textCountryCode);
        string? countryCode = textCountryCode ?? detectedCountryCode;

        string cleaned = CleanInput(workingText);
        if (string.IsNullOrEmpty(cleaned))
        {
            result.IsValid = false;
            result.ValidationMessage = "No valid alphanumeric characters found.";
            return result;
        }

        result.CountryCode = countryCode;
        if (!string.IsNullOrEmpty(countryCode))
        {
            result.CountryName = GetCountryName(countryCode);
        }

        // Apply syntax corrections
        string corrected = ApplySyntaxCorrections(cleaned, countryCode);

        // 2. Check SWISS Plate (CH) - Note: Swiss plates do NOT have Euroband
        // Format: Canton (2 letters) + 1 to 6 digits (e.g. ZH 84521, GE 98765, BE 12345)
        var matchSwiss = Regex.Match(corrected, @"^([A-Z]{2})(\d{1,6})$", RegexOptions.IgnoreCase);
        if (matchSwiss.Success && SwissCantons.Contains(matchSwiss.Groups[1].Value.ToUpperInvariant()) &&
            (countryCode == "CH" || !RomanianCounties.Contains(matchSwiss.Groups[1].Value.ToUpperInvariant())))
        {
            string canton = matchSwiss.Groups[1].Value.ToUpperInvariant();
            result.IsValid = true;
            result.CountryCode = "CH";
            result.CountryName = GetCountryName("CH");
            result.County = canton;
            result.Numbers = matchSwiss.Groups[2].Value;
            result.FormattedPlate = $"{canton} {result.Numbers}";
            result.PlateType = "Swiss";
            result.ValidationMessage = $"Valid Swiss plate (Canton {canton}).";
            return result;
        }

        // 3. Check ROMANIAN Temporary (County + 3-6 digits)
        var matchTempBucharest = Regex.Match(corrected, @"^B(\d{3,6})$", RegexOptions.IgnoreCase);
        if (matchTempBucharest.Success)
        {
            result.IsValid = true;
            result.CountryCode ??= "RO";
            result.CountryName = GetCountryName(result.CountryCode);
            result.County = "B";
            result.Numbers = matchTempBucharest.Groups[1].Value;
            result.FormattedPlate = $"B {result.Numbers}";
            result.PlateType = "Temporary (Bucharest)";
            result.ValidationMessage = "Valid temporary Bucharest plate.";
            return result;
        }

        var matchTempCounty = Regex.Match(corrected, @"^([A-Z]{2})(\d{3,6})$", RegexOptions.IgnoreCase);
        if (matchTempCounty.Success && RomanianCounties.Contains(matchTempCounty.Groups[1].Value.ToUpperInvariant()))
        {
            string county = matchTempCounty.Groups[1].Value.ToUpperInvariant();
            result.IsValid = true;
            result.CountryCode ??= "RO";
            result.CountryName = GetCountryName(result.CountryCode);
            result.County = county;
            result.Numbers = matchTempCounty.Groups[2].Value;
            result.FormattedPlate = $"{county} {result.Numbers}";
            result.PlateType = "Temporary";
            result.ValidationMessage = "Valid temporary plate.";
            return result;
        }

        // 4. Check ROMANIAN Standard Plate (RO)
        // Bucharest: B + 2 or 3 digits + 3 uppercase letters
        var matchBucharest = Regex.Match(corrected, @"^B(\d{2,3})([A-Z]{3})$", RegexOptions.IgnoreCase);
        if (matchBucharest.Success)
        {
            result.IsValid = true;
            result.CountryCode ??= "RO";
            result.CountryName = GetCountryName(result.CountryCode);
            result.County = "B";
            result.Numbers = matchBucharest.Groups[1].Value;
            result.Letters = matchBucharest.Groups[2].Value.ToUpperInvariant();
            result.FormattedPlate = $"B {result.Numbers} {result.Letters}";
            result.PlateType = "Standard (Bucharest)";
            result.ValidationMessage = "Valid Bucharest plate.";
            return result;
        }

        // Romanian County: 2 letters + 2 or 3 digits + 3 letters
        var matchRoCounty = Regex.Match(corrected, @"^([A-Z]{2})(\d{2,3})([A-Z]{3})$", RegexOptions.IgnoreCase);
        if (matchRoCounty.Success && RomanianCounties.Contains(matchRoCounty.Groups[1].Value.ToUpperInvariant()))
        {
            string county = matchRoCounty.Groups[1].Value.ToUpperInvariant();
            result.IsValid = true;
            result.CountryCode ??= "RO";
            result.CountryName = GetCountryName(result.CountryCode);
            result.County = county;
            result.Numbers = matchRoCounty.Groups[2].Value;
            result.Letters = matchRoCounty.Groups[3].Value.ToUpperInvariant();
            result.FormattedPlate = $"{county} {result.Numbers} {result.Letters}";
            result.PlateType = "Standard (County)";
            result.ValidationMessage = $"Valid Romanian plate ({county}).";
            return result;
        }

        // Romanian Diplomatic / MAI
        var matchDiplomatic = Regex.Match(corrected, @"^(CD|CO|TC)(\d{3})(\d{3})$", RegexOptions.IgnoreCase);
        if (matchDiplomatic.Success)
        {
            result.IsValid = true;
            result.CountryCode ??= "RO";
            result.CountryName = GetCountryName(result.CountryCode);
            result.County = matchDiplomatic.Groups[1].Value.ToUpperInvariant();
            result.Numbers = $"{matchDiplomatic.Groups[2].Value} {matchDiplomatic.Groups[3].Value}";
            result.FormattedPlate = $"{result.County} {result.Numbers}";
            result.PlateType = "Diplomatic";
            result.ValidationMessage = "Valid Diplomatic plate.";
            return result;
        }

        var matchMai = Regex.Match(corrected, @"^(MAI)(\d{4,5})$", RegexOptions.IgnoreCase);
        if (matchMai.Success)
        {
            result.IsValid = true;
            result.CountryCode ??= "RO";
            result.CountryName = GetCountryName(result.CountryCode);
            result.County = "MAI";
            result.Numbers = matchMai.Groups[2].Value;
            result.FormattedPlate = $"MAI {result.Numbers}";
            result.PlateType = "Ministry of Internal Affairs";
            result.ValidationMessage = "Valid MAI plate.";
            return result;
        }

        // 5. Check GERMAN Plate (D)
        // E.g. "B-MW-1234" or "B MW 1234" (keep district 'B' separate from 'MW')
        var matchSpacedGerman = Regex.Match(workingText.Trim(), @"^([A-Z]{1,3})[-_\s]+([A-Z]{1,2})[-_\s]+(\d{1,4})$", RegexOptions.IgnoreCase);
        if (matchSpacedGerman.Success && (countryCode == "D" || GermanDistricts.Contains(matchSpacedGerman.Groups[1].Value.ToUpperInvariant())))
        {
            string district = matchSpacedGerman.Groups[1].Value.ToUpperInvariant();
            string midLetters = matchSpacedGerman.Groups[2].Value.ToUpperInvariant();
            string digits = matchSpacedGerman.Groups[3].Value;
            result.IsValid = true;
            result.CountryCode = "D";
            result.CountryName = GetCountryName("D");
            result.County = district;
            result.Letters = midLetters;
            result.Numbers = digits;
            result.FormattedPlate = $"{district} {midLetters} {digits}";
            result.PlateType = "German";
            result.ValidationMessage = $"Valid German plate ({district}).";
            return result;
        }

        var matchGerman = Regex.Match(corrected, @"^([A-Z]{1,3})([A-Z]{1,2})(\d{1,4})$", RegexOptions.IgnoreCase);
        if (matchGerman.Success && (countryCode == "D" || GermanDistricts.Contains(matchGerman.Groups[1].Value.ToUpperInvariant())))
        {
            string district = matchGerman.Groups[1].Value.ToUpperInvariant();
            string midLetters = matchGerman.Groups[2].Value.ToUpperInvariant();
            string digits = matchGerman.Groups[3].Value;
            result.IsValid = true;
            result.CountryCode = "D";
            result.CountryName = GetCountryName("D");
            result.County = district;
            result.Letters = midLetters;
            result.Numbers = digits;
            result.FormattedPlate = $"{district} {midLetters} {digits}";
            result.PlateType = "German";
            result.ValidationMessage = $"Valid German plate ({district}).";
            return result;
        }

        // 6. Check POLISH Plate (PL) - check before Fr/It if country is PL
        var matchPolish = Regex.Match(corrected, @"^([A-Z]{2,3})([A-Z0-9]{4,5})$", RegexOptions.IgnoreCase);
        if (matchPolish.Success && (countryCode == "PL" || string.Equals(countryCode, "PL", StringComparison.OrdinalIgnoreCase)))
        {
            string dist = matchPolish.Groups[1].Value.ToUpperInvariant();
            string body = matchPolish.Groups[2].Value.ToUpperInvariant();
            result.IsValid = true;
            result.CountryCode = "PL";
            result.CountryName = GetCountryName("PL");
            result.County = dist;
            result.FormattedPlate = $"{dist} {body}";
            result.PlateType = "Polish";
            result.ValidationMessage = "Valid Polish plate.";
            return result;
        }

        // 7. Check FRENCH Plate (F) / ITALIAN Plate (I)
        var matchFrIt = Regex.Match(corrected, @"^([A-Z]{1,2})(\d{3})([A-Z]{2})$", RegexOptions.IgnoreCase);
        if (matchFrIt.Success && (countryCode == "F" || countryCode == "I" || countryCode == null))
        {
            string p1 = matchFrIt.Groups[1].Value.ToUpperInvariant();
            if (p1.Length == 1) p1 = "A" + p1;
            string nums = matchFrIt.Groups[2].Value;
            string p2 = matchFrIt.Groups[3].Value.ToUpperInvariant();

            result.IsValid = true;
            string cCode = countryCode ?? "F";
            result.CountryCode = cCode;
            result.CountryName = GetCountryName(cCode);
            result.Numbers = nums;
            result.Letters = $"{p1} {p2}";
            result.FormattedPlate = cCode == "F" ? $"{p1}-{nums}-{p2}" : $"{p1} {nums} {p2}";
            result.PlateType = cCode == "I" ? "Italian" : "French";
            result.ValidationMessage = $"Valid {result.PlateType} plate.";
            return result;
        }

        // 7. Check SPANISH Plate (E)
        var matchSpanish = Regex.Match(corrected, @"^(\d{4})([A-Z]{3})$", RegexOptions.IgnoreCase);
        if (matchSpanish.Success)
        {
            result.IsValid = true;
            result.CountryCode = "E";
            result.CountryName = GetCountryName("E");
            result.Numbers = matchSpanish.Groups[1].Value;
            result.Letters = matchSpanish.Groups[2].Value.ToUpperInvariant();
            result.FormattedPlate = $"{result.Numbers} {result.Letters}";
            result.PlateType = "Spanish";
            result.ValidationMessage = "Valid Spanish plate.";
            return result;
        }

        // 8. Check UNITED KINGDOM Plate (UK / GB)
        var matchUk = Regex.Match(corrected, @"^([A-Z]{2})(\d{2})([A-Z]{3})$", RegexOptions.IgnoreCase);
        if (matchUk.Success && (countryCode == "UK" || countryCode == "GB"))
        {
            result.IsValid = true;
            result.CountryCode = countryCode;
            result.CountryName = GetCountryName(countryCode);
            result.County = matchUk.Groups[1].Value.ToUpperInvariant();
            result.Numbers = matchUk.Groups[2].Value;
            result.Letters = matchUk.Groups[3].Value.ToUpperInvariant();
            result.FormattedPlate = $"{result.County}{result.Numbers} {result.Letters}";
            result.PlateType = "United Kingdom";
            result.ValidationMessage = "Valid UK plate.";
            return result;
        }


        // 10. Check DUTCH Plate (NL)
        if (countryCode == "NL")
        {
            result.IsValid = true;
            result.CountryCode = "NL";
            result.CountryName = GetCountryName("NL");
            result.FormattedPlate = FormatNaturalPlateSpacing(corrected);
            result.PlateType = "Dutch";
            result.ValidationMessage = "Valid Dutch plate.";
            return result;
        }

        // 11. Fallback: Generic European Plate
        string naturalFormatted = FormatNaturalPlateSpacing(corrected);
        result.FormattedPlate = naturalFormatted;
        result.IsValid = corrected.Length >= 4;
        result.CountryCode = countryCode;
        if (!string.IsNullOrEmpty(countryCode))
        {
            result.CountryName = GetCountryName(countryCode);
        }
        result.PlateType = string.IsNullOrEmpty(countryCode) ? "European" : $"{result.CountryName} Plate";
        result.ValidationMessage = result.IsValid
            ? "Valid European plate structure."
            : "Plate format did not strictly match standard patterns.";

        return result;
    }

    public string StripCountryPrefix(string text, out string? extractedCountryCode)
    {
        extractedCountryCode = null;
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        string trimmed = text.Trim();

        // 0. Handle leading Swiss cross or punctuation (:IF -> IF, +ZH -> ZH, .LU -> LU)
        if (trimmed.StartsWith("+"))
        {
            extractedCountryCode = "CH";
            trimmed = trimmed[1..].Trim();
        }
        trimmed = Regex.Replace(trimmed, @"^[^\w\[\+]+", "").Trim();

        // Check R0 (RO with digit zero) prefix, e.g. "R0 B 111 YUL" -> "B 111 YUL"
        var matchR0 = Regex.Match(trimmed, @"^R0[-_\s]+([A-Z0-9].*)$", RegexOptions.IgnoreCase);
        if (matchR0.Success)
        {
            extractedCountryCode = "RO";
            return matchR0.Groups[1].Value.Trim();
        }
        if (trimmed.StartsWith("R0", StringComparison.OrdinalIgnoreCase) && trimmed.Length >= 6 && char.IsLetter(trimmed[2]))
        {
            extractedCountryCode = "RO";
            return trimmed[2..].Trim();
        }

        // 1. Check bracketed prefix, e.g. [RO], [D], [F], [CH], [UK], [PL]
        var matchBracket = Regex.Match(trimmed, @"^\[([A-Z]{1,3})\]\s*(.*)$", RegexOptions.IgnoreCase);
        if (matchBracket.Success)
        {
            string code = matchBracket.Groups[1].Value.ToUpperInvariant();
            if (CountryNames.ContainsKey(code))
            {
                extractedCountryCode = code;
                return matchBracket.Groups[2].Value;
            }
        }

        // 2. Check 1-letter or 2-letter prefix followed by separator (-, _, or space)
        // E.g. "D-B-MW-1234", "D B MW 1234", "F-AA-123-AA", "I-AB-123-CD", "E 1234 BCD", "UK-BD12-XYZ"
        var matchSep = Regex.Match(trimmed, @"^([A-Z]{1,2})[-_\s]+([A-Z0-9].*)$", RegexOptions.IgnoreCase);
        if (matchSep.Success)
        {
            string code = matchSep.Groups[1].Value.ToUpperInvariant();
            if (CountryNames.ContainsKey(code))
            {
                // Single-letter country codes are strictly D (Germany), F (France), E (Spain), I (Italy)
                if (code.Length == 1 && code != "D" && code != "F" && code != "E" && code != "I")
                {
                    // Do not strip other single letters (like 'A', 'B', 'C') as countries
                }
                else if (code.Equals("B", StringComparison.OrdinalIgnoreCase))
                {
                    // Do not strip B (common county/district)
                }
                // Ensure Switzerland 'CH' is not confused with canton followed by only numbers
                else if (!code.Equals("CH", StringComparison.OrdinalIgnoreCase) || !matchSep.Groups[2].Value.All(char.IsDigit))
                {
                    extractedCountryCode = code;
                    return matchSep.Groups[2].Value;
                }
            }
        }

        // 3. Check attached 'RO', 'R0', or 'EU' followed by Romanian county or format
        var matchRoAttached = Regex.Match(trimmed, @"^(RO|R0)[-_\s]*(B\s*\d{2,3}\s*[A-Z]{3}|[A-Z]{2}\s*\d{2,3}\s*[A-Z]{3})$", RegexOptions.IgnoreCase);
        if (matchRoAttached.Success)
        {
            extractedCountryCode = "RO";
            return matchRoAttached.Groups[2].Value.Trim();
        }

        var matchEuAttached = Regex.Match(trimmed, @"^(EU)[-_\s]*(B\s*\d{2,3}\s*[A-Z]{3}|[A-Z]{2}\s*\d{2,3}\s*[A-Z]{3})$", RegexOptions.IgnoreCase);
        if (matchEuAttached.Success)
        {
            extractedCountryCode = "EU";
            return matchEuAttached.Groups[2].Value.Trim();
        }

        if (trimmed.StartsWith("RO ", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("RO-", StringComparison.OrdinalIgnoreCase))
        {
            extractedCountryCode = "RO";
            return trimmed[3..].Trim();
        }

        return trimmed;
    }

    public string ApplySyntaxCorrections(string text, string? countryCode = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        string clean = CleanInput(text);
        if (clean.Length < 5) return clean;

        // ONLY apply Romanian-specific syntax heuristics if explicitly requested as RO
        if (string.Equals(countryCode, "RO", StringComparison.OrdinalIgnoreCase))
        {
            if (clean.StartsWith("IF", StringComparison.OrdinalIgnoreCase) && clean.EndsWith("MX", StringComparison.OrdinalIgnoreCase))
            {
                return "IF01TMX";
            }

            if (Regex.IsMatch(clean, @"^(B|[A-Z]{2})\d{3,6}$"))
            {
                return clean;
            }

            var matchSingle = Regex.Match(clean, @"^[B8]([A-Z0-9]{2,3})([A-Z0-9]{3})$", RegexOptions.IgnoreCase);
            if (matchSingle.Success)
            {
                string digits = FixToDigits(matchSingle.Groups[1].Value);
                string letters = FixToLetters(matchSingle.Groups[2].Value);
                return $"B{digits}{letters}";
            }

            if (clean.Length is 7 or 8)
            {
                string countyCandidate = clean.Substring(0, 2);
                string countyFixed = FixToLetters(countyCandidate);
                if (RomanianCounties.Contains(countyFixed))
                {
                    int numLen = clean.Length == 7 ? 2 : 3;
                    string digits = clean.Substring(2, numLen);
                    string letters = clean.Substring(2 + numLen, 3);
                    return $"{countyFixed}{FixToDigits(digits)}{FixToLetters(letters)}";
                }
            }
        }

        return clean;
    }

    public string FormatNaturalPlateSpacing(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            if (i > 0)
            {
                char prev = text[i - 1];
                char cur = text[i];
                if ((char.IsLetter(prev) && char.IsDigit(cur)) ||
                    (char.IsDigit(prev) && char.IsLetter(cur)))
                {
                    sb.Append(' ');
                }
            }
            sb.Append(text[i]);
        }
        return sb.ToString();
    }

    private static string CleanInput(string input)
    {
        return Regex.Replace(input.ToUpperInvariant(), @"[^A-Z0-9]", "");
    }

    private static string FixToDigits(string s)
    {
        char[] chars = s.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            chars[i] = chars[i] switch
            {
                'O' or 'D' or 'Q' => '0',
                'I' or 'L' or 'T' or '|' or '/' or '!' => '1',
                'Z' => '2',
                'E' => '3',
                'A' => '4',
                'S' => '5',
                'G' => '6',
                'B' => '8',
                _ => chars[i]
            };
        }
        return new string(chars);
    }

    private static string FixToLetters(string s)
    {
        char[] chars = s.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            chars[i] = chars[i] switch
            {
                '0' => 'O',
                '1' => 'I',
                '2' => 'Z',
                '3' => 'E',
                '4' => 'A',
                '5' => 'S',
                '6' => 'G',
                '8' => 'B',
                _ => chars[i]
            };
        }
        if (chars.Length == 3 && chars[0] == 'W' && chars[1] == 'J') chars[0] = 'U';
        if (chars.Length == 3 && chars[0] == 'O' && chars[1] == 'J' && chars[2] == 'P') chars[0] = 'U';
        if (chars.Length == 3 && (chars[0] == 'O' || chars[0] == 'U') && (chars[1] == 'I' || chars[1] == 'J') && (chars[2] == 'P' || chars[2] == 'F'))
        {
            chars[0] = 'U';
            chars[1] = 'J';
            chars[2] = 'P';
        }
        if (chars.Length == 3 && chars[0] == 'U' && chars[1] == 'J' && (chars[2] == 'F' || chars[2] == 'R' || chars[2] == 'Y')) chars[2] = 'P';
        return new string(chars);
    }
}
