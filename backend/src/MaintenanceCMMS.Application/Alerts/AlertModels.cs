namespace MaintenanceCMMS.Application.Alerts;

public enum AlertSeverityLevel
{
    Info = 0,
    Warning = 1,
    Critical = 2
}

public enum AlertStatus
{
    Open = 0,
    Acknowledged = 1,
    Resolved = 2
}

public enum NotificationStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2
}

public sealed record AlertQuery(
    AlertStatus? Status = null,
    AlertSeverityLevel? Severity = null,
    string? Source = null,
    string? FaenaCodigo = null,
    bool IncludeResolved = false);

public sealed record AlertResponse(
    string AlertId,
    string RuleCode,
    string Title,
    string Message,
    AlertSeverityLevel Severity,
    AlertStatus Status,
    string Source,
    string CauseKey,
    string? FaenaCodigo,
    string? EntityType,
    string? EntityId,
    bool IsCriticalRepeat,
    int RepeatCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? AcknowledgedAtUtc,
    string? AcknowledgedBy,
    DateTimeOffset? ResolvedAtUtc,
    string? ResolvedBy,
    string? ResolutionReason);

public sealed record AlertRuleResponse(
    string Code,
    string Name,
    string EventType,
    bool Enabled,
    AlertSeverityLevel Severity,
    bool RepeatUntilResolved,
    bool GenerateEmail,
    bool GeneratePdf,
    string TemplateId,
    string Recipients,
    string? FaenaCodigo);

public sealed record UpdateAlertRuleRequest(
    string Name,
    string EventType,
    bool Enabled,
    AlertSeverityLevel Severity,
    bool RepeatUntilResolved,
    bool GenerateEmail,
    bool GeneratePdf,
    string TemplateId,
    string Recipients,
    string? FaenaCodigo = null,
    string? Reason = null);

public sealed record GenerateAlertRequest(
    string RuleCode,
    string Title,
    string Message,
    string Source,
    string CauseKey,
    string? FaenaCodigo = null,
    string? EntityType = null,
    string? EntityId = null,
    IReadOnlyDictionary<string, string?>? Data = null);

public sealed record SendTestNotificationRequest(
    string? RecipientEmail = null,
    string? Comments = null);

public sealed record ResolveAlertRequest(string Reason);

public sealed record NotificationResponse(
    string NotificationId,
    string AlertId,
    string Subject,
    string Body,
    string Recipients,
    NotificationStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? SentAtUtc,
    string? Provider,
    string? PdfFileKey,
    string? PdfPath,
    string? Error);

public sealed record PdfTemplateResponse(
    string TemplateId,
    string Name,
    string EventType,
    string SubjectTemplate,
    string HtmlTemplate,
    bool Active,
    DateTimeOffset UpdatedAtUtc);

public sealed record UpdatePdfTemplateRequest(
    string Name,
    string EventType,
    string SubjectTemplate,
    string HtmlTemplate,
    bool Active = true,
    string? Reason = null);

public sealed record PdfPreviewRequest(
    string TemplateId,
    IReadOnlyDictionary<string, string?>? Data = null);

public sealed record PdfPreviewResponse(
    string TemplateId,
    string Html,
    string TextPreview);
