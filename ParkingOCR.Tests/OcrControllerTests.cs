using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using ParkingOCR.Core.Services;
using ParkingOCR.Web.Controllers;
using ParkingOCR.Web.Models;
using Xunit;

namespace ParkingOCR.Tests;

public class OcrControllerTests
{
    private readonly OcrController _controller;
    private readonly string _webContentRoot;

    public OcrControllerTests()
    {
        var validator = new RomanianPlateValidator();
        var detector = new PlateDetector();
        var segmenter = new CharacterSegmenter();
        var classifier = new OnnxCharacterClassifier();
        var ocrService = new LicensePlateOcrService(detector, segmenter, classifier, validator);

        // Web project directory contains SampleImages
        var baseDir = AppContext.BaseDirectory;
        var currentDir = new DirectoryInfo(baseDir);
        string? webDir = null;
        while (currentDir != null)
        {
            var candidate = Path.Combine(currentDir.FullName, "ParkingOCR.Web");
            if (Directory.Exists(candidate))
            {
                webDir = candidate;
                break;
            }
            currentDir = currentDir.Parent;
        }

        _webContentRoot = webDir ?? Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "ParkingOCR.Web"));

        _controller = new OcrController(ocrService);
    }

    [Fact]
    public void Health_ReturnsHealthyStatus()
    {
        var actionResult = _controller.Health();
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public void GetSamples_ReturnsAvailablePlateSamples()
    {
        var result = _controller.GetSamples();
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var samples = Assert.IsType<List<SampleImageDto>>(okResult.Value);

        Assert.NotEmpty(samples);
        Assert.Contains(samples, s => s.Id == "bmw-b111yul");
        Assert.Contains(samples, s => s.Id == "astra-gj04ujp");
        Assert.Contains(samples, s => s.Id == "swiss-zh84521");
        Assert.Contains(samples, s => s.Id == "uk-yellow");
        Assert.Contains(samples, s => s.Id == "german-bmw");
        Assert.Contains(samples, s => s.Id == "bucharest");
        Assert.Contains(samples, s => s.Id == "cluj");
    }

    [Fact]
    public async Task RecognizeSample_Swiss_RecognizesWithoutEuroband()
    {
        var result = await _controller.RecognizeSample("swiss-zh84521");
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<OcrRecognitionResponse>(okResult.Value);

        Assert.True(response.Success);
        Assert.Equal("ZH 84521", response.PlateText);
        Assert.Equal("CH", response.CountryCode);
        Assert.Equal("Switzerland", response.CountryName);
        Assert.False(response.HasEuroband);
        Assert.NotNull(response.PlateCropBase64);
        Assert.NotNull(response.ProcessedImageBase64);
    }

    [Fact]
    public async Task RecognizeSample_German_RecognizesWithEuroband()
    {
        var result = await _controller.RecognizeSample("german-bmw");
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<OcrRecognitionResponse>(okResult.Value);

        Assert.True(response.Success);
        Assert.Equal("D", response.CountryCode);
        Assert.Equal("Germany", response.CountryName);
        Assert.True(response.HasEuroband);
        Assert.True(response.PlateText.Length >= 4);
    }

    [Fact]
    public async Task RecognizeSample_Bucharest_StripsCountryCodeAndNormalizes()
    {
        var result = await _controller.RecognizeSample("bucharest");
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<OcrRecognitionResponse>(okResult.Value);

        Assert.True(response.Success);
        Assert.Equal("B 123 ABC", response.PlateText);
        Assert.Equal("RO", response.CountryCode);
        Assert.Equal("B", response.Validation.County);
        Assert.NotNull(response.PlateCropBase64);
        Assert.NotNull(response.ProcessedImageBase64);
    }

    [Fact]
    public async Task RecognizeSample_Cluj_StripsCountryCodeAndNormalizes()
    {
        var result = await _controller.RecognizeSample("cluj");
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<OcrRecognitionResponse>(okResult.Value);

        Assert.True(response.Success);
        Assert.Equal("CJ 12 ABC", response.PlateText);
        Assert.Equal("RO", response.CountryCode);
        Assert.Equal("CJ", response.Validation.County);
    }

    [Fact]
    public async Task Recognize_WithFormFile_ReturnsValidPlate()
    {
        var samplePath = Path.Combine(_webContentRoot, "SampleImages", "county_cj12abc.jpg");
        var fileBytes = await File.ReadAllBytesAsync(samplePath);

        using var stream = new MemoryStream(fileBytes);
        var formFile = new FormFile(stream, 0, fileBytes.Length, "file", "county_cj12abc.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };

        var result = await _controller.Recognize(formFile, true, 0.35f, true, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<OcrRecognitionResponse>(okResult.Value);

        Assert.True(response.Success);
        Assert.Equal("CJ 12 ABC", response.PlateText);
        Assert.Equal("RO", response.CountryCode);
    }

    [Fact]
    public async Task Recognize_WithBmwFormFile_ReturnsB111YUL()
    {
        var samplePath = Path.Combine(_webContentRoot, "SampleImages", "user_bmw_b111yul.jpg");
        var fileBytes = await File.ReadAllBytesAsync(samplePath);

        using var stream = new MemoryStream(fileBytes);
        var formFile = new FormFile(stream, 0, fileBytes.Length, "file", "user_bmw_b111yul.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };

        var result = await _controller.Recognize(formFile, true, 0.35f, true, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<OcrRecognitionResponse>(okResult.Value);

        Assert.True(response.Success);
        Assert.Equal("B 111 YUL", response.PlateText);
        Assert.Equal("RO", response.CountryCode);
    }

    [Fact]
    public async Task Recognize_WithJsonBase64_ReturnsValidPlate()
    {
        var samplePath = Path.Combine(_webContentRoot, "SampleImages", "bucharest_b123abc.jpg");
        var fileBytes = await File.ReadAllBytesAsync(samplePath);
        var base64 = $"data:image/jpeg;base64,{Convert.ToBase64String(fileBytes)}";

        var jsonReq = new OcrRecognitionRequest
        {
            ImageBase64 = base64,
            FilterCountryCode = true,
            ConfidenceThreshold = 0.35f
        };

        var result = await _controller.RecognizeJson(jsonReq, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<OcrRecognitionResponse>(okResult.Value);

        Assert.True(response.Success);
        Assert.Equal("B 123 ABC", response.PlateText);
        Assert.Equal("RO", response.CountryCode);
    }

    [Fact]
    public async Task Recognize_WithEmptyInput_ReturnsBadRequest()
    {
        var result = await _controller.Recognize(null, null, null, null, CancellationToken.None);
        var badResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<OcrRecognitionResponse>(badResult.Value);

        Assert.False(response.Success);
        Assert.NotNull(response.Message);
    }
}
