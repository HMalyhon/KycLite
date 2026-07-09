using KycLite.Api.Validation;

namespace KycLite.Api.Tests.Validation;

public class Mrz731Tests
{
    [Theory]
    [InlineData("L898902C", 3)]   // weighted sum 313 -> 3
    [InlineData("123456789", 7)]
    public void ComputeCheckDigit_KnownInput_MatchesExpectedDigit(string input, int expected)
    {
        // Act
        var checkDigit = Mrz731.ComputeCheckDigit(input);

        // Assert
        Assert.Equal(expected, checkDigit);
    }

    [Fact]
    public void ValidateWithTrailingCheckDigit_SelfConsistentNumber_ReturnsTrue()
    {
        // Arrange
        const string baseNumber = "L898902C";
        var number = baseNumber + Mrz731.ComputeCheckDigit(baseNumber);

        // Act
        var valid = Mrz731.ValidateWithTrailingCheckDigit(number);

        // Assert
        Assert.True(valid);
    }

    [Fact]
    public void ValidateWithTrailingCheckDigit_WrongCheckDigit_ReturnsFalse()
    {
        // Act
        var valid = Mrz731.ValidateWithTrailingCheckDigit("L898902C4");

        // Assert
        Assert.False(valid);
    }

    [Theory]
    [InlineData("")]            // empty
    [InlineData("X")]           // too short
    [InlineData("L898902CC")]   // trailing char not a digit
    public void ValidateWithTrailingCheckDigit_MalformedInput_ReturnsFalse(string number)
    {
        // Act
        var valid = Mrz731.ValidateWithTrailingCheckDigit(number);

        // Assert
        Assert.False(valid);
    }

    [Fact]
    public void ValidateWithTrailingCheckDigit_SeparatorsAndCase_AreIgnored()
    {
        // Arrange — "l898902c" lowercased + the correct check digit, with spaces/dashes interspersed.
        const string number = "l-898 902c3";

        // Act
        var valid = Mrz731.ValidateWithTrailingCheckDigit(number);

        // Assert
        Assert.True(valid);
    }
}
