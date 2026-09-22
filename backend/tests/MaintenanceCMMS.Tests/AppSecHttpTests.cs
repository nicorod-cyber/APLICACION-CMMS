using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.Storage;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Infrastructure.Data.SqlServer.Entities;
using MaintenanceCMMS.Infrastructure.Options;
using MaintenanceCMMS.Infrastructure.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace MaintenanceCMMS.Tests;

public sealed class AppSecHttpTests : IClassFixture<SecurityApiFixture>
{
    private readonly SecurityApiFixture fixture;
    public AppSecHttpTests(SecurityApiFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task ProtectedEndpoints_RejectAnonymousRequests()
    {
        using var client = fixture.Factory.CreateClient();
        foreach (var url in new[] { "/api/assets", "/api/sharepoint/files", "/api/sharepoint/download?fileKey=arbitrary", "/api/auth/me" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(url)).StatusCode);
    }

    [Theory]
    [InlineData("logout")]
    [InlineData("password")]
    [InlineData("disabled")]
    [InlineData("locked")]
    [InlineData("roles")]
    public async Task IssuedToken_IsRejectedAfterSecurityStateChanges(string operation)
    {
        var account = await fixture.CreateUserAsync([AuthRoles.Planner], ["FAE-1"]);
        using var client = await fixture.LoginAsync(account.Username);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
        if (operation == "logout") Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        else if (operation == "password") Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest(SecurityApiFixture.Password, "Changed.Secure2026!", "Changed.Secure2026!"))).StatusCode);
        else
        {
            await using var db = fixture.Database.NewContext();
            var store = new SqlServerIdentityStore(db);
            await store.UpsertUserAsync(account with { IsActive = operation != "disabled", IsLocked = operation == "locked", Roles = operation == "roles" ? [AuthRoles.FaenaViewer] : account.Roles }, CancellationToken.None);
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/assets")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/auth/refresh", null)).StatusCode);
    }

    [Fact]
    public async Task Login_UsesSameResponseForMissingWrongAndLockedAccounts()
    {
        var user = await fixture.CreateUserAsync([AuthRoles.Planner], ["FAE-1"]);
        await using (var db = fixture.Database.NewContext()) await db.Users.Where(x => x.Id.ToString() == user.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsLocked, true));
        using var client = fixture.Factory.CreateClient();
        foreach (var request in new[] { new LoginRequest("missing", SecurityApiFixture.Password), new LoginRequest(user.Username, "wrong"), new LoginRequest(user.Username, SecurityApiFixture.Password), new LoginRequest("' OR 1=1--", SecurityApiFixture.Password) })
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", request);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task FileKeyAndAssetId_CannotCrossFaena_WithoutFaenaHeader()
    {
        var user = await fixture.CreateUserAsync([AuthRoles.Planner], ["FAE-1"]);
        await using var db = fixture.Database.NewContext();
        var site = new FaenaEntity { Code = "SEC-OTHER", Name = "Other", IsActive = true };
        db.Faenas.Add(site);
        var asset = await db.Assets.FirstAsync();
        var other = new AssetEntity { Code = "SEC-ASSET", Name = "Other asset", Faena = site, AssetTypeId = asset.AssetTypeId, FamilyId = asset.FamilyId, OperationalStateId = asset.OperationalStateId };
        db.Assets.Add(other);
        db.Files.Add(new FileMetadataEntity { FileKey = "other-site.pdf", FileName = "other.pdf", Module = "Documents", EntityType = "Activo", EntityId = other.Code, AssetCode = other.Code, FaenaCode = site.Code, LogicalUri = "https://example.com/other.pdf", Provider = "ManualLink", StorageMode = "ManualLink", Purpose = "Document", Status = "ManualLink", AuthorUserId = "another-user" });
        await db.SaveChangesAsync();
        using var client = await fixture.LoginAsync(user.Username);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/sharepoint/download?fileKey=other-site.pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/sharepoint/link?fileKey=other-site.pdf")).StatusCode);
        Assert.DoesNotContain("other-site.pdf", await client.GetStringAsync("/api/sharepoint/files"));
        var request = new { module = "Documents", entityType = "Activo", entityId = other.Code, fileName = "document.pdf", url = "https://example.com/file.pdf", faenaCodigo = "FAE-1", activoCodigo = "ACT-1" };
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/sharepoint/files/manual-link", request)).StatusCode);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("file:///etc/passwd")]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("http://example.com/file.pdf")]
    public async Task ManualLink_RejectsUnsafeSchemes(string url)
    {
        var user = await fixture.CreateUserAsync([AuthRoles.Admin], []);
        using var client = await fixture.LoginAsync(user.Username);
        var response = await client.PostAsJsonAsync("/api/sharepoint/files/manual-link", new { module = "Documents", entityType = "Activo", entityId = "ACT-1", fileName = "file.pdf", url });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("payload.png", "image/png", "<svg onload=alert(1)>")]
    [InlineData("payload.pdf", "application/pdf", "<script>alert(1)</script>")]
    [InlineData("payload.exe", "image/png", "MZ executable")]
    [InlineData("payload.svg", "image/svg+xml", "<svg onload=alert(1)>")]
    public async Task Upload_RejectsSpoofedContentBeforePersistence(string filename, string mime, string payload)
    {
        var user = await fixture.CreateUserAsync([AuthRoles.Admin], []);
        using var client = await fixture.LoginAsync(user.Username);
        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes(payload)); content.Headers.ContentType = new MediaTypeHeaderValue(mime);
        form.Add(content, "file", filename); form.Add(new StringContent("Activo"), "entityType"); form.Add(new StringContent("ACT-1"), "entityId");
        var response = await client.PostAsync("/api/sharepoint/files/upload", form);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("traceId", await response.Content.ReadAsStringAsync());
        Assert.False(Directory.Exists(fixture.StorageRoot) && Directory.EnumerateFiles(fixture.StorageRoot, "*", SearchOption.AllDirectories).Any(p => Path.GetFileName(p).Contains(filename)));
    }

    [Fact]
    public async Task ValidUpload_CanDownload_WithNoPhysicalPathDisclosure()
    {
        var user = await fixture.CreateUserAsync([AuthRoles.Planner], ["FAE-1"]);
        using var client = await fixture.LoginAsync(user.Username);
        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(SecurityTestFiles.Png); content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(content, "file", "evidence.png"); form.Add(new StringContent("Activo"), "entityType"); form.Add(new StringContent("ACT-1"), "entityId");
        var response = await client.PostAsync("/api/sharepoint/files/upload", form);
        response.EnsureSuccessStatusCode();
        var file = await response.Content.ReadFromJsonAsync<DocumentStorageInfo>(new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } });
        Assert.Null(file!.LocalPath);
        var download = await client.GetAsync("/api/sharepoint/download?fileKey=" + Uri.EscapeDataString(file.FileKey));
        Assert.Equal(SecurityTestFiles.Png, await download.Content.ReadAsByteArrayAsync());
        Assert.Equal("nosniff", download.Headers.GetValues("X-Content-Type-Options").Single());
    }
}

public sealed class SecurityApiFixture : IAsyncLifetime
{
    public const string Password = "Security.Test2026!";
    internal SqlServerWorkTestFixture Database = null!;
    public WebApplicationFactory<Program> Factory = null!;
    public string StorageRoot = Path.Combine(Path.GetTempPath(), "cmms-security", Guid.NewGuid().ToString("N"));
    public async Task InitializeAsync()
    {
        Database = await SqlServerWorkTestFixture.CreateAsync();
        Factory = new SecurityApplicationFactory(new Dictionary<string, string?>
            {
                ["environment"] = "Testing",
                ["DataProvider:SqlServerConnectionString"] = Database.DbContext.Database.GetConnectionString(),
                ["Jwt:Secret"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)),
                ["SharePoint:LocalPath"] = StorageRoot, ["SharePoint:Provider"] = "LocalSimulation",
                ["Auth:SeedAdmin:Username"] = "admin", ["Auth:SeedAdmin:Email"] = "admin@example.test", ["Auth:SeedAdmin:Password"] = Password,
                ["PreventiveMaintenance:JobsEnabled"] = "false", ["DocumentCompliance:JobsEnabled"] = "false", ["Security:LoginPermitLimit"] = "100"
            });
        using var client = Factory.CreateClient();
        (await client.GetAsync("/api/health")).EnsureSuccessStatusCode();
    }
    public async Task<UserAccount> CreateUserAsync(string[] roles, string[] faenas)
    {
        var id = Guid.NewGuid().ToString("D");
        var user = new UserAccount(id, "security-" + id, id + "@example.test", "Security test", true, false, new PasswordHasher().Hash(Password), roles, faenas, DateTimeOffset.UtcNow, null);
        await using var db = Database.NewContext(); await new SqlServerIdentityStore(db).UpsertUserAsync(user, CancellationToken.None);
        return user;
    }
    public async Task<HttpClient> LoginAsync(string username)
    {
        var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, Password)); response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
        return client;
    }
    public async Task DisposeAsync() { await Factory.DisposeAsync(); await Database.DisposeAsync(); }
}

internal static class SecurityTestFiles
{
    public static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aX2kAAAAASUVORK5CYII=");
    public static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n2 0 obj\n<< /Type /Pages /Count 0 /Kids [] >>\nendobj\ntrailer\n<< /Root 1 0 R >>\n%%EOF\n");
}

internal sealed class SecurityApplicationFactory(Dictionary<string, string?> settings) : WebApplicationFactory<Program>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(configuration => configuration.AddInMemoryCollection(settings));
        return base.CreateHost(builder);
    }
}
