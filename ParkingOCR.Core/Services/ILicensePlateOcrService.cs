using OpenCvSharp;
using ParkingOCR.Core.Models;

namespace ParkingOCR.Core.Services;

/// <summary>
/// High-level engine interface for offline License Plate Recognition.
/// Completely decoupled and suitable for console, desktop, background worker, or web APIs.
/// </summary>
public interface ILicensePlateOcrService
{
    /// <summary>
    /// Recognizes a license plate from an in-memory image byte array.
    /// </summary>
    Task<LicensePlateResult> RecognizePlateAsync(byte[] imageBytes, OcrOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recognizes a license plate from an image Stream.
    /// </summary>
    Task<LicensePlateResult> RecognizePlateAsync(Stream imageStream, OcrOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recognizes a license plate from an image file path.
    /// </summary>
    Task<LicensePlateResult> RecognizePlateAsync(string imagePath, OcrOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronously recognizes a license plate from an in-memory image byte array.
    /// </summary>
    LicensePlateResult RecognizePlate(byte[] imageBytes, OcrOptions? options = null);

    /// <summary>
    /// Synchronously recognizes a license plate from an OpenCvSharp Mat.
    /// </summary>
    LicensePlateResult RecognizePlate(Mat image, OcrOptions? options = null);
}
