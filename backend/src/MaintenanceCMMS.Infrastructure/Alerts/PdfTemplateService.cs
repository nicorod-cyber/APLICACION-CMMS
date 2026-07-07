using System.Globalization;
using MaintenanceCMMS.Application.Abstractions.Data;
using MaintenanceCMMS.Application.Alerts;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace MaintenanceCMMS.Infrastructure.Alerts;

public sealed class PdfTemplateService : IPdfTemplateService
{
    private const string PdfTemplatesSchema = "pdf_templates";

    private readonly IDataProvider _dataProvider;
    private readonly IAuditService _auditService;
    private readonly IAuthorizationPolicyService _authorizationPolicyService;
    private readonly PdfOptions _options;

    public PdfTemplateService(
        IDataProvider dataProvider,
        IAuditService auditService,
        IAuthorizationPolicyService authorizationPolicyService,
        IOptions<PdfOptions> options)
    {
        _dataProvider = dataProvider;
        _auditService = auditService;
        _authorizationPolicyService = authorizationPolicyService;
        _options = options.Value;
    }

    public async Task<IReadOnlyCollection<PdfTemplateResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await EnsureTemplatesAsync(cancellationToken);
        return rows.Select(ToResponse).OrderBy(template => template.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<PdfTemplateResponse?> UpdateAsync(
        string id,
        UpdatePdfTemplateRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanConfigure(user);
        DomainGuard.AgainstEmpty(request.Name, nameof(request.Name));
        DomainGuard.AgainstEmpty(request.EventType, nameof(request.EventType));
        DomainGuard.AgainstEmpty(request.SubjectTemplate, nameof(request.SubjectTemplate));
        DomainGuard.AgainstEmpty(request.HtmlTemplate, nameof(request.HtmlTemplate));
        DomainGuard.AgainstEmpty(request.Reason ?? string.Empty, "reason");

        var rows = (await EnsureTemplatesAsync(cancellationToken)).ToList();
        var index = FindIndex(rows, id);
        if (index < 0)
        {
            return null;
        }

        var previous = rows[index];
        var updated = TemplateRow(
            previous.GetValue("TemplateId") ?? id,
            request.Name,
            request.EventType,
            request.SubjectTemplate,
            request.HtmlTemplate,
            request.Active,
            DateTimeOffset.UtcNow);

        rows[index] = updated;
        await _dataProvider.SaveRowsAsync(PdfTemplatesSchema, rows, cancellationToken);
        await WriteTemplateFileAsync(ToResponse(updated), cancellationToken);
        await _auditService.RecordAsync(new AuditEventRequest(
            user.UserId,
            "pdf_template.updated",
            AuditModules.Pdfs,
            "PdfTemplate",
            id,
            previous.GetValue("HtmlTemplate"),
            updated.GetValue("HtmlTemplate"),
            Severity: AuditSeverity.Medium,
            Reason: request.Reason), cancellationToken);

        return ToResponse(updated);
    }

    public async Task<PdfPreviewResponse?> PreviewAsync(
        PdfPreviewRequest request,
        CancellationToken cancellationToken)
    {
        var templates = await ListAsync(cancellationToken);
        var template = templates.FirstOrDefault(item => SameCode(item.TemplateId, request.TemplateId));
        if (template is null)
        {
            return null;
        }

        var data = DefaultPreviewData(request.Data);
        var html = RenderTemplate(template.HtmlTemplate, data);
        var text = html.Replace("<", " <").Replace(">", "> ");
        return new PdfPreviewResponse(template.TemplateId, html, text);
    }

    internal async Task<IReadOnlyCollection<DataRow>> EnsureTemplatesAsync(CancellationToken cancellationToken)
    {
        var rows = (await _dataProvider.ReadRowsAsync(PdfTemplatesSchema, cancellationToken)).ToList();
        if (rows.Count > 0)
        {
            return rows;
        }

        rows.AddRange(DefaultTemplates());
        await _dataProvider.SaveRowsAsync(PdfTemplatesSchema, rows, cancellationToken);
        foreach (var template in rows.Select(ToResponse))
        {
            await WriteTemplateFileAsync(template, cancellationToken);
        }

        return rows;
    }

    internal static string RenderTemplate(string template, IReadOnlyDictionary<string, string?> data)
    {
        var result = template;
        foreach (var item in data)
        {
            result = result.Replace($"{{{{{item.Key}}}}}", item.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    private async Task WriteTemplateFileAsync(PdfTemplateResponse template, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_options.TemplatePath);
        var path = Path.Combine(_options.TemplatePath, $"{SanitizeFileName(template.TemplateId)}.html");
        await File.WriteAllTextAsync(path, template.HtmlTemplate, cancellationToken);
    }

    private void EnsureCanConfigure(UserAccessContext user)
    {
        if (!_authorizationPolicyService.CanConfigureAlerts(user))
        {
            throw new UnauthorizedAccessException("El usuario no tiene permiso para configurar plantillas.");
        }
    }

    private static IReadOnlyDictionary<string, string?> DefaultPreviewData(IReadOnlyDictionary<string, string?>? data)
    {
        return new Dictionary<string, string?>(data ?? new Dictionary<string, string?>(), StringComparer.OrdinalIgnoreCase)
        {
            ["Title"] = data?.GetValueOrDefault("Title") ?? "Documento por vencer",
            ["Message"] = data?.GetValueOrDefault("Message") ?? "El documento vence dentro del plazo configurado.",
            ["Severity"] = data?.GetValueOrDefault("Severity") ?? "Warning",
            ["Source"] = data?.GetValueOrDefault("Source") ?? "Documentos",
            ["EntityId"] = data?.GetValueOrDefault("EntityId") ?? "EQ-001",
            ["FaenaCodigo"] = data?.GetValueOrDefault("FaenaCodigo") ?? "F001",
            ["CreatedAtUtc"] = data?.GetValueOrDefault("CreatedAtUtc") ?? DateTimeOffset.UtcNow.ToString("O")
        };
    }

    private static IReadOnlyCollection<DataRow> DefaultTemplates()
    {
        const string html = """
            <html>
              <body>
                <h1>{{Title}}</h1>
                <p><strong>Severidad:</strong> {{Severity}}</p>
                <p><strong>Origen:</strong> {{Source}}</p>
                <p><strong>Entidad:</strong> {{EntityId}}</p>
                <p><strong>Faena:</strong> {{FaenaCodigo}}</p>
                <p>{{Message}}</p>
                <p><small>Generado: {{CreatedAtUtc}}</small></p>
              </body>
            </html>
            """;

        return
        [
            TemplateRow(
                "alert-default",
                "Alerta CMMS",
                "alert",
                "[CMMS] {{Title}}",
                html,
                true,
                DateTimeOffset.UtcNow)
        ];
    }

    private static DataRow TemplateRow(
        string id,
        string name,
        string eventType,
        string subjectTemplate,
        string htmlTemplate,
        bool active,
        DateTimeOffset updatedAtUtc)
    {
        return new DataRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["TemplateId"] = id.Trim(),
            ["Name"] = name.Trim(),
            ["EventType"] = eventType.Trim(),
            ["SubjectTemplate"] = subjectTemplate.Trim(),
            ["HtmlTemplate"] = htmlTemplate.Trim(),
            ["Active"] = active ? "true" : "false",
            ["UpdatedAtUtc"] = updatedAtUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)
        });
    }

    private static PdfTemplateResponse ToResponse(DataRow row)
    {
        return new PdfTemplateResponse(
            row.GetValue("TemplateId")?.Trim() ?? string.Empty,
            row.GetValue("Name")?.Trim() ?? string.Empty,
            row.GetValue("EventType")?.Trim() ?? "alert",
            row.GetValue("SubjectTemplate")?.Trim() ?? "[CMMS] {{Title}}",
            row.GetValue("HtmlTemplate")?.Trim() ?? string.Empty,
            ParseBool(row.GetValue("Active"), true),
            ParseDateTime(row.GetValue("UpdatedAtUtc")) ?? DateTimeOffset.UtcNow);
    }

    private static int FindIndex(IReadOnlyList<DataRow> rows, string id)
    {
        for (var index = 0; index < rows.Count; index++)
        {
            if (SameCode(rows[index].GetValue("TemplateId"), id))
            {
                return index;
            }
        }

        return -1;
    }

    private static DateTimeOffset? ParseDateTime(string? value)
    {
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var result)
            ? result
            : null;
    }

    private static bool ParseBool(string? value, bool defaultValue = false)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("si", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("1", StringComparison.OrdinalIgnoreCase);
    }

    private static bool SameCode(string? left, string? right)
    {
        return string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeFileName(string value)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '-');
        }

        return value;
    }
}
