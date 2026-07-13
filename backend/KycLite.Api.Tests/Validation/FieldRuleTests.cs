using KycLite.Api.Catalog;
using KycLite.Api.Models;
using KycLite.Api.Validation;
using KycLite.Api.Validation.FieldRules;

namespace KycLite.Api.Tests.Validation;

public class FieldRuleTests
{
    // Reference date for the (date-agnostic) rules below; only the date rules actually read it.
    private static readonly DateOnly Today = new(2025, 1, 1);

    // --- RequiredCheck ---

    [Theory]
    [InlineData("Erika", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void Validate_RequiredCheck_PassesOnlyWhenValuePresent(string? value, bool expected)
    {
        // Act
        var result = new RequiredCheck().Validate(value, null, Today);

        // Assert
        Assert.Equal(expected, result.Passed);
    }

    // --- PatternCheck ---

    [Fact]
    public void Validate_PatternWithMatchingValue_Passes()
    {
        // Act
        var result = new PatternCheck().Validate("L898902C3", "^[A-Z0-9]+$", Today);

        // Assert
        Assert.True(result.Passed);
    }

    [Fact]
    public void Validate_PatternWithNonMatchingValue_Fails()
    {
        // Act
        var result = new PatternCheck().Validate("lower case", "^[A-Z0-9]+$", Today);

        // Assert
        Assert.False(result.Passed);
    }

    [Fact]
    public void Validate_PatternWithoutParam_Fails()
    {
        // Act
        var result = new PatternCheck().Validate("anything", null, Today);

        // Assert
        Assert.False(result.Passed);
    }

    [Fact]
    public void Validate_PatternWithInvalidRegex_FailsGracefully()
    {
        // Act
        var result = new PatternCheck().Validate("anything", "([unclosed", Today);

        // Assert
        Assert.False(result.Passed);
        Assert.Contains("Invalid pattern", result.Message);
    }

    // --- MinLengthCheck ---

    [Theory]
    [InlineData("Erika", "2", true)]
    [InlineData("E", "2", false)]
    [InlineData("  E  ", "2", false)] // trimmed length is 1
    [InlineData("anything", "abc", false)] // non-numeric param
    public void Validate_MinLengthCheck_PassesOnlyWhenAtLeastMinChars(string value, string param, bool expected)
    {
        // Act
        var result = new MinLengthCheck().Validate(value, param, Today);

        // Assert
        Assert.Equal(expected, result.Passed);
    }

    // --- ChecksumCheck (ICAO 9303 7-3-1 trailing check digit) ---

    [Theory]
    [InlineData("L898902C3", true)]  // valid trailing check digit
    [InlineData("L898902C4", false)] // wrong check digit
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Validate_ChecksumCheck_PassesOnlyForValidTrailingDigit(string? value, bool expected)
    {
        // Act
        var result = new ChecksumCheck().Validate(value, null, Today);

        // Assert
        Assert.Equal(expected, result.Passed);
    }

    // --- FieldCheckRunner ---

    private static FieldCheckRunner BuildRunner() =>
        new(new IFieldRule[] { new RequiredCheck(), new PatternCheck(), new MinLengthCheck() });

    [Fact]
    public void Run_ValidCheck_LabelsResultWithFieldAndRule()
    {
        // Arrange
        var runner = BuildRunner();
        var doc = Doc.With((FieldKeys.Address, "123 Example Street"));
        var checks = new[] { new FieldCheck(FieldKeys.Address, "required", null) };

        // Act
        var results = runner.Run(checks, doc, Today).Evaluated;

        // Assert
        var result = Assert.Single(results);
        Assert.True(result.Passed);
        Assert.Equal("address:required", result.RuleKey);
        Assert.Equal("Address · Required", result.RuleLabel);
    }

    [Fact]
    public void Run_CheckWithCustomName_UsesNameAsLabel()
    {
        // Arrange
        var runner = BuildRunner();
        var doc = Doc.With((FieldKeys.Address, "123 Example Street"));
        var checks = new[] { new FieldCheck(FieldKeys.Address, "required", null, "Address present") };

        // Act
        var result = Assert.Single(runner.Run(checks, doc, Today).Evaluated);

        // Assert
        Assert.Equal("Address present", result.RuleLabel);
    }

    [Fact]
    public void Run_UnknownRuleOrBlankField_SkipsButRecordsAsIgnored()
    {
        // Arrange
        var runner = BuildRunner();
        var checks = new[]
        {
            new FieldCheck(FieldKeys.Address, "does-not-exist", null),
            new FieldCheck(string.Empty, "required", null),
        };

        // Act
        var run = runner.Run(checks, Doc.Valid(), Today);

        // Assert — neither counts toward the verdict, but both are surfaced as ignored.
        Assert.Empty(run.Evaluated);
        Assert.Equal(2, run.Ignored.Count);
    }

    [Fact]
    public void Run_UnknownField_SkipsButRecordsAsIgnored()
    {
        // Arrange — an unknown field key should be ignored, not validated into a spurious failure.
        var runner = BuildRunner();
        var checks = new[] { new FieldCheck("not-a-real-field", "required", null) };

        // Act
        var run = runner.Run(checks, Doc.Valid(), Today);

        // Assert
        Assert.Empty(run.Evaluated);
        Assert.Equal("not-a-real-field", Assert.Single(run.Ignored).Field);
    }

    [Fact]
    public void Run_RuleFieldTypeMismatch_SkipsButRecordsAsIgnored()
    {
        // Arrange — pattern applies to Text fields only; dateOfBirth is a Date field.
        var runner = BuildRunner();
        var checks = new[] { new FieldCheck(FieldKeys.DateOfBirth, "pattern", "^.+$") };

        // Act
        var run = runner.Run(checks, Doc.Valid(), Today);

        // Assert
        Assert.Empty(run.Evaluated);
        Assert.Contains("does not apply", Assert.Single(run.Ignored).Reason);
    }

    [Fact]
    public void Run_RequiredOnMissingFieldValue_Fails()
    {
        // Arrange — Address is absent from Doc.Valid(); Required should fail.
        var runner = BuildRunner();
        var checks = new[] { new FieldCheck(FieldKeys.Address, "required", null) };

        // Act
        var results = runner.Run(checks, Doc.Valid(), Today).Evaluated;

        // Assert
        Assert.False(Assert.Single(results).Passed);
    }

    [Fact]
    public void AvailableRules_WhenQueried_ExposesEveryRegisteredRule()
    {
        // Arrange
        var runner = BuildRunner();

        // Act
        var available = runner.AvailableRules;

        // Assert
        Assert.Equal(3, available.Count);
    }
}
