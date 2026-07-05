using KycLite.Api.Catalog;
using KycLite.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace KycLite.Api.Controllers;

/// <summary>
/// Discovery endpoints that tell the web app which fields are available, so the frontend builds
/// its UI dynamically.
/// </summary>
[ApiController]
[Route("api")]
public sealed class CatalogController : ControllerBase
{
    /// <summary>Fields the user can choose to include in the response (each tagged with a type).</summary>
    [HttpGet("fields")]
    public ActionResult<IReadOnlyList<FieldDescriptor>> GetFields() => Ok(FieldCatalog.All);
}
