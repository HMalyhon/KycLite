namespace KycLite.Api.Extraction;

/// <summary>
/// Azure Document Intelligence connection settings, bound from the "DocumentIntelligence"
/// configuration section (populated from the .env file / environment variables).
/// When unset, the API falls back to the offline mock extractor.
/// </summary>
public sealed class DocumentIntelligenceOptions
{
    public const string SectionName = "DocumentIntelligence";

    public string? Endpoint { get; set; }
    public string? ApiKey { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Endpoint) && !string.IsNullOrWhiteSpace(ApiKey);
}
