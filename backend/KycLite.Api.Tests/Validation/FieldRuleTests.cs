using KycLite.Api.Catalog;
using KycLite.Api.Models;
using KycLite.Api.Validation;
using KycLite.Api.Validation.FieldRules;

namespace KycLite.Api.Tests.Validation;

public class FieldRuleTests
{
    // Reference date for the (date-agnostic) rules below; only the date rules actually read it.
    private static readonly DateOnly Today = new(2025, 1, 1);

    // --- RequiredRule ---

    [Theory]
    [InlineData("Erika", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void Validate_RequiredRule_PassesOnlyWhenValuePresent(string? value, bool expected)
    {
        // Act
        var result = new RequiredRule().Validate(value, null, Today);

        // Assert
        Assert.Equal(expected, result.Passed);
    }

    // --- PatternRule ---

    [Fact]
    public void Validate_PatternWithMatchingValue_Passes()
    {
        // Act
        var result = new PatternRule().Validate("L898902C3", "^[A-Z0-9]+$", Today);

        // Assert
        Assert.True(result.Passed);
    }

    [Fact]
    public void Validate_PatternWithNonMatchingValue_Fails()
    {
        // Act
        var result = new PatternRule().Validate("lower case", "^[A-Z0-9]+$", Today);

        // Assert
        Assert.False(result.Passed);
    }

    [Fact]
    public void Validate_PatternWithMissingValue_Fails()
    {
        // Act
        var result = new PatternRule().Validate(null, "^[A-Z0-9]+$", Today);

        // Assert — a missing value is the document's problem, so it counts toward the verdict.
        Assert.False(result.Passed);
        Assert.True(result.Evaluated);
    }

    [Fact]
    public void Validate_PatternWithoutParam_CannotEvaluate()
    {
        // Act
        var result = new PatternRule().Validate("anything", null, Today);

        // Assert
        Assert.False(result.Evaluated);
    }

    [Fact]
    public void Validate_PatternWithInvalidRegex_CannotEvaluate()
    {
        // Act
        var result = new PatternRule().Validate("anything", "([unclosed", Today);

        // Assert — an uncompilable pattern is an unanswerable question, not a failed document.
        Assert.False(result.Evaluated);
        Assert.Contains("Invalid pattern", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_PatternWithCatastrophicBacktracking_TimesOutGracefully()
    {
        // Arrange — a classic ReDoS pattern against non-matching input backtracks exponentially.
        // The rule's match timeout must turn that into an unevaluated result, never a hung request.
        var value = new string('a', 40) + "!";

        // Act
        var result = new PatternRule().Validate(value, "^(a+)+$", Today);

        // Assert — we never got an answer out of the pattern, so the document can't be judged on it.
        Assert.False(result.Evaluated);
        Assert.Contains("too long", result.Message, StringComparison.Ordinal);
    }

    // --- MinLengthRule ---

    [Theory]
    [InlineData("Erika", "2", true)]
    [InlineData("E", "2", false)]
    [InlineData("  E  ", "2", false)] // trimmed length is 1
    public void Validate_MinLengthRule_PassesOnlyWhenAtLeastMinChars(string value, string param, bool expected)
    {
        // Act
        var result = new MinLengthRule().Validate(value, param, Today);

        // Assert
        Assert.Equal(expected, result.Passed);
        Assert.True(result.Evaluated);
    }

    [Theory]
    [InlineData("abc")] // not a number at all
    [InlineData("-1")]  // a length no value can satisfy
    [InlineData(null)]  // the rule requires a param
    public void Validate_MinLengthWithUnparseableParam_CannotEvaluate(string? param)
    {
        // Act
        var result = new MinLengthRule().Validate("anything", param, Today);

        // Assert
        Assert.False(result.Evaluated);
    }

    // --- ChecksumRule (ICAO 9303 MRZ check digits) ---

    [Fact]
    public void Validate_ChecksumRule_PassesForValidMrz()
    {
        // Arrange — canonical ICAO 9303 TD3 specimen (consistent with the 7-3-1 algorithm).
        const string validMrz =
            "P<UTOERIKSSON<<ANNA<MARIA<<<<<<<<<<<<<<<<<<<\nL898902C36UTO7408122F1204159ZE184226B<<<<<10";

        // Act
        var result = new ChecksumRule().Validate(validMrz, null, Today);

        // Assert
        Assert.True(result.Passed, result.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("L898902C3")] // a bare document number is not an MRZ
    public void Validate_ChecksumRule_FailsForMissingOrNonMrz(string? value)
    {
        // Act
        var result = new ChecksumRule().Validate(value, null, Today);

        // Assert
        Assert.False(result.Passed);
    }

    // --- FieldCheckRunner ---

    private static FieldCheckRunner BuildRunner() =>
        new(new IFieldRule[] { new RequiredRule(), new PatternRule(), new MinLengthRule() });

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
    public void Run_CheckWithUninterpretableParam_SkipsButRecordsAsIgnored()
    {
        // Arrange — the field and rule both resolve; only the param is nonsense. Previously each of
        // these counted as a failed rule, so a malformed request could force a rejection the
        // document never earned.
        var runner = BuildRunner();
        var doc = Doc.With((FieldKeys.FirstName, "Erika"));
        var checks = new[]
        {
            new FieldCheck(FieldKeys.FirstName, "minLength", "NaN"),
            new FieldCheck(FieldKeys.FirstName, "pattern", "([unclosed"),
        };

        // Act
        var run = runner.Run(checks, doc, Today);

        // Assert — nothing reaches the verdict, and both are reported with the rule's own reason.
        Assert.Empty(run.Evaluated);
        Assert.Equal(2, run.Ignored.Count);
        Assert.All(run.Ignored, i => Assert.Equal(FieldKeys.FirstName, i.Field));
        Assert.Contains(run.Ignored, i => i.Reason.Contains("minimum length", StringComparison.Ordinal));
        Assert.Contains(run.Ignored, i => i.Reason.Contains("Invalid pattern", StringComparison.Ordinal));
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
    public void Run_NullCheckElement_SkipsButRecordsAsIgnored()
    {
        // Arrange — a JSON body of fieldChecks=[null] deserializes to a null element; the runner
        // must record it, not throw a NullReferenceException (which surfaced as a 500).
        var runner = BuildRunner();
        var checks = new FieldCheck[] { null! };

        // Act
        var run = runner.Run(checks, Doc.Valid(), Today);

        // Assert
        Assert.Empty(run.Evaluated);
        Assert.Single(run.Ignored);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Run_NullOrBlankRule_SkipsButRecordsAsIgnored(string? rule)
    {
        // Arrange — a check with no rule (fieldChecks=[{"field":"firstName"}]) must be ignored,
        // not crash the runner on a null dictionary key (which surfaced as a 500).
        var runner = BuildRunner();
        var checks = new[] { new FieldCheck(FieldKeys.FirstName, rule!, null) };

        // Act
        var run = runner.Run(checks, Doc.Valid(), Today);

        // Assert
        Assert.Empty(run.Evaluated);
        Assert.Equal(FieldKeys.FirstName, Assert.Single(run.Ignored).Field);
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
