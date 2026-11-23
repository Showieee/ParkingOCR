using OpenCvSharp;

namespace ParkingOCR.Core.Services;

/// <summary>
/// Service responsible for classifying a segmented character image into an alphanumeric character (0-9, A-Z).
/// </summary>
public interface ICharacterClassifier : IDisposable
{
    /// <summary>
    /// Recognizes a single character image crop.
    /// </summary>
    /// <param name="charMat">Grayscale or binary crop of the character.</param>
    /// <returns>Predicted character and confidence score (0.0 to 1.0).</returns>
    (char Character, float Confidence) Classify(Mat charMat);

    /// <summary>
    /// Recognizes multiple character image crops.
    /// </summary>
    IList<(char Character, float Confidence)> ClassifyBatch(IList<Mat> charMats);

    /// <summary>
    /// Returns raw classification probabilities across all 36 classes (0-9, A-Z).
    /// </summary>
    float[] GetClassProbabilities(Mat charMat);
}
