using System.Text;
using System.Text.RegularExpressions;
using MaintenanceCMMS.Application.Alerts;
using MaintenanceCMMS.Application.Storage;
using MaintenanceCMMS.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace MaintenanceCMMS.Infrastructure.Alerts;

public sealed class PdfService : IPdfService
{
    private readonly PdfOptions _pdfOptions;
    private readonly IDocumentStorageService _documentStorageService;

    public PdfService(
        IOptions<PdfOptions> pdfOptions,
        IDocumentStorageService documentStorageService)
    {
        _pdfOptions = pdfOptions.Value;
        _documentStorageService = documentStorageService;
    }

    public async Task<PdfRenderResult> RenderAsync(
        PdfRenderRequest request,
        CancellationToken cancellationToken)
    {
        var text = HtmlToText(request.Html);
        var bytes = BuildSimplePdf(text);
        var safeName = SanitizeFileName(request.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            ? request.FileName
            : $"{request.FileName}.pdf");
        Directory.CreateDirectory(_pdfOptions.TemplatePath);
        var entityType = GetDataValue(request.Data, "EntityType") ?? "Alert";
        var entityId = GetDataValue(request.Data, "EntityId") ??
                       GetDataValue(request.Data, "AlertId") ??
                       Path.GetFileNameWithoutExtension(safeName);
        var faenaCodigo = GetDataValue(request.Data, "FaenaCodigo");
        var activoCodigo = entityType.Equals("Activo", StringComparison.OrdinalIgnoreCase)
            ? entityId
            : GetDataValue(request.Data, "ActivoCodigo");
        var otNumero = entityType.Equals("OT", StringComparison.OrdinalIgnoreCase)
            ? entityId
            : GetDataValue(request.Data, "OtNumero");

        var stored = await _documentStorageService.SaveAlertPdfAsync(new DocumentStorageSaveRequest(
            "Alerts",
            entityType,
            entityId,
            safeName,
            "application/pdf",
            bytes,
            "system",
            DocumentStoragePurpose.AlertPdf,
            faenaCodigo,
            activoCodigo,
            otNumero,
            request.Data), cancellationToken);

        return new PdfRenderResult(stored.FileKey, stored.LocalPath ?? stored.Url, bytes);
    }

    private static string HtmlToText(string html)
    {
        var withBreaks = Regex.Replace(html, "(<br\\s*/?>|</p>|</div>|</li>|</tr>)", "\n", RegexOptions.IgnoreCase);
        var stripped = Regex.Replace(withBreaks, "<[^>]+>", " ");
        var decoded = stripped
            .Replace("&nbsp;", " ", StringComparison.OrdinalIgnoreCase)
            .Replace("&amp;", "&", StringComparison.OrdinalIgnoreCase)
            .Replace("&lt;", "<", StringComparison.OrdinalIgnoreCase)
            .Replace("&gt;", ">", StringComparison.OrdinalIgnoreCase);

        return Regex.Replace(decoded, "[ \\t]+", " ").Trim();
    }

    private static byte[] BuildSimplePdf(string text)
    {
        var lines = text
            .Replace("\r", string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(36)
            .ToArray();
        if (lines.Length == 0)
        {
            lines = ["Alerta CMMS"];
        }

        var content = new StringBuilder();
        content.AppendLine("BT");
        content.AppendLine("/F1 12 Tf");
        content.AppendLine("50 790 Td");
        foreach (var line in lines)
        {
            content.AppendLine($"({EscapePdfText(line)}) Tj");
            content.AppendLine("0 -18 Td");
        }
        content.AppendLine("ET");

        var contentText = content.ToString();
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(contentText)} >>\nstream\n{contentText}endstream"
        };

        var builder = new StringBuilder();
        var offsets = new List<int> { 0 };
        builder.AppendLine("%PDF-1.4");
        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(builder.ToString()));
            builder.AppendLine($"{index + 1} 0 obj");
            builder.AppendLine(objects[index]);
            builder.AppendLine("endobj");
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(builder.ToString());
        builder.AppendLine("xref");
        builder.AppendLine($"0 {objects.Length + 1}");
        builder.AppendLine("0000000000 65535 f ");
        foreach (var offset in offsets.Skip(1))
        {
            builder.AppendLine($"{offset:0000000000} 00000 n ");
        }

        builder.AppendLine("trailer");
        builder.AppendLine($"<< /Root 1 0 R /Size {objects.Length + 1} >>");
        builder.AppendLine("startxref");
        builder.AppendLine(xrefOffset.ToString());
        builder.AppendLine("%%EOF");

        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    private static string EscapePdfText(string text)
    {
        var ascii = Encoding.ASCII.GetString(Encoding.ASCII.GetBytes(text));
        return ascii.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
    }

    private static string SanitizeFileName(string value)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '-');
        }

        return value;
    }

    private static string? GetDataValue(IReadOnlyDictionary<string, string?> data, string key)
    {
        return data.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
    }
}
