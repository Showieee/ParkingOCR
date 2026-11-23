using Microsoft.AspNetCore.Mvc;
using ParkingOCR.Core.Models;
using ParkingOCR.Core.Services;
using ParkingOCR.Web.Models;

namespace ParkingOCR.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OcrController : ControllerBase
{
    private readonly ILicensePlateOcrService _ocrService;
    private readonly string _samplesDirectory;
    private readonly ILogger<OcrController>? _logger;

    public OcrController(
        ILicensePlateOcrService ocrService,
        ILogger<OcrController>? logger = null,
        IWebHostEnvironment? env = null)
    {
        _ocrService = ocrService;
        _logger = logger;

        var contentRoot = env?.ContentRootPath;
        if (string.IsNullOrEmpty(contentRoot))
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "ParkingOCR.Web", "SampleImages");
                if (Directory.Exists(candidate))
                {
                    contentRoot = Path.Combine(dir.FullName, "ParkingOCR.Web");
                    break;
                }
                var direct = Path.Combine(dir.FullName, "SampleImages");
                if (Directory.Exists(direct))
                {
                    contentRoot = dir.FullName;
                    break;
                }
                dir = dir.Parent;
            }
        }
        _samplesDirectory = Path.Combine(contentRoot ?? AppContext.BaseDirectory, "SampleImages");
    }

    [HttpPost("recognize")]
    [Consumes("multipart/form-data")]
    [ActionName("Recognize")]
    public async Task<ActionResult<OcrRecognitionResponse>> Recognize(
        [FromForm] IFormFile? file,
        [FromForm] bool? filterCountryCode,
        [FromForm] float? confidenceThreshold,
        [FromForm] bool? drawDebugAnnotations,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new OcrRecognitionResponse
            {
                Success = false,
                Message = "No file uploaded. Please select an image file."
            });
        }

        byte[] imageBytes;
        using (var memoryStream = new MemoryStream())
        {
            await file.CopyToAsync(memoryStream, cancellationToken);
            imageBytes = memoryStream.ToArray();
        }

        var filterCountry = filterCountryCode ?? true;
        var threshold = confidenceThreshold ?? 0.35f;
        var debugAnnotations = drawDebugAnnotations ?? true;

        var options = new OcrOptions
        {
            FilterCountryCode = filterCountry,
            ConfidenceThreshold = threshold,
            DrawDebugAnnotations = debugAnnotations
        };

        var result = await _ocrService.RecognizePlateAsync(imageBytes, options, cancellationToken);
        var response = MapToResponse(result);
        return Ok(response);
    }

    [HttpPost("recognize")]
    [HttpPost("recognize-json")]
    [Consumes("application/json")]
    [ActionName("RecognizeJson")]
    public async Task<ActionResult<OcrRecognitionResponse>> RecognizeJson(
        [FromBody] OcrRecognitionRequest? jsonRequest,
        CancellationToken cancellationToken)
    {
        if (jsonRequest == null || string.IsNullOrWhiteSpace(jsonRequest.ImageBase64))
        {
            return BadRequest(new OcrRecognitionResponse
            {
                Success = false,
                Message = "No image data provided. Please submit base64 data."
            });
        }

        var base64 = jsonRequest.ImageBase64;
        var commaIndex = base64.IndexOf(',');
        if (commaIndex >= 0)
        {
            base64 = base64[(commaIndex + 1)..];
        }

        byte[] imageBytes;
        try
        {
            imageBytes = Convert.FromBase64String(base64);
        }
        catch (FormatException ex)
        {
            return BadRequest(new OcrRecognitionResponse
            {
                Success = false,
                Message = $"Invalid base64 image data: {ex.Message}"
            });
        }

        var options = new OcrOptions
        {
            FilterCountryCode = jsonRequest.FilterCountryCode,
            ConfidenceThreshold = jsonRequest.ConfidenceThreshold,
            DrawDebugAnnotations = jsonRequest.DrawDebugAnnotations
        };

        var result = await _ocrService.RecognizePlateAsync(imageBytes, options, cancellationToken);
        var response = MapToResponse(result);
        return Ok(response);
    }

    [HttpGet("samples")]
    public ActionResult<List<SampleImageDto>> GetSamples()
    {
        var samples = GetSampleDefinitions();
        return Ok(samples);
    }

    [HttpPost("sample/{id}")]
    public async Task<ActionResult<OcrRecognitionResponse>> RecognizeSample(
        string id,
        [FromQuery] bool filterCountryCode = true,
        [FromQuery] float confidenceThreshold = 0.35f,
        CancellationToken cancellationToken = default)
    {
        var sample = GetSampleDefinitions().FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));
        if (sample == null)
        {
            return NotFound(new { message = $"Sample plate '{id}' not found." });
        }

        var filePath = Path.Combine(_samplesDirectory, sample.Filename);

        if (!System.IO.File.Exists(filePath))
        {
            return NotFound(new { message = $"Sample image file '{sample.Filename}' not found on server." });
        }

        var imageBytes = await System.IO.File.ReadAllBytesAsync(filePath, cancellationToken);
        var options = new OcrOptions
        {
            FilterCountryCode = filterCountryCode,
            ConfidenceThreshold = confidenceThreshold,
            DrawDebugAnnotations = true
        };

        var result = await _ocrService.RecognizePlateAsync(imageBytes, options, cancellationToken);
        var response = MapToResponse(result);
        return Ok(response);
    }

    [HttpGet("sample/{id}/image")]
    public IActionResult GetSampleImage(string id)
    {
        var sample = GetSampleDefinitions().FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));
        if (sample == null)
        {
            return NotFound();
        }

        var filePath = Path.Combine(_samplesDirectory, sample.Filename);

        if (!System.IO.File.Exists(filePath))
        {
            return NotFound();
        }

        return PhysicalFile(filePath, "image/jpeg");
    }

    [HttpGet("/api/health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "Healthy",
            service = "ParkingOCR Web API",
            timestamp = DateTime.UtcNow,
            engine = "OpenCvSharp4 + ONNX Runtime (DIN 1451 European Classifier)"
        });
    }

    private static OcrRecognitionResponse MapToResponse(LicensePlateResult result)
    {
        return new OcrRecognitionResponse
        {
            Success = result.Success,
            PlateText = result.PlateText,
            RawText = result.RawText,
            CountryCode = result.CountryCode,
            Confidence = result.Confidence,
            ProcessingTimeMs = result.ProcessingTimeMs,
            BoundingBox = result.BoundingBox,
            Validation = result.Validation,
            Message = result.Message,
            Characters = result.Characters.Select(c => new CharacterBoxDto
            {
                Character = c.Character,
                Confidence = c.Confidence,
                X = c.Box.X,
                Y = c.Box.Y,
                Width = c.Box.Width,
                Height = c.Box.Height,
                IsCountryCode = c.IsCountryCode
            }).ToList(),
            PlateCropBase64 = result.PlateCropJpeg != null && result.PlateCropJpeg.Length > 0
                ? $"data:image/jpeg;base64,{Convert.ToBase64String(result.PlateCropJpeg)}"
                : null,
            ProcessedImageBase64 = result.ProcessedImageJpeg != null && result.ProcessedImageJpeg.Length > 0
                ? $"data:image/jpeg;base64,{Convert.ToBase64String(result.ProcessedImageJpeg)}"
                : null
        };
    }

    private List<SampleImageDto> GetSampleDefinitions()
    {
        var list = new List<SampleImageDto>
        {
            new()
            {
                Id = "bmw-b111yul",
                Filename = "user_bmw_b111yul.jpg",
                Title = "BMW B 111 YUL (User Image)",
                Description = "Authentic vehicle camera capture of BMW with Euroband",
                ExpectedPlate = "B 111 YUL",
                CountryCode = "RO",
                CountryName = "Romania",
                HasEuroband = true
            },
            new()
            {
                Id = "astra-gj04ujp",
                Filename = "user_astra_gj04ujp.jpg",
                Title = "Opel Astra GJ 04 UJP (User Image)",
                Description = "Authentic vehicle capture in low-light / shaded conditions",
                ExpectedPlate = "GJ 04 UJP",
                CountryCode = "RO",
                CountryName = "Romania",
                HasEuroband = true
            },
            new()
            {
                Id = "cielo-cj08erj",
                Filename = "daewoo_cj08erj.jpg",
                Title = "Daewoo Cielo CJ 08 ERJ (User Image)",
                Description = "Authentic angled front capture with Cluj county registration",
                ExpectedPlate = "CJ 08 ERI",
                CountryCode = "RO",
                CountryName = "Romania",
                HasEuroband = true
            },
            new()
            {
                Id = "swiss-zh84521",
                Filename = "swiss_zh84521.jpg",
                Title = "Switzerland ZH 84521 (No Euroband)",
                Description = "Swiss plate with Swiss cross shield & Zurich canton coat of arms (No Euroband)",
                ExpectedPlate = "ZH 84521",
                CountryCode = "CH",
                CountryName = "Switzerland",
                HasEuroband = false
            },
            new()
            {
                Id = "swiss-bumper",
                Filename = "swiss_bumper_zh84521.jpg",
                Title = "Swiss Bumper Scene (No Euroband)",
                Description = "Swiss license plate mounted on rear car bumper without Euroband",
                ExpectedPlate = "ZH 84521",
                CountryCode = "CH",
                CountryName = "Switzerland",
                HasEuroband = false
            },
            new()
            {
                Id = "uk-yellow",
                Filename = "uk_yellow_bd12xyz.jpg",
                Title = "UK Yellow Plate BD12 XYZ",
                Description = "British standard yellow reflective rear license plate",
                ExpectedPlate = "BD12 XYZ",
                CountryCode = "UK",
                CountryName = "United Kingdom",
                HasEuroband = false
            },
            new()
            {
                Id = "german-bmw",
                Filename = "german_bmw1234.jpg",
                Title = "Germany B MW 1234 (Euroband D)",
                Description = "German DIN plate with blue 'D' Euroband",
                ExpectedPlate = "B MW 1234",
                CountryCode = "D",
                CountryName = "Germany",
                HasEuroband = true
            },
            new()
            {
                Id = "french-aa123aa",
                Filename = "french_aa123aa.jpg",
                Title = "France AA-123-AA (Euroband F)",
                Description = "French SIV format plate with blue 'F' Euroband",
                ExpectedPlate = "AA 123 AA",
                CountryCode = "F",
                CountryName = "France",
                HasEuroband = true
            },
            new()
            {
                Id = "spanish-1234bcd",
                Filename = "spanish_1234bcd.jpg",
                Title = "Spain 1234 BCD (Euroband E)",
                Description = "Spanish format with 4 digits and 3 consonants (Euroband E)",
                ExpectedPlate = "1234 BCD",
                CountryCode = "E",
                CountryName = "Spain",
                HasEuroband = true
            },
            new()
            {
                Id = "polish-wa12345",
                Filename = "polish_wa12345.jpg",
                Title = "Poland WA 12345 (Euroband PL)",
                Description = "Polish Warsaw plate with 'PL' Euroband",
                ExpectedPlate = "WA 12345",
                CountryCode = "PL",
                CountryName = "Poland",
                HasEuroband = true
            },
            new()
            {
                Id = "bucharest",
                Filename = "bucharest_b123abc.jpg",
                Title = "Bucharest B 123 ABC (Euroband RO)",
                Description = "Standard Bucharest plate with blue EU 'RO' strip",
                ExpectedPlate = "B 123 ABC",
                CountryCode = "RO",
                CountryName = "Romania",
                HasEuroband = true
            },
            new()
            {
                Id = "cluj",
                Filename = "county_cj12abc.jpg",
                Title = "Cluj County CJ 12 ABC",
                Description = "Standard 2-letter county plate with 'RO' strip",
                ExpectedPlate = "CJ 12 ABC",
                CountryCode = "RO",
                CountryName = "Romania",
                HasEuroband = true
            },
            new()
            {
                Id = "timis-no-euroband",
                Filename = "no_euroband_tm01aaa.jpg",
                Title = "Timiș TM 01 AAA (No Euroband)",
                Description = "Classic plate format without EU blue strip",
                ExpectedPlate = "TM 01 AAA",
                CountryCode = "RO",
                CountryName = "Romania",
                HasEuroband = false
            },
            new()
            {
                Id = "temporary",
                Filename = "temporary_b123456.jpg",
                Title = "Temporary / Export Plate",
                Description = "Red temporary Bucharest plate (6 digits)",
                ExpectedPlate = "B 123456",
                CountryCode = "RO",
                CountryName = "Romania",
                HasEuroband = false
            }
        };

        foreach (var sample in list)
        {
            var filePath = Path.Combine(_samplesDirectory, sample.Filename);
            if (System.IO.File.Exists(filePath))
            {
                var bytes = System.IO.File.ReadAllBytes(filePath);
                sample.ThumbnailBase64 = $"data:image/jpeg;base64,{Convert.ToBase64String(bytes)}";
            }
        }

        return list;
    }
}
