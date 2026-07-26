using KycLite.Api.Validation;

namespace KycLite.Api.Tests.Validation;

/// <summary>
/// Covers the MRZ check-digit validator against the canonical ICAO 9303 specimens (which are
/// internally consistent with the 7-3-1 algorithm) for both the passport (TD3) and ID-card (TD1)
/// layouts, plus tampering, malformed input, and whitespace/case normalization.
/// </summary>
public class MrzTests
{
    // Canonical ICAO 9303 TD3 (passport) specimen.
    private const string Td3Line1 = "P<UTOERIKSSON<<ANNA<MARIA<<<<<<<<<<<<<<<<<<<";
    private const string Td3Line2 = "L898902C36UTO7408122F1204159ZE184226B<<<<<10";
    private const string Td3 = Td3Line1 + "\n" + Td3Line2;

    // Canonical ICAO 9303 TD1 (ID card) specimen.
    private const string Td1 =
        "I<UTOD231458907<<<<<<<<<<<<<<<\n" +
        "7408122F1204159UTO<<<<<<<<<<<6\n" +
        "ERIKSSON<<ANNA<MARIA<<<<<<<<<<";

    [Fact]
    public void Validate_ValidTd3Passport_IsValid()
    {
        // Act
        var result = Mrz.Validate(Td3);

        // Assert — message included so a broken composite range is legible on failure.
        Assert.True(result.Valid, result.Message);
    }

    [Fact]
    public void Validate_ValidTd1IdCard_IsValid()
    {
        // Act
        var result = Mrz.Validate(Td1);

        // Assert
        Assert.True(result.Valid, result.Message);
    }

    [Fact]
    public void Validate_TamperedDocumentNumberCheckDigit_Fails()
    {
        // Arrange — flip line 2's document-number check digit (6 -> 5).
        var tampered = Td3Line1 + "\n" + "L898902C35UTO7408122F1204159ZE184226B<<<<<10";

        // Act
        var result = Mrz.Validate(tampered);

        // Assert
        Assert.False(result.Valid);
    }

    [Fact]
    public void Validate_TamperedCompositeCheckDigit_Fails()
    {
        // Arrange — flip only the final (composite) check digit (0 -> 1).
        var tampered = Td3Line1 + "\n" + "L898902C36UTO7408122F1204159ZE184226B<<<<<11";

        // Act
        var result = Mrz.Validate(tampered);

        // Assert
        Assert.False(result.Valid);
        Assert.Contains("composite", result.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("L898902C3")]        // far too short to be an MRZ
    [InlineData("PLACEHOLDER!!!")]   // contains characters outside the MRZ alphabet
    public void Validate_MissingOrMalformed_Fails(string? raw)
    {
        // Act & Assert
        Assert.False(Mrz.Validate(raw).Valid);
    }

    [Fact]
    public void Validate_IgnoresWhitespaceAndCase_WhenReassemblingLines()
    {
        // Arrange — lower-cased, with the line break replaced by spaces; normalization must recover it.
        var messy = (Td3Line1 + "   " + Td3Line2).ToLowerInvariant();

        // Act
        var result = Mrz.Validate(messy);

        // Assert
        Assert.True(result.Valid, result.Message);
    }
}
