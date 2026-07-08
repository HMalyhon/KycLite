namespace KycLite.Api.Models;

/// <summary>Multipart form bound by the verify endpoint.</summary>
public sealed class VerifyForm
{
    /// <summary>The uploaded ID/passport image (jpeg, png, tiff) or PDF.</summary>
    public IFormFile? File { get; set; }

    /// <summary>Comma-separated field keys to return, or "*"/empty for the full response.</summary>
    public string? Fields { get; set; }

    /// <summary>
    /// JSON array of field checks, e.g.
    /// [{"field":"dateOfBirth","rule":"dateOnOrBefore","param":"today-18y","name":"Age ≥ 18"}].
    /// </summary>
    public string? FieldChecks { get; set; }
}
