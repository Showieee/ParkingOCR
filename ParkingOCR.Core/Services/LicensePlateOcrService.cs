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
    private readonly IPlateSequenceOcr? _sequenceOcr;

    [ActivatorUtilitiesConstructor]
    public LicensePlateOcrService(
        IPlateDetector plateDetector,
        ICharacterSegmenter characterSegmenter,
        ICharacterClassifier characterClassifier,
        IEuropeanPlateValidator europeanValidator,
        IPlateSequenceOcr? sequenceOcr = null)
    {
        _plateDetector = plateDetector ?? throw new ArgumentNullException(nameof(plateDetector));
        _characterSegmenter = characterSegmenter ?? throw new ArgumentNullException(nameof(characterSegmenter));
        _characterClassifier = characterClassifier ?? throw new ArgumentNullException(nameof(characterClassifier));
        _europeanValidator = europeanValidator ?? throw new ArgumentNullException(nameof(europeanValidator));
        _sequenceOcr = sequenceOcr;
    }

    public LicensePlateOcrService(
        IPlateDetector plateDetector,
        ICharacterSegmenter characterSegmenter,
        ICharacterClassifier characterClassifier,
        IRomanianPlateValidator romanianValidator)
        : this(plateDetector, characterSegmenter, characterClassifier, (IEuropeanPlateValidator)romanianValidator, null)
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
            string rawText = string.Empty;
            float avgConf = 0f;
            List<CharacterBox> charBoxes = [];
            string countryChars = string.Empty;
            bool candidateHasEuroband = candidate.HasEurobandStrip;

            // 1. Try deep-learning sequence OCR with visual Euroband elimination
            if (_sequenceOcr != null)
            {
                // Run OCR on original candidate crop
                var seqRaw = _sequenceOcr.Recognize(candidate.PlateCrop);

                // Check for Euroband blue strip on the left margin and eliminate it visually
                var (strippedCrop, hasEuroband, cutX) = StripEuroband(candidate.PlateCrop);
                if (hasEuroband)
                {
                    candidateHasEuroband = true;
                    using (strippedCrop)
                    {
                        var seqStripped = _sequenceOcr.Recognize(strippedCrop);

                        string cleanRaw = CleanNoiseSymbols(seqRaw.RawText);
                        string cleanStripped = CleanNoiseSymbols(seqStripped.RawText);

                        int rawAlphanumeric = CountAlphanumeric(cleanRaw);
                        int strippedAlphanumeric = CountAlphanumeric(cleanStripped);

                        string? inferredCountry = InferCountryFromEuroband(cleanRaw, cleanStripped);
                        if (!string.IsNullOrEmpty(inferredCountry))
                        {
                            countryChars = inferredCountry;
                        }

                        // When Euroband is eliminated:
                        // Prefer stripped if it recognized valid plate characters (>= 3) AND
                        // (has equal/more characters than raw OR raw had a recognized country prefix that was removed)
                        bool preferStripped = strippedAlphanumeric >= 3 &&
                                              (strippedAlphanumeric >= rawAlphanumeric || !string.IsNullOrEmpty(inferredCountry));

                        if (preferStripped)
                        {
                            rawText = seqStripped.RawText;
                            avgConf = seqStripped.Confidence;
                            charBoxes = seqStripped.Characters.Select(c => c with
                            {
                                Box = new PlateRectangle(c.Box.X + cutX, c.Box.Y, c.Box.Width, c.Box.Height)
                            }).ToList();
                        }
                        else
                        {
                            rawText = seqRaw.RawText;
                            avgConf = seqRaw.Confidence;
                            charBoxes = seqRaw.Characters;
                        }
                    }
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(seqRaw.RawText) && seqRaw.RawText.Length >= 3)
                    {
                        rawText = seqRaw.RawText;
                        avgConf = seqRaw.Confidence;
                        charBoxes = seqRaw.Characters;
                    }
                }

                // If text seems short or has dark shadow, test contrast enhancement (CLAHE)
                if (CountAlphanumeric(rawText) <= 6 && !candidate.PlateCrop.Empty())
                {
                    using var enhanced = EnhanceContrast(candidate.PlateCrop);
                    var seqEnhanced = _sequenceOcr.Recognize(enhanced);
                    string cleanEnhanced = CleanNoiseSymbols(seqEnhanced.RawText);
                    if (CountAlphanumeric(cleanEnhanced) > CountAlphanumeric(rawText))
                    {
                        rawText = seqEnhanced.RawText;
                        avgConf = seqEnhanced.Confidence;
                        charBoxes = seqEnhanced.Characters;
                    }
                }
            }

            // 2. Fallback to classical character segmentation + classifier if sequence OCR produced no text
            if (string.IsNullOrWhiteSpace(rawText))
            {
                var segmentedChars = _characterSegmenter.SegmentCharacters(candidate.PlateCrop, candidate.HasEurobandStrip, candidate.HasSwissShield);
                if (segmentedChars.Count == 0)
                {
                    candidate.Dispose();
                    continue;
                }

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

                rawText = plateCharsSb.ToString();
                countryChars = countryCharsSb.ToString();
                avgConf = plateCharCount > 0 ? confidenceSum / plateCharCount : 0f;
            }

            // 3. Determine Country Code from Euroband or text prefix
            string? detectedCountry = ResolveCountryCode(candidate, countryChars, rawText);

            // 4. Strip Country Identifier if requested and present in text
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
                else if (detectedCountry != null && (cleanPlate.StartsWith(detectedCountry + " ", StringComparison.OrdinalIgnoreCase) ||
                                                      cleanPlate.StartsWith(detectedCountry + "-", StringComparison.OrdinalIgnoreCase) ||
                                                      cleanPlate.StartsWith(detectedCountry + "_", StringComparison.OrdinalIgnoreCase)))
                {
                    int cutLen = detectedCountry.Length + 1;
                    cleanPlate = cleanPlate[cutLen..].Trim();
                    for (int i = 0; i < Math.Min(cutLen, charBoxes.Count); i++)
                    {
                        charBoxes[i] = charBoxes[i] with { IsCountryCode = true };
                    }
                }
                else if (cleanPlate.StartsWith("RO ", StringComparison.OrdinalIgnoreCase) || cleanPlate.StartsWith("R0 ", StringComparison.OrdinalIgnoreCase))
                {
                    detectedCountry ??= "RO";
                    cleanPlate = cleanPlate[3..].Trim();
                    for (int i = 0; i < Math.Min(3, charBoxes.Count); i++)
                    {
                        charBoxes[i] = charBoxes[i] with { IsCountryCode = true };
                    }
                }
            }

            // 5. Validate and clean with European plate validator
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

        string upperRaw = rawText.ToUpperInvariant().Trim();
        string compact = Regex.Replace(upperRaw, @"[^A-Z0-9]", "");

        // 3. Text prefix check
        if (upperRaw.StartsWith("RO ") || upperRaw.StartsWith("RO-") || upperRaw.StartsWith("R0 ") || upperRaw.StartsWith("R0-"))
        {
            return "RO";
        }
        if (upperRaw.StartsWith("D ") || upperRaw.StartsWith("D-"))
        {
            return "D";
        }
        if (upperRaw.StartsWith("F ") || upperRaw.StartsWith("F-"))
        {
            return "F";
        }
        if (upperRaw.StartsWith("E ") || upperRaw.StartsWith("E-"))
        {
            return "E";
        }
        if (upperRaw.StartsWith("PL ") || upperRaw.StartsWith("PL-"))
        {
            return "PL";
        }
        if (upperRaw.StartsWith("UK ") || upperRaw.StartsWith("UK-") || upperRaw.StartsWith("GB ") || upperRaw.StartsWith("GB-"))
        {
            return "UK";
        }

        // 4. Country code from European plate formats if Euroband is present
        if (candidate.HasEurobandStrip)
        {
            // German: District (1-3 letters) + Letters (1-2) + Digits (1-4) -> e.g. B MW 1234, HH XY 999
            // German plates end in digits, while standard Romanian plates end in letters!
            if (Regex.IsMatch(compact, @"^[A-Z]{1,3}[A-Z]{1,2}\d{1,4}$") &&
                !Regex.IsMatch(compact, @"^B\d{2,3}[A-Z]{3}$") &&
                EuropeanPlateValidator.GermanDistricts.Any(d => compact.StartsWith(d)))
            {
                return "D";
            }

            // Romanian: B 123 ABC or CJ 12 ABC
            if (Regex.IsMatch(compact, @"^B\d{2,3}[A-Z]{3}$") ||
                (compact.Length is 6 or 7 or 8 && EuropeanPlateValidator.RomanianCounties.Contains(compact[..2]) && (char.IsDigit(compact[2]) || compact[2] is 'O' or 'I') && char.IsLetter(compact[^1])))
            {
                return "RO";
            }

            // Spanish: 1234 BCD
            if (Regex.IsMatch(compact, @"^\d{4}[A-Z]{3}$") || Regex.IsMatch(compact, @"^E\d{4}[A-Z]{3}$"))
            {
                return "E";
            }

            // French: AA 123 AA
            if (Regex.IsMatch(compact, @"^[A-Z]{2}\d{3}[A-Z]{2}$"))
            {
                return "F";
            }

            // Polish: WA 12345
            if (Regex.IsMatch(compact, @"^[A-Z]{2,3}[A-Z0-9]{4,5}$") &&
                !Regex.IsMatch(compact, @"^B\d{2,3}[A-Z]{3}$") &&
                (compact.Length != 7 || !EuropeanPlateValidator.RomanianCounties.Contains(compact[..2])))
            {
                return "PL";
            }

            return null;
        }

        return null;
    }

    private static (Mat strippedCrop, bool hasEuroband, int cutX) StripEuroband(Mat plateCrop)
    {
        if (plateCrop.Empty() || plateCrop.Cols < 20 || plateCrop.Rows < 10)
        {
            return (plateCrop.Clone(), false, 0);
        }

        int h = plateCrop.Rows;
        int w = plateCrop.Cols;

        using var hsv = new Mat();
        Cv2.CvtColor(plateCrop, hsv, ColorConversionCodes.BGR2HSV);

        // Euroband Blue range in HSV: H: 98-132, S: 65-255, V: 40-255
        using var blueMask = new Mat();
        Cv2.InRange(hsv, new Scalar(98, 65, 40), new Scalar(132, 255, 255), blueMask);

        // On European plates, the Euroband is at most 15% of the plate width
        int maxCheck = Math.Min(w, (int)(w * 0.15));
        int consecutiveBlue = 0;
        int lastBlueX = 0;
        int gapCount = 0;

        for (int x = 0; x < maxCheck; x++)
        {
            int blueCount = 0;
            for (int y = 0; y < h; y++)
            {
                if (blueMask.At<byte>(y, x) > 0)
                    blueCount++;
            }

            double density = (double)blueCount / h;
            if (density >= 0.22)
            {
                consecutiveBlue++;
                lastBlueX = x;
                gapCount = 0;
            }
            else
            {
                if (consecutiveBlue >= 3)
                {
                    gapCount++;
                    if (gapCount >= 2)
                        break;
                }
            }
        }

        if (consecutiveBlue >= 3 && lastBlueX > 0 && lastBlueX < (int)(w * 0.16))
        {
            int cutX = lastBlueX + 1;
            Rect roi = new Rect(cutX, 0, w - cutX, h);
            return (new Mat(plateCrop, roi).Clone(), true, cutX);
        }

        return (plateCrop.Clone(), false, 0);
    }

    private static string CleanNoiseSymbols(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        string s = Regex.Replace(text, @"^[+:\-_„\.\s]+", "");
        s = Regex.Replace(s, @"[+:\-_„\.\s]+$", "");
        return s.Trim();
    }

    private static int CountAlphanumeric(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        return text.Count(char.IsLetterOrDigit);
    }

    private static string? InferCountryFromEuroband(string rawText, string strippedText)
    {
        string r = Regex.Replace(rawText.ToUpperInvariant(), @"[^A-Z0-9]", "");
        string s = Regex.Replace(strippedText.ToUpperInvariant(), @"[^A-Z0-9]", "");

        if (r.Length > s.Length && r.EndsWith(s))
        {
            string prefix = r[..^s.Length];
            if (prefix is "RO" or "R0" or "R") return "RO";
            if (prefix is "PL") return "PL";
            if (prefix is "D") return "D";
            if (prefix is "F") return "F";
            if (prefix is "E") return "E";
            if (prefix is "I") return "I";
            if (prefix is "UK" or "GB") return "UK";
            if (prefix is "CZ") return "CZ";
            if (prefix is "SK") return "SK";
            if (prefix is "NL") return "NL";
            if (prefix is "B") return "B";
            if (prefix is "A") return "A";
        }

        if (r.StartsWith("RO") || r.StartsWith("R0") || r.StartsWith("RCJ")) return "RO";
        if (r.StartsWith("PL")) return "PL";
        if (r.StartsWith("E") && Regex.IsMatch(s, @"^\d{4}")) return "E";
        if (r.StartsWith("A") && s.StartsWith("AA")) return "F";
        if (r.StartsWith("UK") || r.StartsWith("GB")) return "UK";

        return null;
    }

    private static Mat EnhanceContrast(Mat crop)
    {
        using var lab = new Mat();
        Cv2.CvtColor(crop, lab, ColorConversionCodes.BGR2Lab);
        Mat[] channels = Cv2.Split(lab);
        using var clahe = Cv2.CreateCLAHE(clipLimit: 2.0, tileGridSize: new Size(4, 4));
        clahe.Apply(channels[0], channels[0]);
        using var merged = new Mat();
        Cv2.Merge(channels, merged);
        foreach (var c in channels) c.Dispose();
        var enhanced = new Mat();
        Cv2.CvtColor(merged, enhanced, ColorConversionCodes.Lab2BGR);
        return enhanced;
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
