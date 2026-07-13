namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;

public sealed class PdfTemplateEntity : PostgreSqlEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string SubjectTemplate { get; set; } = string.Empty;
    public string HtmlTemplate { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int TemplateVersion { get; set; } = 1;
    public string? CreatedByUserId { get; set; }
    public string? UpdatedByUserId { get; set; }
    public Guid? FileId { get; set; }
    public FileMetadataEntity? File { get; set; }
}

public sealed class AlertRuleEntity : PostgreSqlEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public string Severity { get; set; } = string.Empty;
    public bool RepeatUntilResolved { get; set; }
    public bool GenerateEmail { get; set; }
    public bool GeneratePdf { get; set; }
    public Guid TemplateId { get; set; }
    public PdfTemplateEntity Template { get; set; } = null!;
    public Guid? FaenaId { get; set; }
    public FaenaEntity? Faena { get; set; }
    public string? CreatedByUserId { get; set; }
    public string? UpdatedByUserId { get; set; }
    public List<AlertRuleRecipientEntity> Recipients { get; set; } = [];
}

public sealed class AlertRuleRecipientEntity : PostgreSqlEntity
{
    public Guid AlertRuleId { get; set; }
    public AlertRuleEntity AlertRule { get; set; } = null!;
    public Guid? UserId { get; set; }
    public AppUserEntity? User { get; set; }
    public Guid? RoleId { get; set; }
    public RoleEntity? Role { get; set; }
    public string? Destination { get; set; }
    public string Channel { get; set; } = "Email";
    public bool IsActive { get; set; } = true;
}

public sealed class AlertEntity : PostgreSqlEntity
{
    public Guid AlertRuleId { get; set; }
    public AlertRuleEntity AlertRule { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string CauseKey { get; set; } = string.Empty;
    public string DeduplicationKey { get; set; } = string.Empty;
    public Guid? FaenaId { get; set; }
    public FaenaEntity? Faena { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public bool IsCriticalRepeat { get; set; }
    public int RepeatCount { get; set; } = 1;
    public DateTimeOffset? AcknowledgedAtUtc { get; set; }
    public string? AcknowledgedByUserId { get; set; }
    public DateTimeOffset? ResolvedAtUtc { get; set; }
    public string? ResolvedByUserId { get; set; }
    public string? ResolutionReason { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? GeneratedPdfFileId { get; set; }
    public FileMetadataEntity? GeneratedPdfFile { get; set; }
    public List<NotificationEntity> Notifications { get; set; } = [];
}

public sealed class NotificationEntity : PostgreSqlEntity
{
    public Guid AlertId { get; set; }
    public AlertEntity Alert { get; set; } = null!;
    public string Channel { get; set; } = "Email";
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? ScheduledAtUtc { get; set; }
    public DateTimeOffset? SentAtUtc { get; set; }
    public int AttemptCount { get; set; }
    public string? Provider { get; set; }
    public string? LastError { get; set; }
    public Guid? PdfFileId { get; set; }
    public FileMetadataEntity? PdfFile { get; set; }
    public List<NotificationRecipientEntity> Recipients { get; set; } = [];
    public List<NotificationAttemptEntity> Attempts { get; set; } = [];
}

public sealed class NotificationRecipientEntity : PostgreSqlEntity
{
    public Guid NotificationId { get; set; }
    public NotificationEntity Notification { get; set; } = null!;
    public Guid? UserId { get; set; }
    public AppUserEntity? User { get; set; }
    public Guid? RoleId { get; set; }
    public RoleEntity? Role { get; set; }
    public string? Destination { get; set; }
    public string DeliveryStatus { get; set; } = "Pending";
}

public sealed class NotificationAttemptEntity : PostgreSqlEntity
{
    public Guid NotificationId { get; set; }
    public NotificationEntity Notification { get; set; } = null!;
    public int AttemptNumber { get; set; }
    public DateTimeOffset AttemptedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public bool Success { get; set; }
    public string? Provider { get; set; }
    public string? Error { get; set; }
}
