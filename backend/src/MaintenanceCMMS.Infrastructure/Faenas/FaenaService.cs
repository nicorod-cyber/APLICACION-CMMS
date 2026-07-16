using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.Faenas;
using MaintenanceCMMS.Domain.Common;
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
        var source = Query().AsNoTracking();
        if (query.Activa.HasValue)
        {
            source = source.Where(faena => faena.IsActive == query.Activa.Value);
        }
        else if (!query.IncludeInactive)
        {
            source = source.Where(faena => faena.IsActive);
        }

        if (_dbContext.Database.IsNpgsql() && !string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            source = source.Where(faena =>
                EF.Functions.ILike(faena.Code, pattern) ||
                EF.Functions.ILike(faena.Name, pattern) ||
                (faena.Zone != null && EF.Functions.ILike(faena.Zone, pattern)) ||
                (faena.Client != null && EF.Functions.ILike(faena.Client, pattern)) ||
                (faena.Region != null && EF.Functions.ILike(faena.Region, pattern)) ||
                (faena.Commune != null && EF.Functions.ILike(faena.Commune, pattern)) ||
                (faena.ResponsibleUser != null && EF.Functions.ILike(faena.ResponsibleUser.DisplayName, pattern)) ||
                (faena.TechnicalLocation != null && EF.Functions.ILike(faena.TechnicalLocation.Code, pattern)) ||
                (faena.TechnicalLocation != null && EF.Functions.ILike(faena.TechnicalLocation.Name, pattern)));
        }

        var faenas = await source
            .OrderBy(faena => faena.Name)
            .ThenBy(faena => faena.Code)
            .ToArrayAsync(cancellationToken);

        return faenas
            .Where(faena => _authorizationPolicyService.CanViewFaena(user, faena.Code))
            .Where(faena => MatchesFilters(faena, query))
            .Select(ToResponse)
            .ToArray();
    }

    public async Task<FaenaResponse?> GetByCodeAsync(
        string code,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        var faena = await Query()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Code == NormalizeCode(code), cancellationToken);

        if (faena is null)
        {
            return null;
        }

        EnsureCanView(user, faena.Code);
        return ToResponse(faena);
    }

    public async Task<FaenaResponse> CreateAsync(
        UpsertFaenaRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var code = NormalizeRequiredCode(request.Codigo, "Codigo");
        if (await _dbContext.Faenas.AnyAsync(item => item.Code == code, cancellationToken))
        {
            throw new DomainException("Ya existe una faena con el codigo indicado.");
        }

        var entity = new FaenaEntity { Code = code };
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await ApplyAsync(entity, request, cancellationToken);
        _dbContext.Faenas.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(entity);
    }

    public async Task<FaenaResponse?> UpdateAsync(
        string code,
        UpsertFaenaRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var entity = await Query()
            .SingleOrDefaultAsync(item => item.Code == NormalizeCode(code), cancellationToken);
        if (entity is null)
        {
            return null;
        }

        EnsureCanView(user, entity.Code);
        if (entity.IsActive && !request.Activo && !_authorizationPolicyService.CanDeactivateFaena(user))
        {
            throw new UnauthorizedAccessException("No tiene permiso para desactivar la faena indicada.");
        }

        var requestedCode = NormalizeRequiredCode(request.Codigo, "Codigo");
        if (!string.Equals(entity.Code, requestedCode, StringComparison.OrdinalIgnoreCase) &&
            await _dbContext.Faenas.AnyAsync(item => item.Code == requestedCode, cancellationToken))
        {
            throw new DomainException("Ya existe una faena con el codigo indicado.");
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        entity.Code = requestedCode;
        await ApplyAsync(entity, request, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(entity);
    }

    private IQueryable<FaenaEntity> Query() => _dbContext.Faenas
        .Include(faena => faena.ResponsibleUser)
        .Include(faena => faena.TechnicalLocation);

    private async Task ApplyAsync(
        FaenaEntity entity,
        UpsertFaenaRequest request,
        CancellationToken cancellationToken)
    {
        var name = RequireText(request.Nombre, "Nombre");
        var zone = RequireText(request.Zona, "Zona");
        var client = RequireText(request.Cliente, "Cliente");
        var faenaType = RequireText(request.TipoFaena, "TipoFaena");
        var region = RequireText(request.Region, "Region");
        var commune = RequireText(request.Comuna, "Comuna");
        var technicalLocationCode = NormalizeRequiredCode(request.UbicacionTecnicaCodigo, "UbicacionTecnicaCodigo");
        var technicalLocationName = RequireText(request.UbicacionTecnicaNombre, "UbicacionTecnicaNombre");

        ValidateCoordinates(request.Latitud, request.Longitud);
        if (request.ResponsableUsuarioId == Guid.Empty)
        {
            throw new DomainException("El responsable de la faena es obligatorio.");
        }

        var responsible = await _dbContext.Users.SingleOrDefaultAsync(
            item => item.Id == request.ResponsableUsuarioId,
            cancellationToken) ?? throw new DomainException("El usuario responsable no existe.");
        if (!responsible.IsActive || responsible.IsLocked)
        {
            throw new DomainException("El usuario responsable debe estar activo y no bloqueado.");
        }

        var locationsForFaena = await _dbContext.TechnicalLocations
            .Where(item => item.FaenaId == entity.Id)
            .ToListAsync(cancellationToken);
        if (locationsForFaena.Count > 1)
        {
            throw new DomainException($"La faena '{entity.Code}' tiene mas de una ubicacion tecnica y requiere resolucion manual antes de editarse.");
        }

        var location = locationsForFaena.SingleOrDefault() ?? entity.TechnicalLocation;
        var duplicateCode = await _dbContext.TechnicalLocations
            .SingleOrDefaultAsync(item => item.Code == technicalLocationCode, cancellationToken);
        if (duplicateCode is not null && duplicateCode.Id != location?.Id)
        {
            throw new DomainException("El codigo de ubicacion tecnica ya esta asociado a otra faena.");
        }

        entity.Name = name;
        entity.Zone = zone;
        entity.Client = client;
        entity.CostCenter = OptionalText(request.CentroCostes);
        entity.FaenaType = faenaType;
        entity.Region = region;
        entity.Commune = commune;
        entity.Latitude = request.Latitud;
        entity.Longitude = request.Longitud;
        entity.ResponsibleUserId = responsible.Id;
        entity.ResponsibleUser = responsible;
        entity.IsActive = request.Activo;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;

        if (location is null)
        {
            location = new TechnicalLocationEntity
            {
                FaenaId = entity.Id,
                Faena = entity
            };
            entity.TechnicalLocation = location;
            _dbContext.TechnicalLocations.Add(location);
        }

        location.Code = technicalLocationCode;
        location.Name = technicalLocationName;
        location.IsObsolete = request.UbicacionTecnicaObsoleta;
        location.UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    private void EnsureCanView(UserAccessContext user, string faenaCode)
    {
        if (!_authorizationPolicyService.CanViewFaena(user, faenaCode))
        {
            throw new UnauthorizedAccessException("No tiene acceso a la faena indicada.");
        }
    }

    private static FaenaResponse ToResponse(FaenaEntity entity) => new(
        entity.Id,
        entity.Code,
        entity.Name,
        entity.Zone,
        entity.Client,
        entity.CostCenter,
        entity.FaenaType,
        entity.Region,
        entity.Commune,
        entity.Latitude,
        entity.Longitude,
        entity.ResponsibleUserId,
        entity.ResponsibleUser?.DisplayName,
        entity.IsActive,
        entity.TechnicalLocation is null
            ? null
            : new TechnicalLocationSummary(
                entity.TechnicalLocation.Id,
                entity.TechnicalLocation.Code,
                entity.TechnicalLocation.Name,
                entity.TechnicalLocation.IsObsolete));

    private static bool MatchesFilters(FaenaEntity faena, FaenaQuery query) =>
        Matches(faena.Code, query.Codigo) &&
        Matches(faena.Name, query.Nombre) &&
        Matches(faena.Zone, query.Zona) &&
        Matches(faena.Client, query.Cliente) &&
        Matches(faena.FaenaType, query.TipoFaena) &&
        Matches(faena.Region, query.Region) &&
        Matches(faena.Commune, query.Comuna) &&
        (!query.ResponsableUsuarioId.HasValue || faena.ResponsibleUserId == query.ResponsableUsuarioId) &&
        Matches(faena.TechnicalLocation?.Code, query.UbicacionTecnicaCodigo) &&
        MatchesSearch(faena, query.Search);

    private static bool MatchesSearch(FaenaEntity faena, string? search) =>
        string.IsNullOrWhiteSpace(search) ||
        Contains(faena.Code, search) ||
        Contains(faena.Name, search) ||
        Contains(faena.Zone, search) ||
        Contains(faena.Client, search) ||
        Contains(faena.Region, search) ||
        Contains(faena.Commune, search) ||
        Contains(faena.ResponsibleUser?.DisplayName, search) ||
        Contains(faena.TechnicalLocation?.Code, search) ||
        Contains(faena.TechnicalLocation?.Name, search);

    private static bool Matches(string? value, string? filter) =>
        string.IsNullOrWhiteSpace(filter) || Contains(value, filter);

    private static bool Contains(string? value, string? search) =>
        !string.IsNullOrWhiteSpace(value) &&
        !string.IsNullOrWhiteSpace(search) &&
        value.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string NormalizeRequiredCode(string? value, string field) =>
        RequireText(value, field).ToUpperInvariant();

    private static string NormalizeCode(string? value) =>
        value?.Trim().ToUpperInvariant() ?? string.Empty;

    private static string RequireText(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{field} es obligatorio.");
        }

        return value.Trim();
    }

    private static string? OptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ValidateCoordinates(decimal? latitude, decimal? longitude)
    {
        if (latitude is < -90 or > 90)
        {
            throw new DomainException("La latitud debe estar entre -90 y 90.");
        }

        if (longitude is < -180 or > 180)
        {
            throw new DomainException("La longitud debe estar entre -180 y 180.");
        }
    }
}
