namespace KycLite.Api.Extraction;

/// <summary>
/// The extraction provider rejected this app's credentials — with the keyless setup that almost
/// always means the managed identity is missing the "Cognitive Services User" role on the resource
/// (or the assignment hasn't propagated yet), not that the caller did anything wrong.
/// Mapped to <c>503</c> so an operator sees a diagnosable answer instead of a blanket 500.
/// </summary>
public sealed class ProviderAuthenticationException(string message, Exception? innerException = null)
    : Exception(message, innerException)
{
}
