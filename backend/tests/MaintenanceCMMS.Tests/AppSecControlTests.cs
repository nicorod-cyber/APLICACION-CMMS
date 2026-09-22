using System.Net;
using System.Security.Cryptography;
using System.Text;
using MaintenanceCMMS.Api.Security;
using MaintenanceCMMS.Application.Storage;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Infrastructure.Alerts;
using MaintenanceCMMS.Infrastructure.Options;
using MaintenanceCMMS.Infrastructure.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace MaintenanceCMMS.Tests;

public sealed class AppSecControlTests
{
    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("..\\outside.txt")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\Windows\\win.ini")]
    [InlineData("\\\\server\\share\\file")]
    [InlineData("%2e%2e%2fsecret")]
    [InlineData("%252e%252e%255csecret")]
    public void StoragePaths_RejectEscapes(string path) => Assert.Throws<DomainException>(() => StoragePathPolicy.Resolve(Path.GetTempPath(), path));

    [Theory]
    [InlineData("CON.pdf")]
    [InlineData("LPT1.png")]
    [InlineData("AUX.txt")]
    public void ReservedNames_AreRejected(string name) => Assert.Throws<DomainException>(() => StoragePathPolicy.SafeFileName(name));

    [Theory]
    [InlineData("https://trusted.sharepoint.com.evil.test/file")]
    [InlineData("https://trusted.sharepoint.com@evil.test/file")]
    [InlineData("https://127.0.0.1/file")]
    [InlineData("https://[::1]/file")]
    [InlineData("https://trusted.sharepoint.com\r\n/file")]
    public void UrlAllowlist_RejectsHostConfusion(string url) => Assert.Throws<DomainException>(() => DocumentUrlPolicy.RequireHttps(url, ["trusted.sharepoint.com"]));

    [Fact]
    public void UploadPolicy_DetectsSpoofingAndAcceptsRequiredContent()
    {
        UploadPolicy.Validate("evidence.png", "image/png", SecurityTestFiles.Png, imagesOnly: true);
        UploadPolicy.Validate("document.pdf", "application/pdf", SecurityTestFiles.Pdf);
        Assert.Throws<DomainException>(() => UploadPolicy.Validate("image.jpg", "image/jpeg", SecurityTestFiles.Png));
        Assert.Throws<DomainException>(() => UploadPolicy.Validate("image.png", "application/pdf", SecurityTestFiles.Png));
        Assert.Throws<DomainException>(() => UploadPolicy.Validate("document.pdf", "application/pdf", SecurityTestFiles.Pdf, imagesOnly: true));
        Assert.Throws<DomainException>(() => UploadPolicy.Validate("image.png", "image/png", new byte[UploadPolicy.MaximumBytes + 1]));
    }

    [Fact]
    public void PasswordHashes_UpgradeWithoutLosingLegacyVerification()
    {
        const string password = "Legacy.Compatible2026!";
        var hasher = new PasswordHasher(); var salt = RandomNumberGenerator.GetBytes(16);
        var bytes = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
        var legacy = $"PBKDF2-SHA256$100000${Convert.ToBase64String(salt)}${Convert.ToBase64String(bytes)}";
        Assert.True(hasher.Verify(password, legacy)); Assert.True(PasswordHasher.NeedsRehash(legacy));
        var modern = hasher.Hash(password); Assert.StartsWith("PBKDF2-SHA256$600000$", modern);
        Assert.True(hasher.Verify(password, modern)); Assert.False(PasswordHasher.NeedsRehash(modern));
        Assert.NotEqual(modern, hasher.Hash(password));
        foreach (var count in new[] { "-1", "0", "2147483647", "invalid" })
            Assert.False(hasher.Verify(password, legacy.Replace("100000", count)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    public void JwtConfiguration_FailsClosed(string secret) => Assert.Throws<InvalidOperationException>(() => new JwtTokenService(Options.Create(new JwtOptions { Secret = secret })));

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("<svg onload=alert(1)>")]
    [InlineData("<p onclick=alert(1)>test</p><a href='javascript:alert(1)'>click</a>")]
    public void TemplateStructure_DropsActiveContent(string payload)
    {
        var result = SafeTemplateHtml.Sanitize("<h1>Title</h1>" + payload);
        Assert.Contains("<h1>Title</h1>", result);
        Assert.DoesNotContain("<script", result); Assert.DoesNotContain("onerror", result); Assert.DoesNotContain("onclick", result); Assert.DoesNotContain("<svg", result); Assert.DoesNotContain("javascript:", result);
    }

    [Fact]
    public async Task LoginLimiter_Enforces429AndDoesNotTrustForwardedHeader()
    {
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Security:LoginPermitLimit"] = "2" });
        builder.Services.AddLoginRateLimiting(builder.Configuration);
        await using var app = builder.Build(); app.UseRateLimiter();
        app.MapPost("/api/auth/login", () => Microsoft.AspNetCore.Http.Results.Unauthorized()).RequireRateLimiting("login");
        await app.StartAsync(); var client = app.GetTestClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/auth/login", null)).StatusCode);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.5");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/auth/login", null)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Forwarded-For"); client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.6");
        var response = await client.PostAsync("/api/auth/login", null);
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode); Assert.NotNull(response.Headers.RetryAfter);
    }
}
