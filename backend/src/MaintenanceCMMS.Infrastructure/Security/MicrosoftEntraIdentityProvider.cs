using MaintenanceCMMS.Application.Auth;

namespace MaintenanceCMMS.Infrastructure.Security;

public sealed class MicrosoftEntraIdentityProvider : IExternalIdentityProvider
{
    public Task<ExternalIdentityResult?> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        // Placeholder intencional: la primera version usa usuarios propios.
        return Task.FromResult<ExternalIdentityResult?>(null);
    }
}
