using KycLite.Api.Models;

namespace KycLite.Api.Services;

/// <summary>
/// Orchestrates a verification request: extract the document, run the user's field checks, and
/// project the response to the requested fields. Keeps controllers thin.
/// </summary>
public interface IVerificationService
{
    Task<VerifyResponse> VerifyAsync(
        Stream image,
        string contentType,
        IEnumerable<string> selectedFields,
        IEnumerable<FieldCheck> fieldChecks,
        CancellationToken ct);
}
