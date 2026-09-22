using System.Net;
using System.Net.Http.Json;
using AngleSharp.Html.Parser;
using MaintenanceCMMS.Application.Alerts;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Infrastructure.Data.SqlServer.Entities;
using Xunit;

namespace MaintenanceCMMS.Tests;

public sealed class AppSecRenderingHttpTests : IClassFixture<SecurityApiFixture>
{
    private readonly SecurityApiFixture fixture;
    public AppSecRenderingHttpTests(SecurityApiFixture fixture) => this.fixture = fixture;

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("<svg onload=alert(1)>")]
    [InlineData("\" onclick=\"alert(1)")]
    public async Task Preview_EncodesDataAndSanitizesExistingTemplate(string payload)
    {
        var account = await fixture.CreateUserAsync([AuthRoles.Planner], ["FAE-1"]);
        using var client = await fixture.LoginAsync(account.Username);
        var code = "security-" + Guid.NewGuid().ToString("N");
        await using var db = fixture.Database.NewContext();
        db.PdfTemplates.Add(new PdfTemplateEntity { Code = code, Name = "Security template", EventType = "alert", SubjectTemplate = "{{Title}}", HtmlTemplate = "<h1>{{Title}}</h1><p>{{Message}}</p><img src=x onerror=alert(1)><script>alert(1)</script>", IsActive = true, CreatedByUserId = account.Id });
        await db.SaveChangesAsync();
        var response = await client.PostAsJsonAsync($"/api/pdf/templates/{code}/preview", new Dictionary<string, string?> { ["Title"] = payload, ["Message"] = payload });
        response.EnsureSuccessStatusCode();
        var preview = await response.Content.ReadFromJsonAsync<PdfPreviewResponse>();
        var document = new HtmlParser().ParseDocument(preview!.Html);
        Assert.Equal(payload, document.QuerySelector("h1")!.TextContent);
        Assert.Equal(payload, document.QuerySelector("p")!.TextContent);
        Assert.Null(document.QuerySelector("script,img,svg,[onclick],[onerror]"));
    }

    [Fact]
    public async Task ReadOnlyUser_CannotUploadOrCreateUsers()
    {
        var user = await fixture.CreateUserAsync([AuthRoles.FaenaViewer], ["FAE-1"]);
        using var client = await fixture.LoginAsync(user.Username);
        var response = await client.PostAsJsonAsync("/api/sharepoint/files/manual-link", new { module = "Documents", entityType = "Activo", entityId = "ACT-1", fileName = "file.pdf", url = "https://example.com/file.pdf" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/users")).StatusCode);
    }

    [Fact]
    public async Task ImportPath_CannotReadOutsideConfiguredRoot()
    {
        var user = await fixture.CreateUserAsync([AuthRoles.Admin], []);
        using var client = await fixture.LoginAsync(user.Username);
        var response = await client.PostAsJsonAsync("/api/imports/sharepoint-files", new { excelPath = "../../appsettings.json" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("appsettings.json", body);
        Assert.Contains("traceId", body);
    }
}
