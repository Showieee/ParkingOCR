using OpenCvSharp;
using ParkingOCR.Core.Services;
using Xunit;

namespace ParkingOCR.Tests;

public class OnnxCharacterClassifierTests
{
    [Fact]
    public void Constructor_LoadsEmbeddedModel_Successfully()
    {
        using var classifier = new OnnxCharacterClassifier();
        Assert.NotNull(classifier);
    }

    [Theory]
    [InlineData('A')]
    [InlineData('B')]
    [InlineData('C')]
    [InlineData('1')]
    [InlineData('2')]
    public void Classify_RenderedCharacters_PredictsCorrectly(char expectedChar)
    {
        using var classifier = new OnnxCharacterClassifier();

        using Mat charMat = new(new Size(50, 70), MatType.CV_8UC1, Scalar.White);
        Cv2.PutText(charMat, expectedChar.ToString(), new Point(8, 55),
            HersheyFonts.HersheySimplex, 1.8, Scalar.Black, 3, LineTypes.AntiAlias);

        var (pred, conf) = classifier.Classify(charMat);

        Assert.Equal(expectedChar, pred);
        Assert.True(conf > 0.4f, $"Expected confidence > 0.4 for '{expectedChar}', but got {conf}");
    }
}
