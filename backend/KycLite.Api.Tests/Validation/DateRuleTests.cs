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

    // --- DateOnOrAfterRule (≥) — replicates "not expired" with param "today" ---

    [Theory]
    [InlineData("2030-01-01", true)]
    [InlineData("2025-01-01", true)]  // boundary is inclusive
    [InlineData("2024-12-31", false)]
    public void Validate_DateOnOrAfter_PassesWhenValueOnOrAfterReference(string value, bool expected)
    {
        // Act
        var result = new DateOnOrAfterRule().Validate(value, "today", Today);

        // Assert
        Assert.Equal(expected, result.Passed);
    }

    [Fact]
    public void Validate_DateOnOrAfterWithAbsoluteReference_Passes()
    {
        // Act
        var result = new DateOnOrAfterRule().Validate("2030-01-01", "2029-12-31", Today);

        // Assert
        Assert.True(result.Passed);
    }

    // --- DateOnOrBeforeRule (≤) — replicates "age ≥ 18" with param "today-18y" ---

    [Theory]
    [InlineData("2007-01-01", true)]   // turns 18 exactly on the reference date
    [InlineData("2007-01-02", false)]  // 18th birthday one day after the reference date
    public void Validate_DateOnOrBeforeWithYearOffset_PassesWhenValueOnOrBefore(string value, bool expected)
    {
        // Act
        var result = new DateOnOrBeforeRule().Validate(value, "today-18y", Today);

        // Assert
        Assert.Equal(expected, result.Passed);
    }

    [Theory]
    [InlineData("today+30d", "2025-01-31", true)]
    [InlineData("today-6m", "2024-07-01", true)]
    public void Validate_RelativeMonthAndDayOffsets_ResolveAgainstReference(string param, string value, bool expected)
    {
        // Act
        var result = new DateOnOrAfterRule().Validate(value, param, Today);

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
        var result = new DateOnOrAfterRule().Validate(value, "today", Today);

        // Assert — the document is what's under test, so an unreadable value is a real failure
        // that counts toward the verdict. Contrast the param cases below.
        Assert.False(result.Passed);
        Assert.True(result.Evaluated);
    }

    [Fact]
    public void Validate_InvalidReferenceParam_CannotEvaluate()
    {
        // Act
        var result = new DateOnOrBeforeRule().Validate("2020-01-01", "garbage", Today);

        // Assert — the question is malformed, not the document: no verdict, so the runner ignores it.
        Assert.False(result.Evaluated);
        Assert.Contains("garbage", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_InvalidReferenceParamWithUnreadableValue_StillCannotEvaluate()
    {
        // Act — both halves are bad; the param is resolved first, so this can't masquerade as a
        // value failure and slip into the verdict.
        var result = new DateOnOrAfterRule().Validate("not-a-date", "garbage", Today);

        // Assert
        Assert.False(result.Evaluated);
    }

    [Theory]
    [InlineData("today+9999y")]        // overflows DateOnly's year-9999 ceiling (AddYears)
    [InlineData("today+99999999999d")] // overflows int (amount parse)
    public void Validate_OutOfRangeRelativeOffset_CannotEvaluateWithoutThrowing(string param)
    {
        // Act — a crafted offset must resolve to "unanswerable", never an unhandled exception (500).
        var result = new DateOnOrAfterRule().Validate("2020-01-01", param, Today);

        // Assert
        Assert.False(result.Evaluated);
    }
}
