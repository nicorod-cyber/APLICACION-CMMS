using System.Text.RegularExpressions;
using MaintenanceCMMS.Application.Alerts;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using MaintenanceCMMS.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MaintenanceCMMS.Infrastructure.Alerts;

public sealed class PdfTemplateService : IPdfTemplateService
{
    private static readonly Regex PlaceholderPattern = new("{{\\s*([A-Za-z][A-Za-z0-9_]{0,80})\\s*}}", RegexOptions.Compiled);
    private readonly CmmsDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly IAuthorizationPolicyService _authorizationPolicyService;
    private readonly BootstrapDefaultsOptions _defaults;

    public PdfTemplateService(
        CmmsDbContext dbContext,
        IAuditService auditService,
        IAuthorizationPolicyService authorizationPolicyService, IOptions<BootstrapDefaultsOptions>? defaults = null)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _authorizationPolicyService = authorizationPolicyService;
        _defaults = defaults?.Value ?? new BootstrapDefaultsOptions();
    }

    public async Task<IReadOnlyCollection<PdfTemplateResponse>> ListAsync(CancellationToken cancellationToken)
    {
        await EnsureDefaultTemplateAsync(cancellationToken);
        return (await _dbContext.PdfTemplates.AsNoTracking().OrderBy(item => item.Name).ToArrayAsync(cancellationToken)).Select(ToResponse).ToArray();
    }

    public async Task<PdfTemplateResponse?> UpdateAsync(string id, UpdatePdfTemplateRequest request, UserAccessContext user, CancellationToken cancellationToken)
    {
        EnsureCanConfigure(user);
        DomainGuard.AgainstEmpty(request.Name, nameof(request.Name));
        DomainGuard.AgainstEmpty(request.EventType, nameof(request.EventType));
        DomainGuard.AgainstEmpty(request.SubjectTemplate, nameof(request.SubjectTemplate));
        DomainGuard.AgainstEmpty(request.HtmlTemplate, nameof(request.HtmlTemplate));
        DomainGuard.AgainstEmpty(request.Reason ?? string.Empty, "reason");
        ValidatePlaceholders(request.SubjectTemplate); ValidatePlaceholders(request.HtmlTemplate);

        var template = await _dbContext.PdfTemplates.SingleOrDefaultAsync(item => item.Code == id, cancellationToken);
        if (template is null) return null;
        var previous = template.HtmlTemplate;
        template.Name = request.Name.Trim(); template.EventType = request.EventType.Trim(); template.SubjectTemplate = request.SubjectTemplate.Trim(); template.HtmlTemplate = request.HtmlTemplate.Trim(); template.IsActive = request.Active; template.TemplateVersion++; template.UpdatedAtUtc = DateTimeOffset.UtcNow; template.UpdatedByUserId = user.UserId;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _auditService.RecordAsync(new AuditEventRequest(user.UserId, "pdf_template.updated", AuditModules.Pdfs, "PdfTemplate", template.Code, previous, template.HtmlTemplate, Severity: AuditSeverity.Medium, Reason: request.Reason), cancellationToken);
        return ToResponse(template);
    }

    public async Task<PdfPreviewResponse?> PreviewAsync(PdfPreviewRequest request, CancellationToken cancellationToken)
    {
        var template = await _dbContext.PdfTemplates.AsNoTracking().SingleOrDefaultAsync(item => item.Code == request.TemplateId, cancellationToken);
        if (template is null) return null;
        var html = RenderTemplate(template.HtmlTemplate, DefaultPreviewData(request.Data));
        return new PdfPreviewResponse(template.Code, html, html.Replace("<", " <").Replace(">", "> "));
    }

    internal async Task<PdfTemplateEntity> ResolveActiveAsync(string code, CancellationToken cancellationToken)
    {
        await EnsureDefaultTemplateAsync(cancellationToken);
        return await _dbContext.PdfTemplates.SingleOrDefaultAsync(item => item.Code == code && item.IsActive, cancellationToken)
            ?? await _dbContext.PdfTemplates.SingleAsync(item => item.Code == "alert-default" && item.IsActive, cancellationToken);
    }

    internal static string RenderTemplate(string template, IReadOnlyDictionary<string, string?> data)
    {
        return PlaceholderPattern.Replace(template, match => data.TryGetValue(match.Groups[1].Value, out var value) ? value ?? string.Empty : match.Value);
    }

    private async Task EnsureDefaultTemplateAsync(CancellationToken cancellationToken)
    {
        if (!_defaults.CreateDefaultPdfTemplate) return;
        if (await _dbContext.PdfTemplates.AnyAsync(item => item.Code == "alert-default", cancellationToken)) return;
        _dbContext.PdfTemplates.Add(new PdfTemplateEntity
        {
            Code = "alert-default", Name = "Alerta CMMS", EventType = "alert", SubjectTemplate = "[CMMS] {{Title}}",
            HtmlTemplate = "<html><body><h1>{{Title}}</h1><p><strong>Severidad:</strong> {{Severity}}</p><p><strong>Origen:</strong> {{Source}}</p><p><strong>Entidad:</strong> {{EntityId}}</p><p><strong>Faena:</strong> {{FaenaCodigo}}</p><p>{{Message}}</p><p><small>Generado: {{CreatedAtUtc}}</small></p></body></html>",
            IsActive = true, TemplateVersion = 1, CreatedByUserId = "system"
        });
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private void EnsureCanConfigure(UserAccessContext user)
    {
        if (!_authorizationPolicyService.CanConfigureAlerts(user)) throw new UnauthorizedAccessException("El usuario no tiene permiso para configurar plantillas.");
    }

    internal static bool PlaceholdersAreValid(string template)
    {
        if (template.Contains("{{", StringComparison.Ordinal) && PlaceholderPattern.Matches(template).Count == 0) return false;
        var stripped = PlaceholderPattern.Replace(template, string.Empty);
        return !stripped.Contains("{{", StringComparison.Ordinal) && !stripped.Contains("}}", StringComparison.Ordinal);
    }
    private static void ValidatePlaceholders(string template)
    {
        if (template.Contains("{{", StringComparison.Ordinal) && PlaceholderPattern.Matches(template).Count == 0) throw new DomainException("La plantilla contiene placeholders invalidos.");
        var stripped = PlaceholderPattern.Replace(template, string.Empty);
        if (stripped.Contains("{{", StringComparison.Ordinal) || stripped.Contains("}}", StringComparison.Ordinal)) throw new DomainException("La plantilla contiene placeholders invalidos.");
    }

    private static IReadOnlyDictionary<string, string?> DefaultPreviewData(IReadOnlyDictionary<string, string?>? data) => new Dictionary<string, string?>(data ?? new Dictionary<string, string?>(), StringComparer.OrdinalIgnoreCase)
    {
        ["Title"] = data?.GetValueOrDefault("Title") ?? "Documento por vencer", ["Message"] = data?.GetValueOrDefault("Message") ?? "El documento vence dentro del plazo configurado.", ["Severity"] = data?.GetValueOrDefault("Severity") ?? "Warning", ["Source"] = data?.GetValueOrDefault("Source") ?? "Documentos", ["EntityId"] = data?.GetValueOrDefault("EntityId") ?? "EQ-001", ["FaenaCodigo"] = data?.GetValueOrDefault("FaenaCodigo") ?? "F001", ["CreatedAtUtc"] = data?.GetValueOrDefault("CreatedAtUtc") ?? DateTimeOffset.UtcNow.ToString("O")
    };

    private static PdfTemplateResponse ToResponse(PdfTemplateEntity item) => new(item.Code, item.Name, item.EventType, item.SubjectTemplate, item.HtmlTemplate, item.IsActive, item.UpdatedAtUtc ?? item.CreatedAtUtc);
}