using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.Faenas;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceCMMS.Infrastructure.Faenas;

public sealed class FaenaService : IFaenaService
{
    private readonly CmmsDbContext _dbContext;
    private readonly IAuthorizationPolicyService _authorizationPolicyService;

    public FaenaService(
        CmmsDbContext dbContext,
        IAuthorizationPolicyService authorizationPolicyService)
    {
        _dbContext = dbContext;
        _authorizationPolicyService = authorizationPolicyService;
    }

    public async Task<IReadOnlyCollection<FaenaResponse>> ListAsync(
        FaenaQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        var faenas = await _dbContext.Faenas
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

        return faenas
            .Select(ToResponse)
            .Where(item => query.IncludeInactive || item.Activa)
            .Where(item => _authorizationPolicyService.CanViewFaena(user, item.Codigo))
            .Where(item => MatchesSearch(item, query.Search))
            .OrderBy(item => item.Nombre, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Codigo, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static FaenaResponse ToResponse(FaenaEntity entity)
    {
        var status = entity.IsActive ? "Activa" : "Inactiva";

        return new FaenaResponse(
            entity.Code,
            entity.Name,
            string.Empty,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            status,
            entity.IsActive,
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase));
    }

    private static bool MatchesSearch(FaenaResponse item, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        var value = search.Trim();
        return Contains(item.Codigo, value) ||
               Contains(item.Nombre, value) ||
               Contains(item.Empresa, value) ||
               Contains(item.Region, value) ||
               Contains(item.Comuna, value) ||
               Contains(item.Responsable, value);
    }

    private static bool Contains(string? value, string search)
    {
        return !string.IsNullOrWhiteSpace(value) && value.Contains(search, StringComparison.OrdinalIgnoreCase);
    }
}
