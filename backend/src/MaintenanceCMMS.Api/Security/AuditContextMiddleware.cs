using MaintenanceCMMS.Application.Auditing;

namespace MaintenanceCMMS.Api.Security;

public sealed class AuditContextMiddleware
{
    private readonly RequestDelegate _next;

    public AuditContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IAuditContextAccessor auditContextAccessor)
    {
        auditContextAccessor.Current = new AuditRequestContext(
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.FirstOrDefault(),
            context.TraceIdentifier);

        await _next(context);
    }
}
