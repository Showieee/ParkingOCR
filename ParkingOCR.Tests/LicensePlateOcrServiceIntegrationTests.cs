using OpenCvSharp;
using ParkingOCR.Core.Models;
using ParkingOCR.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace ParkingOCR.Tests;

public class LicensePlateOcrServiceIntegrationTests
{
    private readonly LicensePlateOcrService _service;
    private readonly ITestOutputHelper _output;
    private static readonly string TestImagesDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "TestImages");

    public LicensePlateOcrServiceIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        var validator = new EuropeanPlateValidator();
        var detector = new OnnxPlateDetector();
        var sequenceOcr = new OnnxSequenceOcr();
        var segmenter = new CharacterSegmenter();
        var classifier = new OnnxCharacterClassifier();

        _service = new LicensePlateOcrService(detector, segmenter, classifier, validator, sequenceOcr);
    }

    [Fact]
    public async Task RecognizePlateAsync_BucharestPlateWithEuroband_ExtractsTextAndStripsCountryCode()
    {
        string imgPath = Path.Combine(TestImagesDir, "bucharest_b123abc.jpg");
        var result = await _service.RecognizePlateAsync(imgPath);

        Assert.True(result.Success, result.Message);
        Assert.Equal("RO", result.CountryCode);
        Assert.Equal("B 123 ABC", result.PlateText);
        Assert.True(result.Validation.IsValid);
        Assert.Equal("B", result.Validation.County);
        Assert.Equal("123", result.Validation.Numbers);
        Assert.Equal("ABC", result.Validation.Letters);
        Assert.NotNull(result.PlateCropJpeg);
        Assert.True(result.PlateCropJpeg.Length > 0);
        Assert.NotNull(result.ProcessedImageJpeg);
        Assert.True(result.ProcessedImageJpeg.Length > 0);
        Assert.True(result.Confidence > 0.5f);
        Assert.True(result.ProcessingTimeMs > 0);
    }

    [Fact]
    public async Task RecognizePlateAsync_CountyPlateWithEuroband_ExtractsTextAndStripsCountryCode()
    {
        string imgPath = Path.Combine(TestImagesDir, "county_cj12abc.jpg");
        var result = await _service.RecognizePlateAsync(imgPath);

        Assert.True(result.Success, result.Message);
        Assert.Equal("RO", result.CountryCode);
        Assert.Equal("CJ 12 ABC", result.PlateText);
        Assert.True(result.Validation.IsValid);
        Assert.Equal("CJ", result.Validation.County);
        Assert.Equal("12", result.Validation.Numbers);
        Assert.Equal("ABC", result.Validation.Letters);
    }

    [Fact]
    public async Task RecognizePlateAsync_AnotherCountyPlate_ExtractsText()
    {
        string imgPath = Path.Combine(TestImagesDir, "county_is99xyz.jpg");
        var result = await _service.RecognizePlateAsync(imgPath);

        Assert.True(result.Success, result.Message);
        Assert.Equal("RO", result.CountryCode);
        Assert.Contains("IS", result.PlateText);
        Assert.True(result.Validation.IsValid);
    }

    [Fact]
    public async Task RecognizePlateAsync_NoEuroband_ExtractsCleanPlate()
    {
        string imgPath = Path.Combine(TestImagesDir, "no_euroband_tm01aaa.jpg");
        var result = await _service.RecognizePlateAsync(imgPath);

        Assert.True(result.Success, result.Message);
        Assert.Contains("AAA", result.PlateText);
        Assert.True(result.Validation.IsValid);
    }

    [Fact]
    public async Task RecognizePlateAsync_EmbeddedInBumper_DetectsAndRecognizes()
    {
        string imgPath = Path.Combine(TestImagesDir, "car_bumper_b777bos.jpg");
        var result = await _service.RecognizePlateAsync(imgPath);

        Assert.True(result.Success, result.Message);
        Assert.Equal("B 777 BOS", result.PlateText);
        Assert.NotNull(result.BoundingBox);
        Assert.True(result.BoundingBox.Width > 100);
        Assert.True(result.BoundingBox.Height > 30);
    }

    [Fact]
    public async Task RecognizePlateAsync_UserBmwImage_RecognizesB111YUL()
    {
        string imgPath = Path.Combine(TestImagesDir, "user_bmw_b111yul.jpg");
        var result = await _service.RecognizePlateAsync(imgPath);

        _output.WriteLine($"BMW Result: Success={result.Success}, PlateText='{result.PlateText}', RawText='{result.RawText}'");
        foreach (var ch in result.Characters)
        {
            _output.WriteLine($"  Char='{ch.Character}', Conf={ch.Confidence:P1}, IsCountry={ch.IsCountryCode}, Box={ch.Box.X},{ch.Box.Y},{ch.Box.Width},{ch.Box.Height}");
        }

        Assert.True(result.Success, result.Message);
        Assert.Equal("RO", result.CountryCode);
        Assert.Equal("B 111 YUL", result.PlateText);
    }

    [Fact]
    public async Task RecognizePlateAsync_UserAstraImage_RecognizesGJ04UJP()
    {
        string imgPath = Path.Combine(TestImagesDir, "user_astra_gj04ujp.jpg");
        var result = await _service.RecognizePlateAsync(imgPath);

        _output.WriteLine($"Astra Result: Success={result.Success}, PlateText='{result.PlateText}', RawText='{result.RawText}'");
        foreach (var ch in result.Characters)
        {
            _output.WriteLine($"  Char='{ch.Character}', Conf={ch.Confidence:P1}, IsCountry={ch.IsCountryCode}, Box={ch.Box.X},{ch.Box.Y},{ch.Box.Width},{ch.Box.Height}");
        }

        Assert.True(result.Success, result.Message);
        Assert.Equal("RO", result.CountryCode);
        Assert.Equal("GJ 04 UJP", result.PlateText);
    }

    [Fact]
    public async Task RecognizePlateAsync_UserThirteenImages_ProcessesSuccessfully()
    {
        string[] fileNames =
        [
            "images.jpg",
            "images (1).jpg",
            "images (2).jpg",
            "images (3).jpg",
            "images (4).jpg",
            "images (5).jpg",
            "images (6).jpg",
            "images (7).jpg",
            "images (8).jpg",
            "images (9).jpg",
            "images (10).jpg",
            "images (11).jpg",
            "images (12).jpg"
        ];

        int recognizedCount = 0;
        foreach (var fn in fileNames)
        {
            string imgPath = Path.Combine(TestImagesDir, fn);
            if (!File.Exists(imgPath)) continue;

            var result = await _service.RecognizePlateAsync(imgPath);
            _output.WriteLine($"Image '{fn}': Success={result.Success}, Plate='{result.PlateText}', Country='{result.CountryCode}', Conf={result.Confidence:P1}, Chars={result.Characters.Count}");
            if (result.Success && !string.IsNullOrWhiteSpace(result.PlateText))
            {
                recognizedCount++;
            }
        }

        _output.WriteLine($"Successfully recognized {recognizedCount}/{fileNames.Length} images.");
        Assert.True(recognizedCount >= 10, $"Expected at least 10/13 images recognized, but got {recognizedCount}");
    }

    [Fact]
    public void CompareImages8And13()
    {
        string p8 = Path.Combine(TestImagesDir, "images (8).jpg");
        string p13 = Path.Combine(TestImagesDir, "images (13).jpg");

        using var m8 = Cv2.ImRead(p8);
        using var m13 = Cv2.ImRead(p13);
        _output.WriteLine($"images (8).jpg: {m8.Cols}x{m8.Rows}");
        _output.WriteLine($"images (13).jpg: {m13.Cols}x{m13.Rows}");

        using var detector = new OnnxPlateDetector();
        using var seqOcr = new OnnxSequenceOcr();

        var c8 = detector.DetectPlates(m8)[0];
        var c13 = detector.DetectPlates(m13)[0];

        _output.WriteLine($"m8 crop size: {c8.PlateCrop.Cols}x{c8.PlateCrop.Rows}");
        _output.WriteLine($"m13 crop size: {c13.PlateCrop.Cols}x{c13.PlateCrop.Rows}");

        var s8 = seqOcr.Recognize(c8.PlateCrop);
        var s13 = seqOcr.Recognize(c13.PlateCrop);

        _output.WriteLine($"m8 raw seq: '{s8.RawText}'");
        _output.WriteLine($"m13 raw seq: '{s13.RawText}'");
    }

    [Fact]
    public async Task TestTheSixProblemImages()
    {
        var detector = new OnnxPlateDetector();
        var seqOcr = new OnnxSequenceOcr();
        var validator = new EuropeanPlateValidator();
        var segmenter = new CharacterSegmenter();
        var classifier = new OnnxCharacterClassifier();

        var service = new LicensePlateOcrService(detector, segmenter, classifier, validator, seqOcr);

        // 1. user_bmw_b111yul.jpg -> B 111 YUL
        var rBmw = await service.RecognizePlateAsync(Path.Combine(TestImagesDir, "user_bmw_b111yul.jpg"));
        _output.WriteLine($"user_bmw_b111yul.jpg -> Plate='{rBmw.PlateText}', Country='{rBmw.CountryCode}'");
        Assert.Equal("B 111 YUL", rBmw.PlateText);

        // 2. images (9).jpg -> CJ 08 ERJ
        var rCielo = await service.RecognizePlateAsync(Path.Combine(TestImagesDir, "images (9).jpg"));
        _output.WriteLine($"images (9).jpg -> Plate='{rCielo.PlateText}', Country='{rCielo.CountryCode}'");
        Assert.Equal("CJ 08 ERJ", rCielo.PlateText);

        // 3. images (2).jpg -> B 208 GAB
        var rMegane = await service.RecognizePlateAsync(Path.Combine(TestImagesDir, "images (2).jpg"));
        _output.WriteLine($"images (2).jpg -> Plate='{rMegane.PlateText}', Country='{rMegane.CountryCode}'");
        Assert.Equal("B 208 GAB", rMegane.PlateText);

        // 4. images (3).jpg -> IF 01 TMX
        var rTesla = await service.RecognizePlateAsync(Path.Combine(TestImagesDir, "images (3).jpg"));
        _output.WriteLine($"images (3).jpg -> Plate='{rTesla.PlateText}', Country='{rTesla.CountryCode}'");
        Assert.Equal("IF 01 TMX", rTesla.PlateText);

        // 5. Blue BMW in trees -> B 58 BPS (images (13).jpg)
        string bmwTreesFile = Path.Combine(TestImagesDir, "images (13).jpg");
        var rBmwTrees = await service.RecognizePlateAsync(bmwTreesFile);
        _output.WriteLine($"Blue BMW in trees ({Path.GetFileName(bmwTreesFile)}) -> Plate='{rBmwTrees.PlateText}', Country='{rBmwTrees.CountryCode}'");
        Assert.Equal("B 58 BPS", rBmwTrees.PlateText);

        // 6. White Skoda -> LU 068SX (images (8).jpg)
        string skodaFile = Path.Combine(TestImagesDir, "images (8).jpg");
        var rSkoda = await service.RecognizePlateAsync(skodaFile);
        _output.WriteLine($"White Skoda ({Path.GetFileName(skodaFile)}) -> Plate='{rSkoda.PlateText}'");
        Assert.Contains("068", rSkoda.PlateText);

        // 7. swiss_zh84521.jpg -> ZH 84521
        var rSwiss = await service.RecognizePlateAsync(Path.Combine(TestImagesDir, "swiss_zh84521.jpg"));
        _output.WriteLine($"swiss_zh84521.jpg -> Plate='{rSwiss.PlateText}', Country='{rSwiss.CountryCode}'");
        Assert.Equal("ZH 84521", rSwiss.PlateText);
    }

    [Fact]
    public void CheckImages8And13()
    {
        string p8 = Path.Combine(TestImagesDir, "images (8).jpg");
        string p13 = Path.Combine(TestImagesDir, "images (13).jpg");

        _output.WriteLine($"images (8).jpg exists: {File.Exists(p8)}");
        _output.WriteLine($"images (13).jpg exists: {File.Exists(p13)}");

        using var seqOcr = new OnnxSequenceOcr();
        using var detector = new OnnxPlateDetector();

        if (File.Exists(p8))
        {
            using var m8 = Cv2.ImRead(p8);
            var cands8 = detector.DetectPlates(m8);
            _output.WriteLine($"images (8).jpg: cands={cands8.Count}");
            foreach (var c in cands8)
            {
                var s = seqOcr.Recognize(c.PlateCrop);
                _output.WriteLine($"  Box={c.BoundingBox.X},{c.BoundingBox.Y},{c.BoundingBox.Width}x{c.BoundingBox.Height} -> '{s.RawText}'");
            }
        }

        if (File.Exists(p13))
        {
            using var m13 = Cv2.ImRead(p13);
            var cands13 = detector.DetectPlates(m13);
            _output.WriteLine($"images (13).jpg: cands={cands13.Count}");
            foreach (var c in cands13)
            {
                var s = seqOcr.Recognize(c.PlateCrop);
                _output.WriteLine($"  Box={c.BoundingBox.X},{c.BoundingBox.Y},{c.BoundingBox.Width}x{c.BoundingBox.Height} -> '{s.RawText}'");
            }
        }
    }

    [Fact]
    public void TestPaddingVariationsOnMeganeAndTesla()
    {
        using var seqOcr = new OnnxSequenceOcr();

        // images (2).jpg
        string p2 = Path.Combine(TestImagesDir, "images (2).jpg");
        using var m2 = Cv2.ImRead(p2);
        // Base box: 55, 227, 103, 56
        _output.WriteLine("=== images (2).jpg variations ===");
        for (int pad = 0; pad <= 10; pad += 2)
        {
            int px = Math.Max(0, 55 - pad);
            int py = Math.Max(0, 227 - pad);
            int pw = Math.Min(m2.Cols - px, 103 + 2 * pad);
            int ph = Math.Min(m2.Rows - py, 56 + 2 * pad);
            using var crop = new Mat(m2, new Rect(px, py, pw, ph));
            var seq = seqOcr.Recognize(crop);
            _output.WriteLine($"  Pad={pad}: Box=[{px},{py},{pw}x{ph}] -> Seq='{seq.RawText}'");
        }

        // images (3).jpg
        string p3 = Path.Combine(TestImagesDir, "images (3).jpg");
        using var m3 = Cv2.ImRead(p3);
        // Base box: 42, 118, 71, 30
        _output.WriteLine("\n=== images (3).jpg variations ===");
        for (int pad = 0; pad <= 6; pad += 1)
        {
            int px = Math.Max(0, 42 - pad);
            int py = Math.Max(0, 118 - pad);
            int pw = Math.Min(m3.Cols - px, 71 + 2 * pad);
            int ph = Math.Min(m3.Rows - py, 30 + 2 * pad);
            using var crop = new Mat(m3, new Rect(px, py, pw, ph));
            var seq = seqOcr.Recognize(crop);
            _output.WriteLine($"  Pad={pad}: Box=[{px},{py},{pw}x{ph}] -> Seq='{seq.RawText}'");
        }
    }

    [Fact]
    public void InspectCropsOfTargetImages()
    {
        using var detector = new OnnxPlateDetector();
        using var seqOcr = new OnnxSequenceOcr();

        var list = new[]
        {
            "user_bmw_b111yul.jpg",
            "images (9).jpg",
            "images (2).jpg",
            "images (3).jpg",
            "images (8).jpg",
            "images (13).jpg",
            "swiss_zh84521.jpg"
        };

        foreach (var name in list)
        {
            string path = Path.Combine(TestImagesDir, name);
            if (!File.Exists(path))
            {
                _output.WriteLine($"File not found: {name}");
                continue;
            }
            using var mat = Cv2.ImRead(path);
            var cands = detector.DetectPlates(mat);
            _output.WriteLine($"\nFile {name}: {mat.Cols}x{mat.Rows}, Candidates={cands.Count}");
            for (int i = 0; i < cands.Count; i++)
            {
                var cand = cands[i];
                var seq = seqOcr.Recognize(cand.PlateCrop);
                _output.WriteLine($"  [{i}] Box=[{cand.BoundingBox.X},{cand.BoundingBox.Y},{cand.BoundingBox.Width}x{cand.BoundingBox.Height}] Conf={cand.ConfidenceScore:F2} HasBlue={cand.HasEurobandStrip} HasSwiss={cand.HasSwissShield} -> Seq='{seq.RawText}'");
            }
        }
    }

    [Fact]
    public void DiagnoseAllTestImages()
    {
        using var detector = new OnnxPlateDetector();
        using var seqOcr = new OnnxSequenceOcr();

        var files = Directory.GetFiles(TestImagesDir, "*.jpg");
        foreach (var file in files.OrderBy(f => f))
        {
            string fname = Path.GetFileName(file);
            using var mat = Cv2.ImRead(file);
            var candidates = detector.DetectPlates(mat);
            string seqText = candidates.Count > 0 ? seqOcr.Recognize(candidates[0].PlateCrop).RawText : "<no-cand>";
            _output.WriteLine($"{fname,-30} -> Detected: {candidates.Count} cand, Top='{seqText}'");
        }
    }

    [Fact]
    public void DiagnoseProblemImages()
    {
        using var detector = new OnnxPlateDetector();
        using var seqOcr = new OnnxSequenceOcr();

        var files = new[] { "images (9).jpg", "images (2).jpg", "images (3).jpg", "images (8).jpg" };
        foreach (var file in files)
        {
            _output.WriteLine($"\n=== DIAGNOSING {file} ===");
            string imgPath = Path.Combine(TestImagesDir, file);
            using var mat = Cv2.ImRead(imgPath);
            _output.WriteLine($"Image size: {mat.Cols}x{mat.Rows}");

            var candidates = detector.DetectPlates(mat, new OcrOptions { ConfidenceThreshold = 0.10f });
            _output.WriteLine($"Candidates (threshold 0.10): {candidates.Count}");
            for (int i = 0; i < candidates.Count; i++)
            {
                var cand = candidates[i];
                var seq = seqOcr.Recognize(cand.PlateCrop);
                _output.WriteLine($"  [{i}] Box=[{cand.BoundingBox.X},{cand.BoundingBox.Y},{cand.BoundingBox.Width}x{cand.BoundingBox.Height}] Conf={cand.ConfidenceScore:F2} -> Seq='{seq.RawText}', SeqConf={seq.Confidence:P1}");
            }
        }
    }

    [Fact]
    public void TestYoloAndSequenceOcrOnTargetImages()
    {
        using var detector = new OnnxPlateDetector();
        using var seqOcr = new OnnxSequenceOcr();

        var testCases = new (string File, string Expected)[]
        {
            ("user_bmw_b111yul.jpg", "B 111 YUL"),
            ("images (9).jpg", "CJ 08 ERJ"),
            ("images (2).jpg", "B 208 GAB"),
            ("images (3).jpg", "IF 01 TMX"),
            ("images (8).jpg", "B 58 BPS"),
            ("swiss_zh84521.jpg", "ZH 84521")
        };

        foreach (var (file, expected) in testCases)
        {
            string imgPath = Path.Combine(TestImagesDir, file);
            if (!File.Exists(imgPath))
            {
                _output.WriteLine($"File {file} not found!");
                continue;
            }

            using var mat = Cv2.ImRead(imgPath);
            var candidates = detector.DetectPlates(mat);
            _output.WriteLine($"\nFile '{file}': Found {candidates.Count} candidates");
            foreach (var cand in candidates.Take(3))
            {
                var seq = seqOcr.Recognize(cand.PlateCrop);
                _output.WriteLine($"  Cand [{cand.BoundingBox.X},{cand.BoundingBox.Y},{cand.BoundingBox.Width}x{cand.BoundingBox.Height}] Conf={cand.ConfidenceScore:F2} -> Seq='{seq.RawText}', SeqConf={seq.Confidence:P1}");
            }
        }
    }

    [Fact]
    public void InspectNewOnnxModels()
    {
        string baseDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ParkingOCR.Core", "Resources");
        string yoloPath = Path.Combine(baseDir, "yolo_plate.onnx");
        string recPath = Path.Combine(baseDir, "rec.onnx");

        using var yoloSession = new Microsoft.ML.OnnxRuntime.InferenceSession(yoloPath);
        _output.WriteLine("=== YOLO MODEL ===");
        foreach (var input in yoloSession.InputMetadata)
        {
            _output.WriteLine($"Input: {input.Key}, Type={input.Value.ElementType}, Dims=[{string.Join(",", input.Value.Dimensions)}]");
        }
        foreach (var output in yoloSession.OutputMetadata)
        {
            _output.WriteLine($"Output: {output.Key}, Type={output.Value.ElementType}, Dims=[{string.Join(",", output.Value.Dimensions)}]");
        }

        using var recSession = new Microsoft.ML.OnnxRuntime.InferenceSession(recPath);
        _output.WriteLine("=== REC MODEL ===");
        foreach (var input in recSession.InputMetadata)
        {
            _output.WriteLine($"Input: {input.Key}, Type={input.Value.ElementType}, Dims=[{string.Join(",", input.Value.Dimensions)}]");
        }
        foreach (var output in recSession.OutputMetadata)
        {
            _output.WriteLine($"Output: {output.Key}, Type={output.Value.ElementType}, Dims=[{string.Join(",", output.Value.Dimensions)}]");
        }
    }

    [Fact]
    public void RecognizePlate_InvalidImage_ReturnsFriendlyFailure()
    {
        byte[] garbage = [1, 2, 3, 4, 5];
        var result = _service.RecognizePlate(garbage);

        Assert.False(result.Success);
        Assert.Contains("Failed to decode", result.Message);
    }
}
