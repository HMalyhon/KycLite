using KycLite.Api.Catalog;
using KycLite.Api.Models;
using KycLite.Api.Validation;
using Microsoft.AspNetCore.Mvc;

namespace KycLite.Api.Controllers;

/// <summary>
/// Discovery endpoints that tell the web app which fields and field-rules are available (and the
/// default check set to seed with), so the frontend builds its UI dynamically.
/// </summary>
[ApiController]
[Route("api")]
public sealed class CatalogController(FieldCheckRunner fieldChecks) : ControllerBase
{
    /// <summary>Fields the user can choose to include in the response (each tagged with a type).</summary>
    [HttpGet("fields")]
    public ActionResult<IReadOnlyList<FieldDescriptor>> GetFields() => Ok(FieldCatalog.All);

    /// <summary>Generic field-rules and the field types each applies to (the matrix).</summary>
    [HttpGet("field-rules")]
    public ActionResult<IReadOnlyList<FieldRuleDescriptor>> GetFieldRules() =>
        Ok(FieldRuleCatalog.Describe(fieldChecks.AvailableRules));

    /// <summary>The default check set the UI seeds with (replicates the legacy fixed rules).</summary>
    [HttpGet("default-checks")]
    public ActionResult<IReadOnlyList<FieldCheck>> GetDefaultChecks() => Ok(DefaultChecks.All);
}
