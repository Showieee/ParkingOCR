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
        var validator = new RomanianPlateValidator();
        var detector = new PlateDetector();
        var segmenter = new CharacterSegmenter();
        var classifier = new OnnxCharacterClassifier();

        _service = new LicensePlateOcrService(detector, segmenter, classifier, validator);
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
    public void RecognizePlate_InvalidImage_ReturnsFriendlyFailure()
    {
        byte[] garbage = [1, 2, 3, 4, 5];
        var result = _service.RecognizePlate(garbage);

        Assert.False(result.Success);
        Assert.Contains("Failed to decode", result.Message);
    }
}
