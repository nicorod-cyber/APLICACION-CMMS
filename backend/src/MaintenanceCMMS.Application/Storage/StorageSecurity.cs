using System.Buffers.Binary;
using System.IO.Compression;
using System.Xml;
using MaintenanceCMMS.Domain.Common;

namespace MaintenanceCMMS.Application.Storage;

public static class DocumentUrlPolicy
{
    public static Uri RequireHttps(string? value, IReadOnlyCollection<string>? allowedHosts = null)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 4096 || value.Any(char.IsControl) || value.Contains('\\') ||
            !Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(uri.UserInfo) || uri.HostNameType != UriHostNameType.Dns || uri.IsLoopback ||
            (allowedHosts is { Count: > 0 } && !allowedHosts.Contains(uri.IdnHost, StringComparer.OrdinalIgnoreCase)))
            throw new DomainException("El enlace debe ser HTTPS y pertenecer a un host autorizado.");
        return uri;
    }

    public static string RequireDocumentLink(string value, IReadOnlyCollection<string>? allowedHosts = null)
    {
        if (value.StartsWith("/api/sharepoint/download?fileKey=", StringComparison.Ordinal) &&
            !value.Any(char.IsControl) && !value.Contains('\\')) return value;
        return RequireHttps(value, allowedHosts).AbsoluteUri;
    }
}

public static class StoragePathPolicy
{
    public static string Resolve(string root, string path, bool allowAbsolute = false)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Any(char.IsControl) || path.Contains('%') ||
            (!allowAbsolute && (Path.IsPathRooted(path) || path.Contains(':') || path.StartsWith('\\'))))
            throw new DomainException("Ruta de archivo no autorizada.");
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(normalizedRoot, path.Replace('\\', Path.DirectorySeparatorChar)));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!fullPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, comparison))
            throw new DomainException("Ruta de archivo no autorizada.");
        // Reject symlinks and junctions below the configured root, including the target file.
        var current = fullPath;
        while (!string.Equals(current, normalizedRoot, comparison))
        {
            if ((File.Exists(current) || Directory.Exists(current)) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new DomainException("No se permiten enlaces simbolicos en el almacenamiento.");
            current = Path.GetDirectoryName(current)!;
        }
        return fullPath;
    }

    public static string SafeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new DomainException("Nombre de archivo invalido.");
        var name = value.Replace('\\', '/').Split('/').Last().Trim();
        name = new string(name.Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' or ' ' ? c : '-').ToArray()).Trim(' ', '.');
        if (name.Length is 0 or > 180) throw new DomainException("Nombre de archivo invalido.");
        var stem = name.Split('.')[0].ToUpperInvariant();
        if (stem is "CON" or "PRN" or "AUX" or "NUL" ||
            (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && char.IsDigit(stem[3])))
            throw new DomainException("Nombre de archivo reservado.");
        return name;
    }
}

public static class UploadPolicy
{
    public const int MaximumBytes = 20 * 1024 * 1024;
    public const string ExcelMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static void Validate(string fileName, string contentType, byte[] content, bool imagesOnly = false, int maximumBytes = MaximumBytes)
    {
        if (content.Length == 0 || content.Length > maximumBytes) throw new DomainException("El archivo esta vacio o supera el limite permitido.");
        var extension = Path.GetExtension(StoragePathPolicy.SafeFileName(fileName)).ToLowerInvariant();
        var expectedMime = extension switch
        {
            ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".webp" => "image/webp",
            ".pdf" when !imagesOnly => "application/pdf", ".xlsx" when !imagesOnly => ExcelMime,
            ".docx" when !imagesOnly => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            _ => throw new DomainException("Formato de archivo no permitido.")
        };
        if (!string.Equals(contentType, expectedMime, StringComparison.OrdinalIgnoreCase))
            throw new DomainException("El tipo MIME no corresponde al formato permitido.");
        ReadOnlySpan<byte> bytes = content;
        var valid = extension switch
        {
            ".png" => bytes.Length >= 45 && bytes.StartsWith(new byte[] {137,80,78,71,13,10,26,10}) && bytes.Slice(12,4).SequenceEqual("IHDR"u8) && bytes.Slice(bytes.Length-8,4).SequenceEqual("IEND"u8),
            ".jpg" or ".jpeg" => bytes.Length >= 12 && bytes.StartsWith(new byte[] {255,216,255}) && bytes.EndsWith(new byte[] {255,217}),
            ".webp" => bytes.Length >= 20 && bytes.StartsWith("RIFF"u8) && bytes.Slice(8,4).SequenceEqual("WEBP"u8) && BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(4,4)) == bytes.Length-8 && (bytes.Slice(12,4).SequenceEqual("VP8 "u8) || bytes.Slice(12,4).SequenceEqual("VP8L"u8) || bytes.Slice(12,4).SequenceEqual("VP8X"u8)),
            ".pdf" => bytes.StartsWith("%PDF-"u8) && bytes.Slice(Math.Max(0, bytes.Length-1024)).IndexOf("%%EOF"u8) >= 0,
            ".xlsx" or ".docx" => ValidateOffice(content, extension),
            _ => false
        };
        if (!valid) throw new DomainException("El contenido real no corresponde al formato del archivo.");
    }

    private static bool ValidateOffice(byte[] content, string extension)
    {
        try
        {
            using var zip = new ZipArchive(new MemoryStream(content), ZipArchiveMode.Read);
            if (zip.Entries.Count > 4096 || zip.GetEntry("[Content_Types].xml") is null ||
                zip.GetEntry(extension == ".xlsx" ? "xl/workbook.xml" : "word/document.xml") is null) return false;
            long expanded = 0;
            foreach (var entry in zip.Entries)
            {
                expanded = checked(expanded + entry.Length);
                if (expanded > 100 * 1024 * 1024 || entry.Length > 20 * 1024 * 1024 ||
                    entry.FullName.Contains("..") || entry.FullName.Contains('\\') || entry.FullName.StartsWith('/') ||
                    entry.FullName.Contains("vbaProject", StringComparison.OrdinalIgnoreCase) ||
                    entry.FullName.Contains("embeddings/", StringComparison.OrdinalIgnoreCase)) return false;
                if (!entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) && !entry.FullName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)) continue;
                using var reader = XmlReader.Create(entry.Open(), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 20 * 1024 * 1024 });
                while (reader.Read())
                {
                    // External hyperlinks are data; external workbook/image/template relationships can fetch content.
                    if (reader.LocalName == "Relationship" && reader.GetAttribute("TargetMode") == "External" &&
                        !(reader.GetAttribute("Type")?.EndsWith("/hyperlink", StringComparison.Ordinal) ?? false)) return false;
                }
            }
            return true;
        }
        catch (Exception ex) when (ex is InvalidDataException or XmlException or OverflowException) { return false; }
    }
}
