using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Domain.Enums;

namespace MaintenanceCMMS.Domain.Alerts;

public sealed class Alert : AuditableEntity
{
    public Alert(string title, AlertSeverity severity, string source)
    {
        DomainGuard.AgainstEmpty(title, nameof(title));
        DomainGuard.AgainstEmpty(source, nameof(source));
        Title = title.Trim();
        Severity = severity;
        Source = source.Trim();
    }

    public string Title { get; private set; }

    public AlertSeverity Severity { get; private set; }

    public string Source { get; private set; }

    public bool IsAcknowledged { get; private set; }
}

public sealed class Notification : AuditableEntity
{
    public Notification(EntityId alertId, string subject, string body)
    {
        DomainGuard.AgainstEmpty(subject, nameof(subject));
        DomainGuard.AgainstEmpty(body, nameof(body));
        AlertId = alertId;
        Subject = subject.Trim();
        Body = body.Trim();
    }

    public EntityId AlertId { get; private set; }

    public string Subject { get; private set; }

    public string Body { get; private set; }
}

public sealed class NotificationRecipient : AuditableEntity
{
    public NotificationRecipient(EntityId notificationId, string email)
    {
        DomainGuard.AgainstEmpty(email, nameof(email));
        NotificationId = notificationId;
        Email = email.Trim().ToLowerInvariant();
    }

    public EntityId NotificationId { get; private set; }

    public string Email { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }
}

public sealed class NotificationPdfRecord : AuditableEntity
{
    public NotificationPdfRecord(EntityId notificationId, string pdfFileKey)
    {
        DomainGuard.AgainstEmpty(pdfFileKey, nameof(pdfFileKey));
        NotificationId = notificationId;
        PdfFileKey = pdfFileKey.Trim();
    }

    public EntityId NotificationId { get; private set; }

    public string PdfFileKey { get; private set; }
}

