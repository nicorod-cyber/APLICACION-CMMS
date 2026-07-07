using MaintenanceCMMS.Application.Auth;

namespace MaintenanceCMMS.Application.Alerts;

public interface IAlertService
{
    Task<IReadOnlyCollection<AlertResponse>> ListAsync(
        AlertQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<AlertResponse> GenerateAsync(
        GenerateAlertRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<AlertResponse?> AcknowledgeAsync(
        string id,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<AlertResponse?> ResolveAsync(
        string id,
        ResolveAlertRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<NotificationResponse?> SendTestAsync(
        string id,
        SendTestNotificationRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<NotificationResponse>> ListNotificationsAsync(
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AlertRuleResponse>> ListRulesAsync(
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<AlertRuleResponse?> UpdateRuleAsync(
        string code,
        UpdateAlertRuleRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);
}

public interface IEmailService
{
    Task<EmailSendResult> SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken);
}

public interface IPdfService
{
    Task<PdfRenderResult> RenderAsync(
        PdfRenderRequest request,
        CancellationToken cancellationToken);
}

public interface IPdfTemplateService
{
    Task<IReadOnlyCollection<PdfTemplateResponse>> ListAsync(CancellationToken cancellationToken);

    Task<PdfTemplateResponse?> UpdateAsync(
        string id,
        UpdatePdfTemplateRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<PdfPreviewResponse?> PreviewAsync(
        PdfPreviewRequest request,
        CancellationToken cancellationToken);
}

public sealed record EmailMessage(
    string Subject,
    string HtmlBody,
    IReadOnlyCollection<string> Recipients,
    string? PdfFileKey = null,
    string? PdfPath = null);

public sealed record EmailSendResult(
    bool Success,
    string Provider,
    DateTimeOffset? SentAtUtc,
    string? Error = null);

public sealed record PdfRenderRequest(
    string TemplateId,
    string Html,
    string FileName,
    IReadOnlyDictionary<string, string?> Data);

public sealed record PdfRenderResult(
    string FileKey,
    string Path,
    byte[] Content);
