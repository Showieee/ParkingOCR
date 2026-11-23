using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using OpenCvSharp;
using ParkingOCR.Core.Models;

namespace ParkingOCR.Core.Services;

/// <summary>
/// Primary offline License Plate Recognition service integrating plate detection,
/// character segmentation, ONNX-based character classification, and European plate validation.
/// Supports Romanian, German, French, Italian, Spanish, UK, Polish, Swiss (no Euroband),
/// Dutch, and other European license plates.
/// </summary>
public class LicensePlateOcrService : ILicensePlateOcrService
{
    private readonly IPlateDetector _plateDetector;
    private readonly ICharacterSegmenter _characterSegmenter;
    private readonly ICharacterClassifier _characterClassifier;
    private readonly IEuropeanPlateValidator _europeanValidator;

    [ActivatorUtilitiesConstructor]
    public LicensePlateOcrService(
        IPlateDetector plateDetector,
        ICharacterSegmenter characterSegmenter,
        ICharacterClassifier characterClassifier,
        IEuropeanPlateValidator europeanValidator)
    {
        _plateDetector = plateDetector ?? throw new ArgumentNullException(nameof(plateDetector));
        _characterSegmenter = characterSegmenter ?? throw new ArgumentNullException(nameof(characterSegmenter));
        _characterClassifier = characterClassifier ?? throw new ArgumentNullException(nameof(characterClassifier));
        _europeanValidator = europeanValidator ?? throw new ArgumentNullException(nameof(europeanValidator));
    }

    public LicensePlateOcrService(
        IPlateDetector plateDetector,
        ICharacterSegmenter characterSegmenter,
        ICharacterClassifier characterClassifier,
        IRomanianPlateValidator romanianValidator)
        : this(plateDetector, characterSegmenter, characterClassifier, (IEuropeanPlateValidator)romanianValidator)
    {
    }

    public Task<LicensePlateResult> RecognizePlateAsync(byte[] imageBytes, OcrOptions? options = null, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => RecognizePlate(imageBytes, options), cancellationToken);
    }

    public async Task<LicensePlateResult> RecognizePlateAsync(Stream imageStream, OcrOptions? options = null, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        await imageStream.CopyToAsync(ms, cancellationToken);
        return RecognizePlate(ms.ToArray(), options);
    }

    public async Task<LicensePlateResult> RecognizePlateAsync(string imagePath, OcrOptions? options = null, CancellationToken cancellationToken = default)
    {
        byte[] bytes = await File.ReadAllBytesAsync(imagePath, cancellationToken);
        return RecognizePlate(bytes, options);
    }

    public LicensePlateResult RecognizePlate(byte[] imageBytes, OcrOptions? options = null)
    {
        if (imageBytes == null || imageBytes.Length == 0)
        {
            return new LicensePlateResult
            {
                Success = false,
                Message = "Image byte array is null or empty."
            };
        }

        using var mat = Cv2.ImDecode(imageBytes, ImreadModes.Color);
        if (mat == null || mat.Empty())
        {
            return new LicensePlateResult
            {
                Success = false,
                Message = "Failed to decode image from byte array. Ensure image format is valid (JPEG, PNG, etc.)."
            };
        }

        return RecognizePlate(mat, options);
    }

    public LicensePlateResult RecognizePlate(Mat image, OcrOptions? options = null)
    {
        options ??= new OcrOptions();
        var sw = Stopwatch.StartNew();

        var plateCandidates = _plateDetector.DetectPlates(image, options);
        if (plateCandidates.Count == 0)
        {
            sw.Stop();
            return new LicensePlateResult
            {
                Success = false,
                ProcessingTimeMs = sw.ElapsedMilliseconds,
                Message = "No license plate candidate regions detected in image."
            };
        }

        LicensePlateResult? bestResult = null;

        foreach (var candidate in plateCandidates)
        {
            var segmentedChars = _characterSegmenter.SegmentCharacters(candidate.PlateCrop, candidate.HasEurobandStrip, candidate.HasSwissShield);
            if (segmentedChars.Count == 0)
            {
                candidate.Dispose();
                continue;
            }

            var charBoxes = new List<CharacterBox>();
            var plateCharsSb = new StringBuilder();
            var countryCharsSb = new StringBuilder();
            float confidenceSum = 0f;
            int plateCharCount = 0;

            foreach (var sc in segmentedChars)
            {
                var (character, conf) = _characterClassifier.Classify(sc.Crop);

                if (sc.IsCountryCode)
                {
                    countryCharsSb.Append(character);
                }
                else
                {
                    plateCharsSb.Append(character);
                    confidenceSum += conf;
                    plateCharCount++;
                }

                charBoxes.Add(new CharacterBox(
                    character,
                    conf,
                    sc.Box,
                    sc.IsCountryCode
                ));

                sc.Dispose();
            }

            string rawText = plateCharsSb.ToString();
            float avgConf = plateCharCount > 0 ? confidenceSum / plateCharCount : 0f;

            // 1. Determine Country Code from Euroband or text prefix
            string? detectedCountry = ResolveCountryCode(candidate, countryCharsSb.ToString(), rawText);

            // 2. Strip Country Identifier if requested and present in text
            string cleanPlate = rawText;
            if (options.FilterCountryCode)
            {
                string stripped = _europeanValidator.StripCountryPrefix(cleanPlate, out string? textCountry);
                if (textCountry != null)
                {
                    detectedCountry ??= textCountry;
                    cleanPlate = stripped;

                    int prefixLen = rawText.Length - cleanPlate.Length;
                    for (int i = 0; i < Math.Min(prefixLen, charBoxes.Count); i++)
                    {
                        charBoxes[i] = charBoxes[i] with { IsCountryCode = true };
                    }
                }
                else if (detectedCountry != null && cleanPlate.StartsWith(detectedCountry, StringComparison.OrdinalIgnoreCase))
                {
                    cleanPlate = cleanPlate[detectedCountry.Length..].Trim();
                    for (int i = 0; i < Math.Min(detectedCountry.Length, charBoxes.Count); i++)
                    {
                        charBoxes[i] = charBoxes[i] with { IsCountryCode = true };
                    }
                }
            }

            // 3. Validate and clean with European plate validator
            var validation = _europeanValidator.ValidateAndClean(cleanPlate, detectedCountry);
            string finalPlateText = validation.FormattedPlate;
            if (string.IsNullOrEmpty(detectedCountry))
            {
                detectedCountry = validation.CountryCode;
            }

            // Encode Plate Crop as JPEG
            byte[] plateCropJpeg;
            using (var encMs = candidate.PlateCrop.ToMemoryStream(".jpg", new ImageEncodingParam(ImwriteFlags.JpegQuality, 90)))
            {
                plateCropJpeg = encMs.ToArray();
            }

            var currentResult = new LicensePlateResult
            {
                Success = true,
                PlateText = finalPlateText,
                RawText = rawText,
                CountryCode = detectedCountry,
                Confidence = avgConf,
                BoundingBox = candidate.BoundingBox,
                Characters = charBoxes,
                Validation = validation,
                PlateCropJpeg = plateCropJpeg,
                Message = validation.ValidationMessage
            };

            candidate.Dispose();

            // If we found a valid plate format, pick it immediately
            if (validation.IsValid)
            {
                bestResult = currentResult;
                break;
            }

            // Otherwise, keep the one with most characters and highest confidence
            if (bestResult == null ||
                (currentResult.Characters.Count > bestResult.Characters.Count) ||
                (currentResult.Characters.Count == bestResult.Characters.Count && currentResult.Confidence > bestResult.Confidence))
            {
                bestResult = currentResult;
            }
        }

        sw.Stop();

        if (bestResult == null)
        {
            return new LicensePlateResult
            {
                Success = false,
                ProcessingTimeMs = sw.ElapsedMilliseconds,
                Message = "Could not segment or recognize plate characters from candidates."
            };
        }

        bestResult.ProcessingTimeMs = sw.ElapsedMilliseconds;

        // Draw debug annotations if requested
        if (options.DrawDebugAnnotations && bestResult.BoundingBox != null)
        {
            bestResult.ProcessedImageJpeg = GenerateAnnotatedImage(image, bestResult);
        }

        return bestResult;
    }

    private string? ResolveCountryCode(DetectedPlateRegion candidate, string countryChars, string rawText)
    {
        // 1. Direct Euroband character identification
        if (!string.IsNullOrWhiteSpace(countryChars))
        {
            string upper = countryChars.ToUpperInvariant();
            string[] knownCodes = ["RO", "PL", "NL", "CH", "CZ", "SK", "BG", "GB", "UK", "HR", "EU", "HU", "D", "F", "I", "E", "A", "B", "S", "P"];
            foreach (var code in knownCodes)
            {
                if (upper.Contains(code)) return code;
            }
        }

        // 2. Image feature indicators
        if (candidate.HasSwissShield)
        {
            return "CH";
        }

        if (candidate.IsYellowPlate)
        {
            return "UK";
        }

        // 3. Text prefix check
        string upperRaw = rawText.ToUpperInvariant();
        string[] twoLetterCodes = ["RO", "PL", "NL", "CH", "CZ", "SK", "BG", "GB", "UK", "HR", "SI", "DK", "FI", "NO", "PT", "IE", "LU", "GR", "EU", "HU"];
        foreach (var code in twoLetterCodes)
        {
            if (upperRaw.StartsWith(code) && upperRaw.Length >= code.Length + 4)
            {
                return code;
            }
        }

        // 4. Country code from European plate formats if Euroband is present
        if (candidate.HasEurobandStrip)
        {
            // Romanian: B 123 ABC or CJ 12 ABC
            if (Regex.IsMatch(upperRaw, @"^B\d{2,3}[A-Z]{3}$") ||
                (upperRaw.Length is 7 or 8 && EuropeanPlateValidator.RomanianCounties.Contains(upperRaw[..2]) && char.IsDigit(upperRaw[2]) && char.IsLetter(upperRaw[^1])))
            {
                return "RO";
            }

            // French: AA 123 AA
            if (Regex.IsMatch(upperRaw, @"^[A-Z]{2}\d{3}[A-Z]{2}$"))
            {
                return "F";
            }

            // Spanish: 1234 BCD
            if (Regex.IsMatch(upperRaw, @"^\d{4}[A-Z]{3}$"))
            {
                return "E";
            }

            // German: District + Letters + Digits (e.g. B MW 1234, HH XY 999)
            if (Regex.IsMatch(upperRaw, @"^[A-Z]{1,3}[A-Z]{1,2}\d{1,4}$") &&
                EuropeanPlateValidator.GermanDistricts.Any(d => upperRaw.StartsWith(d)))
            {
                return "D";
            }

            // Polish: WA 12345
            if (Regex.IsMatch(upperRaw, @"^[A-Z]{2,3}[A-Z0-9]{4,5}$") &&
                (upperRaw.StartsWith("W") || upperRaw.StartsWith("K") || upperRaw.StartsWith("P")))
            {
                return "PL";
            }

            return "RO"; // Default Euroband fallback for Romanian parking
        }

        return null;
    }

    private static byte[] GenerateAnnotatedImage(Mat original, LicensePlateResult result)
    {
        using var annotated = original.Clone();
        var bb = result.BoundingBox!;
        var rect = new Rect(bb.X, bb.Y, bb.Width, bb.Height);

        // 1. Draw glowing rectangular border around plate
        Cv2.Rectangle(annotated, rect, new Scalar(0, 255, 128), 3);

        // 2. Draw badge above plate with recognized text
        string label = string.IsNullOrEmpty(result.CountryCode)
            ? $"{result.PlateText} ({(result.Confidence * 100):0.0}%)"
            : $"[{result.CountryCode}] {result.PlateText} ({(result.Confidence * 100):0.0}%)";

        int baseline = 0;
        var textSize = Cv2.GetTextSize(label, HersheyFonts.HersheySimplex, 0.75, 2, out baseline);

        int badgeY = Math.Max(0, rect.Y - textSize.Height - 12);
        int badgeX = Math.Max(0, rect.X);
        int badgeW = textSize.Width + 16;
        int badgeH = textSize.Height + 10;

        if (badgeX + badgeW > annotated.Cols)
            badgeX = Math.Max(0, annotated.Cols - badgeW);

        // Dark background badge
        Cv2.Rectangle(annotated, new Rect(badgeX, badgeY, badgeW, badgeH), new Scalar(20, 20, 20), -1);
        Cv2.Rectangle(annotated, new Rect(badgeX, badgeY, badgeW, badgeH), new Scalar(0, 255, 128), 1);

        // Text
        Cv2.PutText(annotated, label, new Point(badgeX + 8, badgeY + textSize.Height + 4),
            HersheyFonts.HersheySimplex, 0.75, new Scalar(255, 255, 255), 2, LineTypes.AntiAlias);

        using var encMs = annotated.ToMemoryStream(".jpg", new ImageEncodingParam(ImwriteFlags.JpegQuality, 88));
        return encMs.ToArray();
    }
}
