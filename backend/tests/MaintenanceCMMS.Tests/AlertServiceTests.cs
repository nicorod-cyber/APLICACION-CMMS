using MaintenanceCMMS.Application.Abstractions.Data;
using MaintenanceCMMS.Application.Alerts;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Infrastructure.Alerts;
using MaintenanceCMMS.Infrastructure.Auditing;
using MaintenanceCMMS.Infrastructure.Data.Excel;
using MaintenanceCMMS.Infrastructure.Options;
using MaintenanceCMMS.Infrastructure.Security;
using MaintenanceCMMS.Infrastructure.SharePoint;
using Microsoft.Extensions.Options;
using Xunit;

namespace MaintenanceCMMS.Tests;

public sealed class AlertServiceTests
{
    private static readonly UserAccessContext Admin = new(
        "admin",
        [AuthRoles.Admin],
        [AuthPermissions.Administration, AuthPermissions.ManageAlerts, AuthPermissions.ConfigureAlerts],
        []);

    [Fact]
    public async Task GenerateAsync_CreatesAlertAndNotification()
    {
        await using var fixture = await CreateFixtureAsync();

        var alert = await fixture.AlertService.GenerateAsync(AlertRequest("document-expiring", "DOC-1"), Admin, CancellationToken.None);
        var notifications = await fixture.AlertService.ListNotificationsAsync(Admin, CancellationToken.None);

        Assert.Equal(AlertStatus.Open, alert.Status);
        Assert.Equal(AlertSeverityLevel.Warning, alert.Severity);
        Assert.Contains(notifications, item => item.AlertId == alert.AlertId && item.Status == NotificationStatus.Sent);
    }

    [Fact]
    public async Task DevelopmentEmailService_ReturnsSentWithoutExternalServer()
    {
        await using var fixture = await CreateFixtureAsync();

        var alert = await fixture.AlertService.GenerateAsync(AlertRequest("document-expiring", "DOC-2"), Admin, CancellationToken.None);
        var notification = await fixture.AlertService.SendTestAsync(
            alert.AlertId,
            new SendTestNotificationRequest("test@example.local", "Prueba"),
            Admin,
            CancellationToken.None);

        Assert.NotNull(notification);
        Assert.Equal(NotificationStatus.Sent, notification!.Status);
        Assert.Equal("Development", notification.Provider);
    }

    [Fact]
    public async Task PdfService_GeneratesPdfFile()
    {
        await using var fixture = await CreateFixtureAsync();

        var result = await fixture.PdfService.RenderAsync(new PdfRenderRequest(
            "alert-default",
            "<h1>Alerta CMMS</h1><p>Documento vencido</p>",
            "alerta-test.pdf",
            new Dictionary<string, string?>()), CancellationToken.None);

        Assert.True(File.Exists(result.Path));
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(result.Content));
    }

    [Fact]
    public async Task GenerateAsync_RepeatsCriticalOpenAlertUntilResolved()
    {
        await using var fixture = await CreateFixtureAsync();

        var first = await fixture.AlertService.GenerateAsync(AlertRequest("document-expired", "DOC-3"), Admin, CancellationToken.None);
        var second = await fixture.AlertService.GenerateAsync(AlertRequest("document-expired", "DOC-3"), Admin, CancellationToken.None);
        var open = await fixture.AlertService.ListAsync(new AlertQuery(), Admin, CancellationToken.None);

        Assert.Equal(first.AlertId, second.AlertId);
        Assert.Equal(2, second.RepeatCount);
        Assert.Single(open.Where(item => item.CauseKey == "DOC-3"));
    }

    private static GenerateAlertRequest AlertRequest(string ruleCode, string causeKey)
    {
        return new GenerateAlertRequest(
            ruleCode,
            "Documento vencido",
            "El documento requiere revision.",
            "Documentos",
            causeKey,
            "FAE-1",
            "Activo",
            "ACT-1");
    }

    private static async Task<AlertFixture> CreateFixtureAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "maintenance-cmms-alert-tests", Guid.NewGuid().ToString("N"));
        var database = await PostgreSqlWorkTestFixture.CreateAsync();
        var auditService = new PostgreSqlAuditService(database.DbContext, new AuditContextAccessor());
        var authorization = new AuthorizationPolicyService();
        var mailOptions = Options.Create(new MailOptions
        {
            Provider = "Development",
            From = "cmms@example.local",
            PlanningEmail = "planificacion@example.local"
        });
        var pdfOptions = Options.Create(new PdfOptions
        {
            TemplatePath = Path.Combine(root, "templates")
        });
        var sharePointOptions = Options.Create(new SharePointOptions
        {
            Provider = "LocalSimulation",
            LocalPath = Path.Combine(root, "sharepoint")
        });

        var emailService = new EmailService(mailOptions);
        var storageService = new LocalSharePointSimulationService(database.DbContext, auditService, sharePointOptions);
        var pdfService = new PdfService(pdfOptions, storageService);
        var templateService = new PdfTemplateService(database.DbContext, auditService, authorization);
        var alertService = new AlertService(database.DbContext, auditService, authorization, emailService, pdfService, templateService);

        return new AlertFixture(database, alertService, pdfService);
    }

    private sealed record AlertFixture(
        PostgreSqlWorkTestFixture Database,
        IAlertService AlertService,
        IPdfService PdfService) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Database.DisposeAsync();
    }
}
