using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.Documents;
using MaintenanceCMMS.Application.Inventory;
using MaintenanceCMMS.Application.WorkNotifications;
using MaintenanceCMMS.Application.WorkOrders;
using MaintenanceCMMS.Domain.Enums;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql;

public interface IPostgreSqlDevelopmentSeeder
{
    Task SeedReferenceCatalogsAsync(CancellationToken cancellationToken);
    Task SeedDemoDataAsync(CancellationToken cancellationToken);
}

public sealed class PostgreSqlDevelopmentSeeder : IPostgreSqlDevelopmentSeeder
{
    private readonly CmmsDbContext _dbContext;

    public PostgreSqlDevelopmentSeeder(CmmsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task SeedReferenceCatalogsAsync(CancellationToken cancellationToken)
    {
        await UpsertOperationalStateAsync("OPERATIVO_FAENA", "Operativo en Faena", cancellationToken);
        await UpsertOperationalStateAsync("ALERTA_FAENA", "Con alerta en Faena", cancellationToken);
        await UpsertOperationalStateAsync("FUERA_SERVICIO_FAENA", "Fuera de servicio en Faena", cancellationToken);
        await UpsertOperationalStateAsync("FUERA_SERVICIO_TALLER", "Fuera de servicio en Taller", cancellationToken);

        await UpsertAssetTypeAsync("EQUIPO", "Equipo", cancellationToken);
        // Families require the generated asset-type key on a pristine database.
        await _dbContext.SaveChangesAsync(cancellationToken);
        await UpsertFamilyAsync("CAMION_PLUMA", "Camion pluma", cancellationToken);
        await UpsertFamilyAsync("COMPRESOR", "Compresor", cancellationToken);
        await UpsertFamilyAsync("GRUA_HORQUILLA", "Grua horquilla", cancellationToken);

        await UpsertPermissionAsync(AuthPermissions.ViewFaenas, "Ver faenas", cancellationToken);
        await UpsertPermissionAsync(AuthPermissions.CreateFaenas, "Crear faenas", cancellationToken);
        await UpsertPermissionAsync(AuthPermissions.EditFaenas, "Editar faenas", cancellationToken);
        await UpsertPermissionAsync(AuthPermissions.DeactivateFaenas, "Desactivar faenas", cancellationToken);
        await UpsertPermissionAsync(AuthPermissions.ManageEquipmentFamilies, "Gestionar familias de equipo", cancellationToken);
        await UpsertPermissionAsync(AuthPermissions.ManageAssetCatalogs, "Administrar catálogos de activos", cancellationToken);
        await UpsertPermissionAsync(AuthPermissions.ManageAssetAttributes, "Administrar atributos de activos", cancellationToken);
        await UpsertPermissionAsync(AuthPermissions.RegisterAssetReadings, "Registrar lecturas de activos", cancellationToken);
        await UpsertPermissionAsync(AuthPermissions.CorrectAssetReadings, "Corregir lecturas de activos", cancellationToken);
        await UpsertPermissionAsync(AuthPermissions.ViewOperationalUnits, "Ver unidades operativas", cancellationToken);
        await UpsertPermissionAsync(AuthPermissions.ManageOperationalUnits, "Administrar unidades operativas", cancellationToken);
        await UpsertPermissionAsync(AuthPermissions.ManageOperationalUnitComposition, "Gestionar composición de unidades", cancellationToken);
        await UpsertPermissionAsync(AuthPermissions.ManageDocumentRequirements, "Administrar requisitos documentales", cancellationToken);
        await UpsertDocumentTypeAsync("REV-TEC", "Revision tecnica", DocumentEntityType.Activo, true, true, true, 30, cancellationToken);
        await UpsertDocumentTypeAsync("PERMISO", "Permiso operacional", DocumentEntityType.Activo, true, false, false, 30, cancellationToken);
        await UpsertDocumentTypeAsync("CERT", "Certificado", DocumentEntityType.Activo, false, false, false, 45, cancellationToken);
        await UpsertDocumentTypeAsync("FAENA-GRAL", "Documento general de faena", DocumentEntityType.Faena, false, false, false, 30, cancellationToken);
        await UpsertDocumentTypeAsync("OT-GRAL", "Documento general de OT", DocumentEntityType.OT, false, false, false, 30, cancellationToken);
        await UpsertWorkCatalogsAsync(cancellationToken);
        await UpsertInventoryCatalogsAsync(cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        await UpsertBaseChecklistTemplateAsync(cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SeedDemoDataAsync(CancellationToken cancellationToken) => Task.CompletedTask;

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

    private async Task UpsertAssetTypeAsync(string code, string name, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.AssetTypes.FirstOrDefaultAsync(item => item.Code == code, cancellationToken);
        if (entity is null) _dbContext.AssetTypes.Add(new AssetTypeEntity { Code = code, Name = name, Category = "EQUIPO", IsActive = true });
        else { entity.Name = name; entity.IsActive = true; entity.UpdatedAtUtc = DateTimeOffset.UtcNow; }
    }

    private async Task UpsertFamilyAsync(string code, string name, CancellationToken cancellationToken)
    {
        var assetType = await _dbContext.AssetTypes.SingleAsync(item => item.Code == "EQUIPO", cancellationToken);
        var entity = await _dbContext.EquipmentFamilies.FirstOrDefaultAsync(item => item.Code == code, cancellationToken);
        if (entity is null) _dbContext.EquipmentFamilies.Add(new EquipmentFamilyEntity { Code = code, Name = name, AssetTypeId = assetType.Id, IsActive = true });
        else { entity.Name = name; entity.AssetTypeId = assetType.Id; entity.IsActive = true; entity.UpdatedAtUtc = DateTimeOffset.UtcNow; }
    }
    private async Task UpsertInventoryCatalogsAsync(CancellationToken cancellationToken)
    {
        var sortOrder = 1;
        foreach (var value in Enum.GetNames<WarehouseType>())
        {
            await UpsertInventoryCatalogAsync("WarehouseType", value.ToUpperInvariant(), value, sortOrder++, cancellationToken);
        }

        sortOrder = 1;
        foreach (var value in Enum.GetNames<StockMovementType>())
        {
            await UpsertInventoryCatalogAsync("MovementType", value.ToUpperInvariant(), value, sortOrder++, cancellationToken);
        }

        await UpsertInventoryCatalogAsync("Unit", "UN", "Unidad", 1, cancellationToken);
    }

    private async Task UpsertInventoryCatalogAsync(string category, string code, string name, int sortOrder, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.InventoryCatalogs.FirstOrDefaultAsync(
            item => item.Category == category && item.Code == code,
            cancellationToken);
        if (entity is null)
        {
            _dbContext.InventoryCatalogs.Add(new InventoryCatalogEntity
            {
                Category = category,
                Code = code,
                Name = name,
                IsActive = true,
                SortOrder = sortOrder
            });
            return;
        }

        entity.Name = name;
        entity.IsActive = true;
        entity.SortOrder = sortOrder;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
    private async Task UpsertWorkCatalogsAsync(CancellationToken cancellationToken)
    {
        var order = 1;
        foreach (var value in Enum.GetNames<WorkNotificationType>()) await UpsertWorkCatalogAsync("WorkNotificationType", value, order++, cancellationToken);
        order = 1;
        foreach (var value in Enum.GetNames<WorkNotificationStatus>()) await UpsertWorkCatalogAsync("WorkNotificationStatus", value, order++, cancellationToken);
        order = 1;
        foreach (var value in Enum.GetNames<WorkNotificationPriority>()) await UpsertWorkCatalogAsync("WorkNotificationPriority", value, order++, cancellationToken);
        order = 1;
        foreach (var value in Enum.GetNames<WorkNotificationCriticality>()) await UpsertWorkCatalogAsync("WorkNotificationCriticality", value, order++, cancellationToken);
        order = 1;
        foreach (var value in Enum.GetNames<WorkFailureClassification>()) await UpsertWorkCatalogAsync("WorkFailureClassification", value, order++, cancellationToken);
        order = 1;
        foreach (var value in Enum.GetNames<WorkOrderLifecycleStatus>()) await UpsertWorkCatalogAsync("WorkOrderLifecycleStatus", value, order++, cancellationToken);
        foreach (var value in Enum.GetNames<WorkOrderTaskStatus>()) await UpsertWorkCatalogAsync("WorkOrderTaskStatus", value, order++, cancellationToken);
        order = 1;
        foreach (var value in Enum.GetNames<WorkOrderSparePartStatus>()) await UpsertWorkCatalogAsync("WorkOrderSparePartStatus", value, order++, cancellationToken);
        order = 1;
        foreach (var value in Enum.GetNames<WorkOrderEvidenceType>()) await UpsertWorkCatalogAsync("WorkOrderEvidenceType", value, order++, cancellationToken);
        order = 1;
        foreach (var value in Enum.GetNames<WorkOrderChecklistResponseType>()) await UpsertWorkCatalogAsync("WorkOrderChecklistResponseType", value, order++, cancellationToken);
        order = 1;
        foreach (var value in Enum.GetNames<MaintenanceType>()) await UpsertWorkCatalogAsync("MaintenanceType", value, order++, cancellationToken);
    }

    private async Task UpsertWorkCatalogAsync(string category, string code, int sortOrder, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.WorkCatalogs.FirstOrDefaultAsync(item => item.Category == category && item.Code == code, cancellationToken);
        if (entity is null)
        {
            _dbContext.WorkCatalogs.Add(new WorkCatalogEntity { Category = category, Code = code, Name = code, IsActive = true, SortOrder = sortOrder });
        }
        else
        {
            entity.Name = code;
            entity.IsActive = true;
            entity.SortOrder = sortOrder;
            entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }
    }

    private async Task UpsertBaseChecklistTemplateAsync(CancellationToken cancellationToken)
    {
        var responseType = await _dbContext.WorkCatalogs.FirstAsync(
            item => item.Category == "WorkOrderChecklistResponseType" && item.Code == nameof(WorkOrderChecklistResponseType.CumpleNoCumpleNoAplica),
            cancellationToken);
        var template = await _dbContext.ChecklistTemplates
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.Code == "TPL-BASE", cancellationToken);
        if (template is null)
        {
            template = new ChecklistTemplateEntity { Code = "TPL-BASE", Name = "Plantilla base", IsActive = true };
            _dbContext.ChecklistTemplates.Add(template);
        }
        else
        {
            template.Name = "Plantilla base";
            template.IsActive = true;
            template.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        if (!template.Items.Any(item => item.SortOrder == 1))
        {
            template.Items.Add(new ChecklistTemplateItemEntity
            {
                Id = Guid.NewGuid(),
                SortOrder = 1,
                ItemText = "Verificacion base",
                Mandatory = true,
                ResponseTypeId = responseType.Id,
                ResponseType = responseType,
                IsActive = true
            });
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
    private async Task UpsertDocumentTypeAsync(
        string code,
        string name,
        DocumentEntityType appliesTo,
        bool mandatory,
        bool critical,
        bool blocksAvailability,
        int alertDays,
        CancellationToken cancellationToken)
    {
        var normalized = code.Trim().ToUpperInvariant();
        var entity = await _dbContext.DocumentTypes.FirstOrDefaultAsync(item => item.Code == normalized, cancellationToken);
        if (entity is null)
        {
            _dbContext.DocumentTypes.Add(new DocumentTypeEntity
            {
                Code = normalized,
                Name = name,
                AppliesTo = appliesTo.ToString(),
                IsMandatory = mandatory,
                IsCritical = critical,
                BlocksAvailability = blocksAvailability,
                AlertDays = alertDays,
                ResponsibleRoles = AuthRoles.Planner,
                IsActive = true,
                CreatedByUserId = "seed"
            });
        }
        else
        {
            entity.Name = name;
            entity.AppliesTo = appliesTo.ToString();
            entity.IsMandatory = mandatory;
            entity.IsCritical = critical;
            entity.BlocksAvailability = blocksAvailability;
            entity.AlertDays = alertDays;
            entity.ResponsibleRoles = AuthRoles.Planner;
            entity.IsActive = true;
            entity.UpdatedByUserId = "seed";
            entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }
    }
}
