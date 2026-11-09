using System.IO.Compression;
using System.Text;

namespace UniversityLeaders.Application.Attachments;

public enum AttachmentKind
{
    Jpeg = 1,
    Png = 2,
    Pdf = 3,
    Docx = 4,
}

/// <summary>
/// Decides what a file really is from its bytes (magic numbers and structure), never from the client's name or MIME type,
/// and rejects content that is detectably unsafe. Heuristic by nature: see README "Attachments" for the limits.
/// </summary>
public static class AttachmentInspector
{
    public const string DocxContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private const int MaxZipEntries = 1000;
    private const long MaxZipUncompressedBytes = 100L * 1024 * 1024;
    private const int MaxZipCompressionRatio = 100;

    private static readonly byte[] JpegMagic = { 0xFF, 0xD8, 0xFF };
    private static readonly byte[] PngMagic = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    private static readonly byte[] PdfMagic = Encoding.ASCII.GetBytes("%PDF-");
    private static readonly byte[] ZipMagic = { 0x50, 0x4B, 0x03, 0x04 };

    // Active or hidden PDF content we refuse to store.
    private static readonly string[] PdfUnsafeMarkers = { "/JavaScript", "/JS", "/Launch", "/EmbeddedFile", "/RichMedia", "/Encrypt" };

    public static string ContentType(AttachmentKind kind)
    {
        return kind switch
        {
            AttachmentKind.Jpeg => "image/jpeg",
            AttachmentKind.Png => "image/png",
            AttachmentKind.Pdf => "application/pdf",
            _ => DocxContentType,
        };
    }

    /// <summary>Kind implied by the file extension, or null for unsupported extensions.</summary>
    public static AttachmentKind? KindFromExtension(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => AttachmentKind.Jpeg,
            ".png" => AttachmentKind.Png,
            ".pdf" => AttachmentKind.Pdf,
            ".docx" => AttachmentKind.Docx,
            _ => null,
        };
    }

    /// <summary>Kind detected from the content's signature, or null when it is none of the supported formats.</summary>
    public static AttachmentKind? Detect(ReadOnlySpan<byte> data)
    {
        if (data.StartsWith(JpegMagic)) return AttachmentKind.Jpeg;
        if (data.StartsWith(PngMagic)) return AttachmentKind.Png;
        if (data.StartsWith(PdfMagic)) return AttachmentKind.Pdf;
        if (data.StartsWith(ZipMagic)) return AttachmentKind.Docx; // confirmed by FindProblem (must be a real Word document)
        return null;
    }

    /// <summary>Returns a reason when the content is malformed or detectably unsafe for its kind; null when acceptable.</summary>
    public static string? FindProblem(byte[] data, AttachmentKind kind)
    {
        return kind switch
        {
            AttachmentKind.Pdf => InspectPdf(data),
            AttachmentKind.Docx => InspectDocx(data),
            _ => null, // images: signature checked; served only as downloads with nosniff
        };
    }

    private static string? InspectPdf(byte[] data)
    {
        string text = Encoding.Latin1.GetString(data);
        int tail = Math.Max(0, text.Length - 2048);
        if (text.IndexOf("%%EOF", tail, StringComparison.Ordinal) < 0)
        {
            return "The PDF is truncated or malformed.";
        }

        foreach (string marker in PdfUnsafeMarkers)
        {
            if (text.Contains(marker, StringComparison.Ordinal))
            {
                return marker == "/Encrypt"
                    ? "Encrypted PDFs cannot be checked and are not accepted."
                    : "PDFs with scripts, launch actions or embedded files are not accepted.";
            }
        }

        return null;
    }

    private static string? InspectDocx(byte[] data)
    {
        try
        {
            using MemoryStream stream = new MemoryStream(data, writable: false);
            using ZipArchive zip = new ZipArchive(stream, ZipArchiveMode.Read);
            if (zip.Entries.Count > MaxZipEntries)
            {
                return "The document has too many parts.";
            }

            long total = 0;
            bool hasContentTypes = false;
            bool hasDocument = false;
            foreach (ZipArchiveEntry entry in zip.Entries)
            {
                string name = entry.FullName;
                if (name.Contains("..", StringComparison.Ordinal) || name.StartsWith('/') || name.Contains('\\'))
                {
                    return "The document contains invalid part names.";
                }

                total += entry.Length;
                bool suspiciousRatio = entry.CompressedLength > 0 && entry.Length > 1024 * 1024 && entry.Length / entry.CompressedLength > MaxZipCompressionRatio;
                if (total > MaxZipUncompressedBytes || suspiciousRatio)
                {
                    return "The document expands to an unsafe size.";
                }

                if (name.Equals("[Content_Types].xml", StringComparison.Ordinal)) hasContentTypes = true;
                if (name.Equals("word/document.xml", StringComparison.Ordinal)) hasDocument = true;

                string lower = name.ToLowerInvariant();
                if (lower.EndsWith("vbaproject.bin", StringComparison.Ordinal) || (lower.StartsWith("word/embeddings/", StringComparison.Ordinal) && lower.EndsWith(".bin", StringComparison.Ordinal)))
                {
                    return "Documents with macros or embedded objects are not accepted.";
                }

                // Remote template injection: settings relationships pointing outside the document.
                if (lower == "word/_rels/settings.xml.rels" && ReadEntry(entry).Contains("TargetMode=\"External\"", StringComparison.OrdinalIgnoreCase))
                {
                    return "Documents that load remote templates are not accepted.";
                }

                if (name.Equals("[Content_Types].xml", StringComparison.Ordinal) && ReadEntry(entry).Contains("macroEnabled", StringComparison.OrdinalIgnoreCase))
                {
                    return "Documents with macros are not accepted.";
                }
            }

            return hasContentTypes && hasDocument ? null : "The file is not a Word (.docx) document.";
        }
        catch (InvalidDataException)
        {
            return "The document is corrupt.";
        }
    }

    private static string ReadEntry(ZipArchiveEntry entry)
    {
        using Stream stream = entry.Open();
        using StreamReader reader = new StreamReader(stream);
        char[] buffer = new char[64 * 1024]; // relationship/content-type parts are small; read a bounded prefix
        int read = reader.ReadBlock(buffer, 0, buffer.Length);
        return new string(buffer, 0, read);
    }
}
