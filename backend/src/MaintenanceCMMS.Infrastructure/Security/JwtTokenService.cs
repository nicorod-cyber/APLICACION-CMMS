using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Infrastructure.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace MaintenanceCMMS.Infrastructure.Security;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public LoginResponse CreateToken(UserAccount user, IReadOnlyCollection<string> permissions)
    {
        if (string.IsNullOrWhiteSpace(_options.Secret) || Encoding.UTF8.GetByteCount(_options.Secret) < 32)
        {
            throw new InvalidOperationException("Jwt:Secret must be configured with at least 32 bytes.");
        }

        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(Math.Max(1, _options.ExpirationMinutes));
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email),
            new("display_name", user.DisplayName)
        };

        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));
        claims.AddRange(user.Faenas.Select(faena => new Claim("faena", faena)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new LoginResponse(
            new JwtSecurityTokenHandler().WriteToken(jwt),
            expiresAt,
            AuthResponseFactory.FromUser(user, permissions));
    }
}
