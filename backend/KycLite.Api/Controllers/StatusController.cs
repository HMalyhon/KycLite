using KycLite.Api.Extraction;
using KycLite.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace KycLite.Api.Controllers;

/// <summary>
/// Runtime information about this instance. The web app calls this on load so it can tell the
/// visitor up-front whether uploads will hit the real OCR provider or the offline mock — the
/// same fact each <see cref="VerifyResponse"/> reports afterwards.
/// </summary>
[ApiController]
[Route("api")]
public sealed class StatusController(IDocumentExtractor extractor) : ControllerBase
{
    /// <summary>The extraction provider this instance is running with ("azure" or "mock").</summary>
    [HttpGet("status")]
    public ActionResult<ApiStatus> GetStatus() => Ok(new ApiStatus(extractor.Mode));
}
