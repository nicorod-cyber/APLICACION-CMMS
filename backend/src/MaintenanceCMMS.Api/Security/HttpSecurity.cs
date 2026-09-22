using System.Threading.RateLimiting;
using MaintenanceCMMS.Application.Storage;
using Microsoft.AspNetCore.Http.Features;

namespace MaintenanceCMMS.Api.Security;

public static class HttpSecurity
{
    public static void AddLoginRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var permits = Math.Clamp(configuration.GetValue("Security:LoginPermitLimit", 20), 1, 100);
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = "60";
                await context.HttpContext.Response.WriteAsJsonAsync(new { message = "Demasiados intentos. Intente nuevamente mas tarde.", traceId = context.HttpContext.TraceIdentifier }, ct);
            };
            options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = permits, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
        services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = UploadPolicy.MaximumBytes;
            options.ValueLengthLimit = 64 * 1024;
            options.MultipartHeadersLengthLimit = 16 * 1024;
        });
    }
}

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.XContentTypeOptions = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            context.Response.Headers["Content-Security-Policy"] = "frame-ancestors 'none'; base-uri 'self'; object-src 'none'";
            if (context.Request.Path.StartsWithSegments("/api")) context.Response.Headers.CacheControl = "no-store";
            return Task.CompletedTask;
        });
        await next(context);
    }
}

public sealed class UploadValidationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true && context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync(context.RequestAborted);
            if (form.Files.Count > 1) throw new MaintenanceCMMS.Domain.Common.DomainException("Adjunte un archivo por solicitud.");
            foreach (var file in form.Files)
            {
                if (file.Length > UploadPolicy.MaximumBytes) throw new MaintenanceCMMS.Domain.Common.DomainException("El archivo supera 20 MB.");
                await using var source = file.OpenReadStream();
                using var output = new MemoryStream();
                await source.CopyToAsync(output, context.RequestAborted);
                var path = context.Request.Path.Value ?? string.Empty;
                var image = path.EndsWith("/evidences", StringComparison.Ordinal) || path.EndsWith("/signatures", StringComparison.Ordinal);
                UploadPolicy.Validate(file.FileName, file.ContentType, output.ToArray(), image,
                    path.EndsWith("/signatures", StringComparison.Ordinal) ? 2 * 1024 * 1024 : image ? 10 * 1024 * 1024 : UploadPolicy.MaximumBytes);
            }
        }
        await next(context);
    }
}
