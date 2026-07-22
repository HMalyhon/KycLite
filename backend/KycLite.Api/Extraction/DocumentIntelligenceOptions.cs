namespace KycLite.Api.Extraction;

/// <summary>
/// Azure Document Intelligence connection settings, bound from the "DocumentIntelligence"
/// configuration section (populated from the .env file / environment variables).
/// When <see cref="Endpoint"/> is unset, the API falls back to the offline mock extractor.
/// </summary>
public sealed class DocumentIntelligenceOptions
{
    public const string SectionName = "DocumentIntelligence";

    /// <summary>Resource endpoint, e.g. https://kyclite-di.cognitiveservices.azure.com/.</summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Optional account key. Leave it unset to authenticate with Entra ID instead
    /// (see <see cref="UsesManagedIdentity"/>) — which is what the deployed app does.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>The endpoint alone is enough: with no key we authenticate with a token credential.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Endpoint);

    /// <summary>
    /// True when an endpoint is configured but no key, i.e. the extractor authenticates with a
    /// token credential — the App Service's managed identity in Azure, `az login` locally.
    /// </summary>
    public bool UsesManagedIdentity => IsConfigured && string.IsNullOrWhiteSpace(ApiKey);
}
