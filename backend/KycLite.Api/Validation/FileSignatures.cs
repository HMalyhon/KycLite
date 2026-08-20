namespace KycLite.Api.Validation;

/// <summary>A supported format, identified from a file's leading bytes rather than its headers.</summary>
public sealed record DetectedFormat(string Name, string MediaType);

/// <summary>
/// The "magic number" byte prefixes of the document formats the API accepts. A file's declared
/// Content-Type is client-supplied and easily spoofed, so uploads are identified from this table of
/// real signatures instead — and the declared type is then checked against what the bytes actually
/// are. Adding a format is a one-line entry here.
/// </summary>
public static class FileSignatures
{
    /// <summary>
    /// A supported format, the media type a correct client declares for it, and the leading bytes
    /// every file of that format begins with.
    /// </summary>
    private sealed record Signature(string Name, string MediaType, byte[] Magic);

    private static readonly Signature[] Known =
    [
        new("JPEG",                 "image/jpeg",      [0xFF, 0xD8, 0xFF]),
        new("PNG",                  "image/png",       [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
        new("TIFF (little-endian)", "image/tiff",      [0x49, 0x49, 0x2A, 0x00]),
        new("TIFF (big-endian)",    "image/tiff",      [0x4D, 0x4D, 0x00, 0x2A]),
        new("PDF",                  "application/pdf", [0x25, 0x50, 0x44, 0x46]), // "%PDF"
    ];

    /// <summary>The media types a client may declare — every distinct type in the table above.</summary>
    public static IReadOnlyList<string> SupportedMediaTypes { get; } =
        [.. Known.Select(s => s.MediaType).Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>How many leading bytes to read to be able to identify any supported format.</summary>
    public static int MaxSignatureLength { get; } = Known.Max(s => s.Magic.Length);

    /// <summary>
    /// The format <paramref name="header"/> actually begins with, or null if it is none of them.
    /// </summary>
    public static DetectedFormat? Detect(ReadOnlySpan<byte> header)
    {
        foreach (var signature in Known)
        {
            if (header.StartsWith(signature.Magic))
                return new DetectedFormat(signature.Name, signature.MediaType);
        }

        return null;
    }
}
