using KycLite.Api.Validation;

namespace KycLite.Api.Tests.Validation;

public class Mrz731Tests
{
    [Theory]
    [InlineData("L898902C", 3)]   // weighted sum 313 -> 3
    [InlineData("123456789", 7)]
    [InlineData("740812", 2)]     // ICAO specimen date-of-birth check digit
    public void ComputeCheckDigit_KnownInput_MatchesExpectedDigit(string input, int expected)
    {
        // Act
        var checkDigit = Mrz731.ComputeCheckDigit(input);

        // Assert
        Assert.Equal(expected, checkDigit);
    }
}
