namespace KycLite.Api.Validation;

/// <summary>
/// The "magic number" byte prefixes of the document formats the API accepts. A file's declared
/// Content-Type is client-supplied and easily spoofed, so uploads are verified against this table
/// of real signatures instead. Adding a format is a one-line entry here.
/// </summary>
public static class FileSignatures
{
    /// <summary>A supported format and the leading bytes every file of that format begins with.</summary>
    private sealed record Signature(string Format, byte[] Magic);

    private static readonly Signature[] Known =
    [
        new("JPEG",                 [0xFF, 0xD8, 0xFF]),
        new("PNG",                  [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
        new("TIFF (little-endian)", [0x49, 0x49, 0x2A, 0x00]),
        new("TIFF (big-endian)",    [0x4D, 0x4D, 0x00, 0x2A]),
        new("PDF",                  [0x25, 0x50, 0x44, 0x46]), // "%PDF"
    ];

    /// <summary>How many leading bytes to read to be able to identify any supported format.</summary>
    public static int MaxSignatureLength { get; } = Known.Max(s => s.Magic.Length);

    /// <summary>True if <paramref name="header"/> starts with a supported format's magic bytes.</summary>
    public static bool IsSupported(ReadOnlySpan<byte> header)
    {
        foreach (var signature in Known)
        {
            if (header.StartsWith(signature.Magic))
                return true;
        }

        return false;
    }
}
