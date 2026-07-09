using KycLite.Api.Catalog;
using KycLite.Api.Models;
using KycLite.Api.Services;
using KycLite.Api.Validation;
using KycLite.Api.Validation.FieldRules;

namespace KycLite.Api.Tests.Services;

public class VerificationServiceTests
{
    private static readonly string[] AllFields = ["*"];
    private static readonly FieldCheck[] NoChecks = [];

    // A fixed reference date so date-driven rules evaluate deterministically.
    private static readonly DateOnly Today = new(2025, 1, 1);

    private static VerificationService BuildService(ExtractionResult doc, string mode = "fake")
    {
        var runner = new FieldCheckRunner(
        [
            new RequiredCheck(), new PatternCheck(), new MinLengthCheck(), new ChecksumCheck(),
            new DateOnOrAfterCheck(), new DateOnOrBeforeCheck(),
        ]);
        return new VerificationService(new FakeDocumentExtractor(doc, mode), runner, new FixedTimeProvider(Today));
    }

    private static Task<VerifyResponse> Verify(
        VerificationService svc,
        IEnumerable<string> fields,
        IEnumerable<FieldCheck>? checks = null)
        => svc.VerifyAsync(Stream.Null, "image/png", fields, checks ?? NoChecks, CancellationToken.None);

    [Fact]
    public async Task VerifyAsync_ValidDocumentWithPassingChecks_Approves()
    {
        // Arrange
        var svc = BuildService(Doc.Valid());
        var checks = new[]
        {
            new FieldCheck(FieldKeys.FirstName, "required", null),
            new FieldCheck(FieldKeys.DocumentNumber, "checksum", null),
        };

        // Act
        var response = await Verify(svc, AllFields, checks);

        // Assert
        Assert.Equal("Approve", response.Status);
        Assert.All(response.RuleResults, r => Assert.True(r.Passed));
    }

    [Fact]
    public async Task VerifyAsync_FailingCheck_RejectsWithReason()
    {
        // Arrange — Address is not populated by Doc.Valid(), so a Required check on it fails.
        var svc = BuildService(Doc.Valid());
        var checks = new[] { new FieldCheck(FieldKeys.Address, "required", null) };

        // Act
        var response = await Verify(svc, AllFields, checks);

        // Assert
        Assert.Equal("Reject", response.Status);
        Assert.False(response.RuleResults.Single().Passed);
    }

    [Fact]
    public async Task VerifyAsync_NoChecksSelected_ApprovesVacuously()
    {
        // Arrange
        var svc = BuildService(Doc.Valid());

        // Act
        var response = await Verify(svc, AllFields);

        // Assert
        Assert.Equal("Approve", response.Status);
        Assert.Empty(response.RuleResults);
    }

    [Fact]
    public async Task VerifyAsync_WildcardSelection_ReturnsEveryField()
    {
        // Arrange
        var svc = BuildService(Doc.Valid());

        // Act
        var response = await Verify(svc, AllFields);

        // Assert — Doc.Valid() populates 5 fields.
        Assert.Equal(5, response.ExtractedFields.Count);
    }

    [Fact]
    public async Task VerifyAsync_EmptyFieldSelection_ReturnsEveryField()
    {
        // Arrange
        var svc = BuildService(Doc.Valid());

        // Act
        var response = await Verify(svc, Array.Empty<string>());

        // Assert
        Assert.Equal(5, response.ExtractedFields.Count);
    }

    [Fact]
    public async Task VerifyAsync_SpecificFieldSelection_ProjectsYetChecksRunOnFullExtraction()
    {
        // Arrange — the check targets DocumentNumber, which is not part of the projection.
        var svc = BuildService(Doc.Valid());
        var checks = new[] { new FieldCheck(FieldKeys.DocumentNumber, "checksum", null) };

        // Act
        var response = await Verify(svc, new[] { FieldKeys.FirstName }, checks);

        // Assert — only the requested field is returned...
        Assert.Equal(new[] { FieldKeys.FirstName }, response.ExtractedFields.Keys);
        // ...yet the checksum check (on a non-projected field) still evaluated and passed.
        Assert.True(response.RuleResults.Single(r => r.RuleKey == "documentNumber:checksum").Passed);
    }

    [Fact]
    public async Task VerifyAsync_MockExtractor_PropagatesExtractorMode()
    {
        // Arrange
        var svc = BuildService(Doc.Valid(), mode: "mock");

        // Act
        var response = await Verify(svc, AllFields);

        // Assert
        Assert.Equal("mock", response.ExtractorMode);
    }

    [Fact]
    public async Task VerifyAsync_ExpiredDocumentAgainstFixedClock_Rejects()
    {
        // Arrange — expiry is 2020-01-01, well before the injected 2025-01-01 clock, so the
        // "not expired" check (dateOnOrAfter today) must fail. This exercises the date path that
        // was previously untestable when the service read the ambient wall clock.
        var doc = Doc.With(
            (FieldKeys.FirstName, "Erika"),
            (FieldKeys.DateOfExpiration, "2020-01-01"));
        var svc = BuildService(doc);
        var checks = new[] { new FieldCheck(FieldKeys.DateOfExpiration, "dateOnOrAfter", "today") };

        // Act
        var response = await Verify(svc, AllFields, checks);

        // Assert
        Assert.Equal("Reject", response.Status);
        Assert.False(response.RuleResults.Single().Passed);
    }

    [Fact]
    public async Task VerifyAsync_MultipleChecks_FoldsAllIntoVerdict()
    {
        // Arrange
        var svc = BuildService(Doc.Valid());
        var checks = new[]
        {
            new FieldCheck(FieldKeys.DocumentNumber, "pattern", "^[A-Z0-9]+$"),
            new FieldCheck(FieldKeys.FirstName, "minLength", "2"),
        };

        // Act
        var response = await Verify(svc, AllFields, checks);

        // Assert
        Assert.Equal("Approve", response.Status);
        Assert.Equal(2, response.RuleResults.Count);
        Assert.Contains(response.RuleResults, r => r.RuleKey == "documentNumber:pattern" && r.Passed);
        Assert.Contains(response.RuleResults, r => r.RuleKey == "firstName:minLength" && r.Passed);
    }
}
