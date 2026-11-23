using ParkingOCR.Core.Services;
using Xunit;

namespace ParkingOCR.Tests;

public class EuropeanPlateValidatorTests
{
    private readonly EuropeanPlateValidator _validator = new();

    [Theory]
    [InlineData("D-B-MW-1234", "D", "B MW 1234", "German")]
    [InlineData("D-M-AB-123", "D", "M AB 123", "German")]
    [InlineData("D HH XY 999", "D", "HH XY 999", "German")]
    public void Validate_GermanPlates_RecognizedAndFormatted(
        string input, string expectedCountry, string expectedFormatted, string expectedType)
    {
        var result = _validator.ValidateAndClean(input);

        Assert.True(result.IsValid, result.ValidationMessage);
        Assert.Equal(expectedCountry, result.CountryCode);
        Assert.Equal("Germany", result.CountryName);
        Assert.Equal(expectedFormatted, result.FormattedPlate);
        Assert.Equal(expectedType, result.PlateType);
    }

    [Theory]
    [InlineData("ZH 84521", "ZH", "84521", "ZH 84521")]
    [InlineData("GE 98765", "GE", "98765", "GE 98765")]
    [InlineData("BE 12345", "BE", "12345", "BE 12345")]
    [InlineData("VD 1234", "VD", "1234", "VD 1234")]
    [InlineData("TI 55555", "TI", "55555", "TI 55555")]
    public void Validate_SwissPlates_NoEuroband_RecognizedAsSwiss(
        string input, string expectedCanton, string expectedNumbers, string expectedFormatted)
    {
        var result = _validator.ValidateAndClean(input, "CH");

        Assert.True(result.IsValid, result.ValidationMessage);
        Assert.Equal("CH", result.CountryCode);
        Assert.Equal("Switzerland", result.CountryName);
        Assert.Equal(expectedCanton, result.County);
        Assert.Equal(expectedNumbers, result.Numbers);
        Assert.Equal(expectedFormatted, result.FormattedPlate);
        Assert.Equal("Swiss", result.PlateType);
    }

    [Theory]
    [InlineData("F-AA-123-AA", "F", "AA-123-AA")]
    [InlineData("F-AB-123-CD", "F", "AB-123-CD")]
    public void Validate_FrenchPlates_RecognizedAndFormatted(string input, string expectedCountry, string expectedFormatted)
    {
        var result = _validator.ValidateAndClean(input);

        Assert.True(result.IsValid, result.ValidationMessage);
        Assert.Equal(expectedCountry, result.CountryCode);
        Assert.Equal("France", result.CountryName);
        Assert.Equal(expectedFormatted, result.FormattedPlate);
        Assert.Equal("French", result.PlateType);
    }

    [Theory]
    [InlineData("I-AB-123-CD", "I", "AB 123 CD")]
    [InlineData("I-EF-456-GH", "I", "EF 456 GH")]
    public void Validate_ItalianPlates_RecognizedAndFormatted(string input, string expectedCountry, string expectedFormatted)
    {
        var result = _validator.ValidateAndClean(input);

        Assert.True(result.IsValid, result.ValidationMessage);
        Assert.Equal(expectedCountry, result.CountryCode);
        Assert.Equal("Italy", result.CountryName);
        Assert.Equal(expectedFormatted, result.FormattedPlate);
        Assert.Equal("Italian", result.PlateType);
    }

    [Theory]
    [InlineData("E-1234-BCD", "E", "1234 BCD")]
    [InlineData("E 5678 FGH", "E", "5678 FGH")]
    public void Validate_SpanishPlates_RecognizedAndFormatted(string input, string expectedCountry, string expectedFormatted)
    {
        var result = _validator.ValidateAndClean(input);

        Assert.True(result.IsValid, result.ValidationMessage);
        Assert.Equal(expectedCountry, result.CountryCode);
        Assert.Equal("Spain", result.CountryName);
        Assert.Equal(expectedFormatted, result.FormattedPlate);
        Assert.Equal("Spanish", result.PlateType);
    }

    [Theory]
    [InlineData("UK-BD12-XYZ", "UK", "BD12 XYZ")]
    [InlineData("GB-LN14-ABC", "GB", "LN14 ABC")]
    public void Validate_UnitedKingdomPlates_RecognizedAndFormatted(string input, string expectedCountry, string expectedFormatted)
    {
        var result = _validator.ValidateAndClean(input);

        Assert.True(result.IsValid, result.ValidationMessage);
        Assert.Equal(expectedCountry, result.CountryCode);
        Assert.Equal("United Kingdom", result.CountryName);
        Assert.Equal(expectedFormatted, result.FormattedPlate);
        Assert.Equal("United Kingdom", result.PlateType);
    }

    [Theory]
    [InlineData("PL-WA-12345", "PL", "WA 12345")]
    [InlineData("PL-KR-123AB", "PL", "KR 123AB")]
    public void Validate_PolishPlates_RecognizedAndFormatted(string input, string expectedCountry, string expectedFormatted)
    {
        var result = _validator.ValidateAndClean(input);

        Assert.True(result.IsValid, result.ValidationMessage);
        Assert.Equal(expectedCountry, result.CountryCode);
        Assert.Equal("Poland", result.CountryName);
        Assert.Equal(expectedFormatted, result.FormattedPlate);
        Assert.Equal("Polish", result.PlateType);
    }

    [Fact]
    public void Validate_DutchPlates_RecognizedAndFormatted()
    {
        var result = _validator.ValidateAndClean("12-ABC-3", "NL");

        Assert.True(result.IsValid, result.ValidationMessage);
        Assert.Equal("NL", result.CountryCode);
        Assert.Equal("Netherlands", result.CountryName);
        Assert.Equal("Dutch", result.PlateType);
    }

    [Theory]
    [InlineData("RO-B-123-ABC", "RO", "B 123 ABC")]
    [InlineData("D-B-MW-1234", "D", "B MW 1234")]
    [InlineData("PL-WA-12345", "PL", "WA 12345")]
    [InlineData("F-AA-123-AA", "F", "AA-123-AA")]
    public void StripCountryPrefix_StripsCorrectPrefix(string input, string expectedCountry, string expectedPlate)
    {
        string stripped = _validator.StripCountryPrefix(input, out string? country);

        Assert.Equal(expectedCountry, country);
        var validation = _validator.ValidateAndClean(stripped, country);
        Assert.Equal(expectedPlate, validation.FormattedPlate);
    }

    [Fact]
    public void FormatNaturalPlateSpacing_SeparatesLettersAndDigits()
    {
        string formatted = _validator.FormatNaturalPlateSpacing("ABC123DEF");
        Assert.Equal("ABC 123 DEF", formatted);
    }

    [Theory]
    [InlineData(":IF 01 TMX.", "IF 01 TMX")]
    [InlineData("-LU-068SX-", "LU-068SX")]
    [InlineData("„2TS4ME...", "2TS4ME")]
    [InlineData(".ZH 84521.", "ZH 84521")]
    [InlineData("---B 111 YUL---", "B 111 YUL")]
    [InlineData("!@#B 208 GAB$%^", "B 208 GAB")]
    public void TrimNonAlphanumeric_RemovesLeadingAndTrailingNoiseCharacters(string input, string expected)
    {
        string trimmed = EuropeanPlateValidator.TrimNonAlphanumeric(input);
        Assert.Equal(expected, trimmed);
    }

    [Theory]
    [InlineData(":IF 01 TMX.", "RO", "IF 01 TMX")]
    [InlineData(".LU 068SX.", null, "LU 068 SX")]
    [InlineData("„2TS4ME", null, "2 TS 4 ME")]
    public void ValidateAndClean_TrimsLeadingAndTrailingNonAlphanumerics(string input, string? country, string expectedFormatted)
    {
        var result = _validator.ValidateAndClean(input, country);
        Assert.Equal(expectedFormatted, result.FormattedPlate);
    }
}
