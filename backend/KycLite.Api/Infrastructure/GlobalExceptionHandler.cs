using KycLite.Api.Extraction;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace KycLite.Api.Infrastructure;

/// <summary>
/// Turns any unhandled exception into an RFC 7807 <see cref="ProblemDetails"/> response, so the API
/// never leaks stack traces and every error shares one consistent JSON shape. Registered via
/// <c>AddExceptionHandler</c> + <c>UseExceptionHandler</c> in <c>Program.cs</c>.
/// </summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // A client aborting the request surfaces here as a cancellation — that's not a server
        // fault, so let it propagate rather than logging an error and writing a 500 onto a dead
        // response.
        if (exception is OperationCanceledException)
            return false;

        // Map known failure kinds to a fitting status; everything else is an unexpected 500.
        var (status, title) = exception switch
        {
            UnprocessableDocumentException => (StatusCodes.Status422UnprocessableEntity,
                "The uploaded document could not be processed."),
            TimeoutException => (StatusCodes.Status504GatewayTimeout,
                "Document processing timed out. Please try again."),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred."),
        };

        // Server faults (5xx) are logged as errors; client-input faults (4xx) as warnings.
        if (status >= StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception while processing {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
        else
            logger.LogWarning(exception, "Request could not be processed: {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = status;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails { Title = title, Status = status },
        });
    }
}
