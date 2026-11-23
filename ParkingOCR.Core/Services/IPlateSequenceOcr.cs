using OpenCvSharp;
using ParkingOCR.Core.Models;

namespace ParkingOCR.Core.Services;

/// <summary>
/// Result of deep-learning sequence OCR on a cropped plate.
/// </summary>
public record SequenceOcrResult(
    string RawText,
    float Confidence,
    List<CharacterBox> Characters
);

/// <summary>
/// Interface for deep-learning plate text recognition.
/// </summary>
public interface IPlateSequenceOcr
{
    /// <summary>
    /// Recognizes the alphanumeric sequence from a cropped license plate image.
    /// </summary>
    SequenceOcrResult Recognize(Mat plateCrop);
}
