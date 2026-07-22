using KycLite.Api.Extraction;

namespace KycLite.Api.Tests.Extraction;

/// <summary>
/// These two flags decide which extractor <c>Program.cs</c> registers and which credential type
/// <see cref="AzureDocumentExtractor"/> constructs, so the endpoint-only case (keyless auth) is
/// worth pinning: it used to mean "mock".
/// </summary>
public class DocumentIntelligenceOptionsTests
{
    private const string Endpoint = "https://example.cognitiveservices.azure.com/";

    [Fact]
    public void IsConfigured_WithNothingSet_IsFalse()
    {
        // Arrange
        var options = new DocumentIntelligenceOptions();

        // Act & Assert — no endpoint means the offline mock.
        Assert.False(options.IsConfigured);
        Assert.False(options.UsesManagedIdentity);
    }

    [Fact]
    public void IsConfigured_WithEndpointOnly_IsTrueAndUsesManagedIdentity()
    {
        // Arrange — the deployed app's configuration: an endpoint and deliberately no key.
        var options = new DocumentIntelligenceOptions { Endpoint = Endpoint };

        // Act & Assert
        Assert.True(options.IsConfigured);
        Assert.True(options.UsesManagedIdentity);
    }

    [Fact]
    public void UsesManagedIdentity_WithEndpointAndKey_IsFalse()
    {
        // Arrange — the .env quick-start: an explicit account key wins over token auth.
        var options = new DocumentIntelligenceOptions { Endpoint = Endpoint, ApiKey = "secret" };

        // Act & Assert
        Assert.True(options.IsConfigured);
        Assert.False(options.UsesManagedIdentity);
    }

    [Fact]
    public void IsConfigured_WithKeyButNoEndpoint_IsFalse()
    {
        // Arrange — a key alone is unusable; there is nowhere to send it.
        var options = new DocumentIntelligenceOptions { ApiKey = "secret" };

        // Act & Assert
        Assert.False(options.IsConfigured);
        Assert.False(options.UsesManagedIdentity);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("")]
    public void IsConfigured_WithBlankEndpoint_IsFalse(string endpoint)
    {
        // Arrange — .env.example ships the key with an empty value; blanks must not count.
        var options = new DocumentIntelligenceOptions { Endpoint = endpoint };

        // Act & Assert
        Assert.False(options.IsConfigured);
    }
}
