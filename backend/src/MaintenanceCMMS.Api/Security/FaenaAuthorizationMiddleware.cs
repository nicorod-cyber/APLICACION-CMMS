using System.Security.Claims;
using MaintenanceCMMS.Application.Auth;

namespace MaintenanceCMMS.Api.Security;

public sealed class FaenaAuthorizationMiddleware
{
    private readonly RequestDelegate _next;

    public FaenaAuthorizationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IAuthorizationPolicyService authorizationPolicyService)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated != true ||
            !context.Request.Headers.TryGetValue("X-Faena-Codigo", out var values))
        {
            await _next(context);
            return;
        }

        var faenaCodigo = values.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(faenaCodigo))
        {
            await _next(context);
            return;
        }

        var accessContext = UserAccessContext.FromClaims(user);
        if (!authorizationPolicyService.CanViewFaena(accessContext, faenaCodigo))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { message = "El usuario no tiene acceso a la faena solicitada." });
            return;
        }

        await _next(context);
    }
}
