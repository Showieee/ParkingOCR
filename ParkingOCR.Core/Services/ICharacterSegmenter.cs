using OpenCvSharp;
using ParkingOCR.Core.Models;

namespace ParkingOCR.Core.Services;

/// <summary>
/// A single segmented character candidate from a license plate crop.
/// </summary>
public record SegmentedCharacter(
    Mat Crop,
    PlateRectangle Box,
    bool IsCountryCode
) : IDisposable
{
    public void Dispose()
    {
        Crop?.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Service responsible for segmenting individual character regions from a cropped license plate image.
/// </summary>
public interface ICharacterSegmenter
{
    /// <summary>
    /// Segments character regions from a cropped plate, ordered from left to right.
    /// </summary>
    /// <param name="plateCrop">Cropped license plate image.</param>
    /// <param name="hasEuroband">Whether the plate has a detected blue Euroband on the left.</param>
    /// <param name="hasSwissShield">Whether the plate has a detected Swiss red shield on the left.</param>
    /// <returns>List of segmented character patches.</returns>
    IList<SegmentedCharacter> SegmentCharacters(Mat plateCrop, bool hasEuroband = false, bool hasSwissShield = false);
}
