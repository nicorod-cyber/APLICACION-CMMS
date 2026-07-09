using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql;

public interface IPostgreSqlDevelopmentSeeder
{
    Task SeedAsync(CancellationToken cancellationToken);
}

public sealed class PostgreSqlDevelopmentSeeder : IPostgreSqlDevelopmentSeeder
{
    private readonly CmmsDbContext _dbContext;

    public PostgreSqlDevelopmentSeeder(CmmsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        await UpsertFaenaAsync("FAENA_DEMO", "Faena Demo", cancellationToken);

        await UpsertOperationalStateAsync("OPERATIVO_FAENA", "Operativo en Faena", cancellationToken);
        await UpsertOperationalStateAsync("ALERTA_FAENA", "Con alerta en Faena", cancellationToken);
        await UpsertOperationalStateAsync("FUERA_SERVICIO_FAENA", "Fuera de servicio en Faena", cancellationToken);
        await UpsertOperationalStateAsync("FUERA_SERVICIO_TALLER", "Fuera de servicio en Taller", cancellationToken);

        await UpsertFamilyAsync("CAMION_PLUMA", "Camion pluma", cancellationToken);
        await UpsertFamilyAsync("COMPRESOR", "Compresor", cancellationToken);
        await UpsertFamilyAsync("GRUA_HORQUILLA", "Grua horquilla", cancellationToken);

        await UpsertPermissionAsync(AuthPermissions.ManageEquipmentFamilies, "Gestionar familias de equipo", cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var faena = await _dbContext.Faenas.SingleAsync(item => item.Code == "FAENA_DEMO", cancellationToken);
        var family = await _dbContext.EquipmentFamilies.SingleAsync(item => item.Code == "CAMION_PLUMA", cancellationToken);
        var state = await _dbContext.AssetOperationalStates.SingleAsync(item => item.Code == "OPERATIVO_FAENA", cancellationToken);

        if (!await _dbContext.Assets.AnyAsync(item => item.Code == "ACT-DEMO-001", cancellationToken))
        {
            _dbContext.Assets.Add(new AssetEntity
            {
                Code = "ACT-DEMO-001",
                Name = "Activo demo",
                FaenaId = faena.Id,
                FamilyId = family.Id,
                OperationalStateId = state.Id,
                AssetType = "Equipo",
                RecordStatus = "vigente",
                DocumentStatus = "Pendiente",
                TechnicalSheetValidated = false
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task UpsertFaenaAsync(string code, string name, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Faenas.FirstOrDefaultAsync(item => item.Code == code, cancellationToken);
        if (entity is null)
        {
            _dbContext.Faenas.Add(new FaenaEntity { Code = code, Name = name, IsActive = true });
        }
        else
        {
            entity.Name = name;
            entity.IsActive = true;
            entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }
    }

    private async Task UpsertOperationalStateAsync(string code, string name, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.AssetOperationalStates.FirstOrDefaultAsync(item => item.Code == code, cancellationToken);
        if (entity is null)
        {
            _dbContext.AssetOperationalStates.Add(new AssetOperationalStateEntity { Code = code, Name = name, IsActive = true });
        }
        else
        {
            entity.Name = name;
            entity.IsActive = true;
            entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }
    }

    private async Task UpsertFamilyAsync(string code, string name, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.EquipmentFamilies.FirstOrDefaultAsync(item => item.Code == code, cancellationToken);
        if (entity is null)
        {
            _dbContext.EquipmentFamilies.Add(new EquipmentFamilyEntity { Code = code, Name = name, IsActive = true });
        }
        else
        {
            entity.Name = name;
            entity.IsActive = true;
            entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }
    }

    private async Task UpsertPermissionAsync(string code, string name, CancellationToken cancellationToken)
    {
        var normalized = code.Trim().ToLowerInvariant();
        var entity = await _dbContext.Permissions.FirstOrDefaultAsync(item => item.Code == normalized, cancellationToken);
        if (entity is null)
        {
            _dbContext.Permissions.Add(new PermissionEntity { Code = normalized, Name = name, IsActive = true });
        }
        else
        {
            entity.Name = name;
            entity.IsActive = true;
            entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }
    }
}
