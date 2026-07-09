using KycLite.Api.Validation.FieldRules;

namespace KycLite.Api.Tests.Validation;

/// <summary>
/// Covers the date field-rules against a fixed reference date so age/expiry-style assertions are
/// deterministic. These rules replace the old AgeRule/ExpiryRule via relative parameters
/// ("today-18y", "today").
/// </summary>
public class DateRuleTests
{
    private static readonly DateOnly Today = new(2025, 1, 1);

    // --- DateOnOrAfterCheck (≥) — replicates "not expired" with param "today" ---

    [Theory]
    [InlineData("2030-01-01", true)]
    [InlineData("2025-01-01", true)]  // boundary is inclusive
    [InlineData("2024-12-31", false)]
    public void Validate_DateOnOrAfter_PassesWhenValueOnOrAfterReference(string value, bool expected)
    {
        // Act
        var result = new DateOnOrAfterCheck().Validate(value, "today", Today);

        // Assert
        Assert.Equal(expected, result.Passed);
    }

    [Fact]
    public void Validate_DateOnOrAfterWithAbsoluteReference_Passes()
    {
        // Act
        var result = new DateOnOrAfterCheck().Validate("2030-01-01", "2029-12-31", Today);

        // Assert
        Assert.True(result.Passed);
    }

    // --- DateOnOrBeforeCheck (≤) — replicates "age ≥ 18" with param "today-18y" ---

    [Theory]
    [InlineData("2007-01-01", true)]   // turns 18 exactly on the reference date
    [InlineData("2007-01-02", false)]  // 18th birthday one day after the reference date
    public void Validate_DateOnOrBeforeWithYearOffset_PassesWhenValueOnOrBefore(string value, bool expected)
    {
        // Act
        var result = new DateOnOrBeforeCheck().Validate(value, "today-18y", Today);

        // Assert
        Assert.Equal(expected, result.Passed);
    }

    [Theory]
    [InlineData("today+30d", "2025-01-31", true)]
    [InlineData("today-6m", "2024-07-01", true)]
    public void Validate_RelativeMonthAndDayOffsets_ResolveAgainstReference(string param, string value, bool expected)
    {
        // Act
        var result = new DateOnOrAfterCheck().Validate(value, param, Today);

        // Assert
        Assert.Equal(expected, result.Passed);
    }

    // --- Failure modes ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-date")]
    public void Validate_UnreadableDateValue_Fails(string? value)
    {
        // Act
        var result = new DateOnOrAfterCheck().Validate(value, "today", Today);

        // Assert
        Assert.False(result.Passed);
    }

    [Fact]
    public void Validate_InvalidReferenceParam_Fails()
    {
        // Act
        var result = new DateOnOrBeforeCheck().Validate("2020-01-01", "garbage", Today);

        // Assert
        Assert.False(result.Passed);
    }

    [Theory]
    [InlineData("today+9999y")]        // overflows DateOnly's year-9999 ceiling (AddYears)
    [InlineData("today+99999999999d")] // overflows int (amount parse)
    public void Validate_OutOfRangeRelativeOffset_FailsGracefullyWithoutThrowing(string param)
    {
        // Act — a crafted offset must yield a normal rule failure, never an unhandled exception (500).
        var result = new DateOnOrAfterCheck().Validate("2020-01-01", param, Today);

        // Assert
        Assert.False(result.Passed);
    }
}
