using OpenCvSharp;
using ParkingOCR.Core.Models;
using ParkingOCR.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace ParkingOCR.Tests;

public class EuropeanPlateOcrIntegrationTests
{
    private readonly LicensePlateOcrService _service;
    private readonly ITestOutputHelper _output;
    private static readonly string TestImagesDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "TestImages");

    public EuropeanPlateOcrIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        var validator = new EuropeanPlateValidator();
        var detector = new OnnxPlateDetector();
        var sequenceOcr = new OnnxSequenceOcr();
        var segmenter = new CharacterSegmenter();
        var classifier = new OnnxCharacterClassifier();

        _service = new LicensePlateOcrService(detector, segmenter, classifier, validator, sequenceOcr);
        Directory.CreateDirectory(TestImagesDir);
    }

    [Fact]
    public async Task Recognize_SwissPlate_WithoutEuroband_SuccessfullyRecognized()
    {
        // Swiss plate: Red shield with white cross on left, NO Euroband, Canton ZH + numbers
        string filename = "swiss_zh84521.jpg";
        string filePath = Path.Combine(TestImagesDir, filename);

        using (var mat = TestImageGenerator.CreateEuropeanPlateImage("ZH 84521", eurobandCountry: null, yellowBackground: false, isSwiss: true))
        {
            mat.SaveImage(filePath);
        }

        var result = await _service.RecognizePlateAsync(filePath);

        _output.WriteLine($"Swiss Result: Success={result.Success}, Plate='{result.PlateText}', Country='{result.CountryCode}', Conf={result.Confidence:P1}");
        Assert.True(result.Success, result.Message);
        Assert.Equal("CH", result.CountryCode);
        Assert.Contains("ZH", result.PlateText);
        Assert.True(result.Validation.IsValid);
        Assert.Equal("Swiss", result.Validation.PlateType);
    }

    [Fact]
    public async Task Recognize_GermanPlate_WithEuroband_StripsCountryCodeD()
    {
        string filename = "german_bmw1234.jpg";
        string filePath = Path.Combine(TestImagesDir, filename);

        using (var mat = TestImageGenerator.CreateEuropeanPlateImage("B MW 1234", eurobandCountry: "D"))
        {
            mat.SaveImage(filePath);
        }

        var result = await _service.RecognizePlateAsync(filePath);

        _output.WriteLine($"German Result: Success={result.Success}, Plate='{result.PlateText}', Country='{result.CountryCode}'");
        foreach (var c in result.Characters)
        {
            _output.WriteLine($"  Char='{c.Character}', Conf={c.Confidence:P1}, IsCountry={c.IsCountryCode}, Box={c.Box.X},{c.Box.Y},{c.Box.Width},{c.Box.Height}");
        }
        Assert.True(result.Success, result.Message);
        Assert.Equal("D", result.CountryCode);
        Assert.True(result.PlateText.Length >= 4);
        Assert.True(result.Validation.IsValid);
    }

    [Fact]
    public async Task Recognize_UkYellowPlate_SuccessfullyDetectedAndRecognized()
    {
        // UK rear plate: yellow background with black letters
        string filename = "uk_yellow_bd12xyz.jpg";
        string filePath = Path.Combine(TestImagesDir, filename);

        using (var mat = TestImageGenerator.CreateEuropeanPlateImage("BD12 XYZ", eurobandCountry: "UK", yellowBackground: true))
        {
            mat.SaveImage(filePath);
        }

        var result = await _service.RecognizePlateAsync(filePath);

        _output.WriteLine($"UK Yellow Result: Success={result.Success}, Plate='{result.PlateText}', Country='{result.CountryCode}'");
        Assert.True(result.Success, result.Message);
        Assert.Equal("UK", result.CountryCode);
        Assert.True(result.Validation.IsValid);
    }

    [Fact]
    public async Task Recognize_FrenchPlate_StripsEurobandF()
    {
        string filename = "french_aa123aa.jpg";
        string filePath = Path.Combine(TestImagesDir, filename);

        using (var mat = TestImageGenerator.CreateEuropeanPlateImage("AA 123 AA", eurobandCountry: "F"))
        {
            mat.SaveImage(filePath);
        }

        var result = await _service.RecognizePlateAsync(filePath);

        _output.WriteLine($"French Result: Success={result.Success}, Plate='{result.PlateText}', Country='{result.CountryCode}'");
        Assert.True(result.Success, result.Message);
        Assert.Equal("F", result.CountryCode);
        Assert.True(result.Validation.IsValid);
    }

    [Fact]
    public async Task Recognize_SpanishPlate_RecognizesDigitsAndLetters()
    {
        string filename = "spanish_1234bcd.jpg";
        string filePath = Path.Combine(TestImagesDir, filename);

        using (var mat = TestImageGenerator.CreateEuropeanPlateImage("1234 BCD", eurobandCountry: "E"))
        {
            mat.SaveImage(filePath);
        }

        var result = await _service.RecognizePlateAsync(filePath);

        _output.WriteLine($"Spanish Result: Success={result.Success}, Plate='{result.PlateText}', Country='{result.CountryCode}'");
        Assert.True(result.Success, result.Message);
        Assert.Equal("E", result.CountryCode);
        Assert.Contains("BCD", result.PlateText);
        Assert.True(result.Validation.IsValid);
    }

    [Fact]
    public async Task Recognize_PolishPlate_StripsEurobandPL()
    {
        string filename = "polish_wa12345.jpg";
        string filePath = Path.Combine(TestImagesDir, filename);

        using (var mat = TestImageGenerator.CreateEuropeanPlateImage("WA 12345", eurobandCountry: "PL"))
        {
            mat.SaveImage(filePath);
        }

        var result = await _service.RecognizePlateAsync(filePath);

        _output.WriteLine($"Polish Result: Success={result.Success}, Plate='{result.PlateText}', Country='{result.CountryCode}'");
        Assert.True(result.Success, result.Message);
        Assert.Equal("PL", result.CountryCode);
        Assert.Contains("WA", result.PlateText);
        Assert.True(result.Validation.IsValid);
    }

    [Fact]
    public async Task Recognize_SwissPlate_EmbeddedInBumper_DetectedWithoutEuroband()
    {
        // Swiss plate embedded in realistic bumper background without Euroband
        string filename = "swiss_bumper_zh84521.jpg";
        string filePath = Path.Combine(TestImagesDir, filename);

        using (var mat = TestImageGenerator.CreateEuropeanPlateImage("ZH 84521", eurobandCountry: null, yellowBackground: false, isSwiss: true, embedInBumper: true))
        {
            mat.SaveImage(filePath);
        }

        var result = await _service.RecognizePlateAsync(filePath);

        _output.WriteLine($"Swiss Bumper Result: Success={result.Success}, Plate='{result.PlateText}', Country='{result.CountryCode}'");
        Assert.True(result.Success, result.Message);
        Assert.Equal("CH", result.CountryCode);
        Assert.NotNull(result.BoundingBox);
    }
}
