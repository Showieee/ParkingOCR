using ParkingOCR.Core.Services;
using Xunit;

namespace ParkingOCR.Tests;

public class RomanianPlateValidatorTests
{
    private readonly RomanianPlateValidator _validator = new();

    [Theory]
    [InlineData("B 123 ABC", "B", "123", "ABC", "B 123 ABC")]
    [InlineData("B 12 ABC", "B", "12", "ABC", "B 12 ABC")]
    [InlineData("B-123-ABC", "B", "123", "ABC", "B 123 ABC")]
    [InlineData("B123ABC", "B", "123", "ABC", "B 123 ABC")]
    public void Validate_BucharestPlates_RecognizedAndFormatted(
        string input, string expectedCounty, string expectedNums, string expectedLetters, string expectedFormatted)
    {
        var result = _validator.ValidateAndClean(input);

        Assert.True(result.IsValid, result.ValidationMessage);
        Assert.Equal(expectedCounty, result.County);
        Assert.Equal(expectedNums, result.Numbers);
        Assert.Equal(expectedLetters, result.Letters);
        Assert.Equal(expectedFormatted, result.FormattedPlate);
    }

    [Theory]
    [InlineData("CJ 12 ABC", "CJ", "12", "ABC", "CJ 12 ABC")]
    [InlineData("CJ 123 ABC", "CJ", "123", "ABC", "CJ 123 ABC")]
    [InlineData("CJ-12-ABC", "CJ", "12", "ABC", "CJ 12 ABC")]
    [InlineData("IS 99 XYZ", "IS", "99", "XYZ", "IS 99 XYZ")]
    [InlineData("TM 777 BOS", "TM", "777", "BOS", "TM 777 BOS")]
    [InlineData("CT 01 AAA", "CT", "01", "AAA", "CT 01 AAA")]
    public void Validate_CountyPlates_RecognizedAndFormatted(
        string input, string expectedCounty, string expectedNums, string expectedLetters, string expectedFormatted)
    {
        var result = _validator.ValidateAndClean(input);

        Assert.True(result.IsValid, result.ValidationMessage);
        Assert.Equal(expectedCounty, result.County);
        Assert.Equal(expectedNums, result.Numbers);
        Assert.Equal(expectedLetters, result.Letters);
        Assert.Equal(expectedFormatted, result.FormattedPlate);
    }

    [Theory]
    [InlineData("RO B 123 ABC", "RO", "B 123 ABC")]
    [InlineData("ROB123ABC", "RO", "B 123 ABC")]
    [InlineData("RO-B-123-ABC", "RO", "B 123 ABC")]
    [InlineData("RO CJ 12 ABC", "RO", "CJ 12 ABC")]
    [InlineData("ROCJ12ABC", "RO", "CJ 12 ABC")]
    [InlineData("RO-CJ-12-ABC", "RO", "CJ 12 ABC")]
    [InlineData("EU-B-123-ABC", "EU", "B 123 ABC")]
    public void Validate_WithCountryCode_StripsCountryCodeAndExtractsPlate(
        string input, string expectedCountry, string expectedFormatted)
    {
        var result = _validator.ValidateAndClean(input);

        Assert.True(result.IsValid, result.ValidationMessage);
        Assert.Equal(expectedCountry, result.CountryCode);
        Assert.Equal(expectedFormatted, result.FormattedPlate);
    }

    [Theory]
    [InlineData("B 123456", "Temporary (Bucharest)", "B 123456")]
    [InlineData("CJ 12345", "Temporary", "CJ 12345")]
    [InlineData("CD 123 456", "Diplomatic", "CD 123 456")]
    [InlineData("MAI 12345", "Ministry of Internal Affairs", "MAI 12345")]
    public void Validate_SpecialPlates_RecognizedCorrectly(string input, string expectedType, string expectedFormatted)
    {
        var result = _validator.ValidateAndClean(input);

        Assert.True(result.IsValid, result.ValidationMessage);
        Assert.Equal(expectedType, result.PlateType);
        Assert.Equal(expectedFormatted, result.FormattedPlate);
    }

    [Fact]
    public void Validate_OcrConfusionFixes_CorrectsDigitsAndLetters()
    {
        // 'O' in number section should be corrected to '0'
        // '1' in letter section should be corrected to 'I'
        var result = _validator.ValidateAndClean("CJ 1O ABC");
        Assert.True(result.IsValid);
        Assert.Equal("10", result.Numbers);

        var result2 = _validator.ValidateAndClean("B 123 AB1");
        Assert.True(result2.IsValid);
        Assert.Equal("ABI", result2.Letters);
    }

    [Fact]
    public void Validate_EmptyOrGarbage_ReturnsInvalid()
    {
        var empty = _validator.ValidateAndClean("");
        Assert.False(empty.IsValid);

        var garbage = _validator.ValidateAndClean("!@#$%^");
        Assert.False(garbage.IsValid);
    }
}
