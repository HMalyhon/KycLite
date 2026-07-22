using System.Globalization;
using Azure;
using Azure.AI.DocumentIntelligence;
using Azure.Identity;
using KycLite.Api.Catalog;
using KycLite.Api.Models;
using Microsoft.Extensions.Options;

namespace KycLite.Api.Extraction;

/// <summary>
/// Real extractor backed by Azure AI Document Intelligence's prebuilt ID document model.
/// All Azure types stay inside this class; callers only see <see cref="ExtractionResult"/>.
/// </summary>
public sealed class AzureDocumentExtractor(IOptions<DocumentIntelligenceOptions> options) : IDocumentExtractor
{
    private const string ModelId = "prebuilt-idDocument";
    private const string ModeName = "azure";

    // Fallback document type when Azure doesn't classify the document (also our provider-agnostic tag).
    private const string DefaultDocumentType = "idDocument";

    // Date fields are normalized to this ISO 8601 form so downstream date rules parse consistently.
    private const string DateFormat = "yyyy-MM-dd";

    // Bound how long a single analysis may run so a stuck Azure operation can't tie up the request.
    private static readonly TimeSpan AnalyzeTimeout = TimeSpan.FromSeconds(60);

    // Azure ID-document field name -> our canonical key.
    private static readonly (string Azure, string Canonical)[] FieldMap =
    [
        ("FirstName", FieldKeys.FirstName),
        ("LastName", FieldKeys.LastName),
        ("DocumentNumber", FieldKeys.DocumentNumber),
        ("DateOfBirth", FieldKeys.DateOfBirth),
        ("DateOfExpiration", FieldKeys.DateOfExpiration),
        ("Sex", FieldKeys.Sex),
        ("Nationality", FieldKeys.Nationality),
        ("CountryRegion", FieldKeys.CountryRegion),
        ("Address", FieldKeys.Address),
    ];

    // Keyless by default: with no ApiKey configured we authenticate with Entra ID, so the deployed
    // app holds no secret at all (App Service managed identity; `az login` when running locally).
    // A key, when supplied, still works — it keeps the .env quick-start viable.
    private readonly DocumentIntelligenceClient _client = options.Value.UsesManagedIdentity
        ? new DocumentIntelligenceClient(new Uri(options.Value.Endpoint!), new DefaultAzureCredential())
        : new DocumentIntelligenceClient(new Uri(options.Value.Endpoint!), new AzureKeyCredential(options.Value.ApiKey!));

    public string Mode => ModeName;

    public async Task<ExtractionResult> ExtractAsync(Stream image, string contentType, CancellationToken ct)
    {
        var bytes = await BinaryData.FromStreamAsync(image, ct);

        // Cap the wait with a linked token so a slow/hung Azure operation fails fast, while still
        // honouring a genuine client abort (which stays an OperationCanceledException → not a fault).
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(AnalyzeTimeout);

        Operation<AnalyzeResult> operation;
        try
        {
            operation = await _client.AnalyzeDocumentAsync(
                WaitUntil.Completed, ModelId, bytes, cancellationToken: timeoutCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new TimeoutException("Document analysis timed out.");
        }
        catch (RequestFailedException ex) when (ex.Status is 400 or 413 or 415)
        {
            // Azure rejected the upload itself (invalid/unsupported/oversized document): that's a
            // client-input problem, not a server fault. Quota errors (429) still bubble.
            throw new UnprocessableDocumentException("The uploaded document could not be processed.", ex);
        }
        catch (RequestFailedException ex) when (ex.Status is 401 or 403)
        {
            // Azure took the token but won't authorize it — a missing or still-propagating role
            // assignment. Distinguished from a generic fault so the response can say so.
            throw new ProviderAuthenticationException(
                "The document provider rejected this application's credentials.", ex);
        }
        catch (AuthenticationFailedException ex)
        {
            // Couldn't obtain a token at all (no managed identity, no `az login`, wrong tenant).
            // CredentialUnavailableException derives from this, so both land here.
            throw new ProviderAuthenticationException(
                "The application could not authenticate to the document provider.", ex);
        }

        AnalyzeResult analyze = operation.Value;
        var result = new ExtractionResult { DocumentType = DefaultDocumentType };

        if (analyze.Documents is not [var document, ..]) return result;

        result.DocumentType = string.IsNullOrEmpty(document.DocumentType) ? DefaultDocumentType : document.DocumentType;

        foreach (var (azureName, canonical) in FieldMap)
        {
            if (document.Fields.TryGetValue(azureName, out DocumentField? field) && field is not null)
            {
                var value = Normalize(field);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    result.Fields[canonical] = new FieldValue(value, field.Confidence);
                }
            }
        }

        return result;
    }

    /// <summary>Produce a normalized string per field; dates become yyyy-MM-dd.</summary>
    private static string? Normalize(DocumentField field)
    {
        if (field.FieldType == DocumentFieldType.Date && field.ValueDate is { } date)
        {
            return date.ToString(DateFormat, CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrWhiteSpace(field.ValueString))
        {
            return field.ValueString;
        }

        return field.Content;
    }
}
