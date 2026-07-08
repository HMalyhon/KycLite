namespace KycLite.Api.Extraction;

/// <summary>
/// Thrown when the extraction provider rejects the upload itself — an unreadable, unsupported, or
/// malformed document. This is a client-input problem, not a server fault, so the API surfaces it
/// as 422 Unprocessable Entity (see <see cref="Infrastructure.GlobalExceptionHandler"/>).
/// </summary>
public sealed class UnprocessableDocumentException(string message, Exception? innerException = null)
    : Exception(message, innerException);
