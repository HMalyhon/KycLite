using System.Reflection;
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
    // Stamped at publish time (-p:InformationalVersion=<git sha>), so a caller can tell *which*
    // build answered. That matters because App Service keeps serving the previous container for
    // ~20-30s after a deployment reports success: "the app is up" and "the new app is up" are not
    // the same claim, and the deploy smoke test needs the second one. Falls back to whatever the
    // SDK stamped for a local build, where no SHA is injected.
    private static readonly string BuildVersion =
        typeof(StatusController).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "unknown";

    /// <summary>The extraction provider this instance is running with ("azure" or "mock").</summary>
    [HttpGet("status")]
    public ActionResult<ApiStatus> GetStatus() => Ok(new ApiStatus(extractor.Mode, BuildVersion));
}
