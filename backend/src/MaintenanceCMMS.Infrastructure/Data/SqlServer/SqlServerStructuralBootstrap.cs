using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.Inventory;
using MaintenanceCMMS.Application.WorkNotifications;
using MaintenanceCMMS.Application.WorkOrders;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Domain.Enums;
using MaintenanceCMMS.Infrastructure.Data.SqlServer.Entities;
using MaintenanceCMMS.Infrastructure.Assets;
using MaintenanceCMMS.Infrastructure.Security;
using MaintenanceCMMS.Infrastructure.Options;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceCMMS.Infrastructure.Data.SqlServer;

public interface ISqlServerStructuralBootstrap
{
    Task BootstrapAsync(CancellationToken cancellationToken);
}

public sealed class SqlServerStructuralBootstrap : ISqlServerStructuralBootstrap
{
    private const long BootstrapLockKey = 7_144_260_118_247_903_412;
    private readonly CmmsDbContext _db;

    public SqlServerStructuralBootstrap(CmmsDbContext db) { _db = db; }
    public SqlServerStructuralBootstrap(CmmsDbContext db, IPasswordHasher _, IOptions<AuthSeedOptions> __) : this(db) { }

    public async Task BootstrapAsync(CancellationToken ct)
    {
        await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                await SqlServerTransactionLock.AcquireExclusiveAsync(_db, "cmms.structural-bootstrap", ct);
                await EnsureStatesAsync(ct);
                await EnsureWorkCatalogsAsync(ct);
                await EnsureInventoryCatalogsAsync(ct);
                await _db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch (DbUpdateConcurrencyException exception)
            {
                var entries = string.Join(", ", exception.Entries.Select(entry => $"{entry.Metadata.ClrType.Name}:{entry.State}:id={entry.Property("Id").CurrentValue}:rowversion={entry.Property("RowVersion").OriginalValue}:modified={string.Join("|", entry.Properties.Where(property => property.IsModified).Select(property => property.Metadata.Name))}"));
                await RollbackAsync(transaction);
                _db.ChangeTracker.Clear();
                throw new DbUpdateConcurrencyException($"El bootstrap estructural encontro concurrencia en: {entries}.", exception);
            }
            catch
            {
                await RollbackAsync(transaction);
                _db.ChangeTracker.Clear();
                throw;
            }
        });
    }

    private static async Task RollbackAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            // SQL Server may have already rolled back the transaction after a failed command.
        }
    }

    private async Task EnsureStatesAsync(CancellationToken ct)
    {
        var states = await _db.AssetOperationalStates.ToListAsync(ct);
        foreach (var legacyCode in new[] { "OPERATIVO_FAENA", "ALERTA_FAENA", "FUERA_SERVICIO_FAENA", "FUERA_SERVICIO_TALLER", "EN_PREPARACION" })
        {
            var legacy = states.SingleOrDefault(item => string.Equals(item.Code, legacyCode, StringComparison.OrdinalIgnoreCase));
            if (legacy is null) continue;
            var canonicalCode = AssetOperationalPolicy.NormalizeLegacyCode(legacyCode);
            var canonical = states.SingleOrDefault(item => string.Equals(item.Code, canonicalCode, StringComparison.OrdinalIgnoreCase));
            if (canonical is null)
            {
                legacy.Code = canonicalCode;
                canonical = legacy;
            }
            else if (canonical.Id != legacy.Id)
            {
                await _db.Assets.Where(item => item.OperationalStateId == legacy.Id).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.OperationalStateId, canonical.Id), ct);
                await _db.AssetStateEvents.Where(item => item.PreviousStateId == legacy.Id).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.PreviousStateId, canonical.Id), ct);
                await _db.AssetStateEvents.Where(item => item.NewStateId == legacy.Id).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.NewStateId, canonical.Id), ct);
                await _db.OperationalUnits.Where(item => item.OperationalStateId == legacy.Id).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.OperationalStateId, canonical.Id), ct);
                await _db.OperationalUnits.Where(item => item.BaselineOperationalStateId == legacy.Id).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.BaselineOperationalStateId, canonical.Id), ct);
                _db.AssetOperationalStates.Remove(legacy);
                states.Remove(legacy);
            }
        }

        foreach (var definition in AssetOperationalPolicy.Definitions)
        {
            var entity = states.SingleOrDefault(item => string.Equals(item.Code, definition.Code, StringComparison.OrdinalIgnoreCase));
            if (entity is null)
            {
                entity = new AssetOperationalStateEntity { Code = definition.Code, Name = definition.Name, Severity = definition.Severity, IsActive = true };
                _db.AssetOperationalStates.Add(entity);
                states.Add(entity);
            }
            else
            {
                entity.Code = definition.Code;
                entity.Name = definition.Name;
                entity.Severity = definition.Severity;
                entity.IsActive = true;
                entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
            }
        }
    }
    private async Task EnsureWorkCatalogsAsync(CancellationToken ct)
    {
        var definitions = new List<(string Category, string Code, int SortOrder)>();
        AddEnum<WorkNotificationType>(definitions, "WorkNotificationType");
        AddEnum<WorkNotificationStatus>(definitions, "WorkNotificationStatus");
        AddEnum<WorkNotificationPriority>(definitions, "WorkNotificationPriority");
        AddEnum<WorkNotificationCriticality>(definitions, "WorkNotificationCriticality");
        AddEnum<WorkFailureClassification>(definitions, "WorkFailureClassification");
        AddEnum<WorkOrderLifecycleStatus>(definitions, "WorkOrderLifecycleStatus");
        AddEnum<WorkOrderTaskStatus>(definitions, "WorkOrderTaskStatus");
        AddEnum<WorkOrderSparePartStatus>(definitions, "WorkOrderSparePartStatus");
        AddEnum<WorkOrderEvidenceType>(definitions, "WorkOrderEvidenceType");
        AddEnum<WorkOrderChecklistResponseType>(definitions, "WorkOrderChecklistResponseType");
        AddEnum<MaintenanceType>(definitions, "MaintenanceType");
        var categories = definitions.Select(item => item.Category).Distinct().ToArray();
        var existing = await _db.WorkCatalogs.Where(item => categories.Contains(item.Category)).Select(item => new { item.Category, item.Code }).ToListAsync(ct);
        foreach (var definition in definitions.Where(definition => !existing.Any(item => item.Category == definition.Category && item.Code == definition.Code)))
            _db.WorkCatalogs.Add(new WorkCatalogEntity { Category = definition.Category, Code = definition.Code, Name = definition.Code, IsActive = true, SortOrder = definition.SortOrder });
    }

    private async Task EnsureInventoryCatalogsAsync(CancellationToken ct)
    {
        var definitions = new List<(string Category, string Code, string Name, int SortOrder)> { ("Unit", "UN", "Unidad", 1) };
        AddInventoryEnum<WarehouseType>(definitions, "WarehouseType");
        AddInventoryEnum<StockMovementType>(definitions, "MovementType");
        var categories = definitions.Select(item => item.Category).Distinct().ToArray();
        var existing = await _db.InventoryCatalogs.Where(item => categories.Contains(item.Category)).Select(item => new { item.Category, item.Code }).ToListAsync(ct);
        foreach (var definition in definitions.Where(definition => !existing.Any(item => item.Category == definition.Category && item.Code == definition.Code)))
            _db.InventoryCatalogs.Add(new InventoryCatalogEntity { Category = definition.Category, Code = definition.Code, Name = definition.Name, IsActive = true, SortOrder = definition.SortOrder });
    }

    private static void AddEnum<TEnum>(ICollection<(string Category, string Code, int SortOrder)> target, string category) where TEnum : struct, Enum
    {
        var order = 1;
        foreach (var value in Enum.GetNames<TEnum>()) target.Add((category, value, order++));
    }

    private static void AddInventoryEnum<TEnum>(ICollection<(string Category, string Code, string Name, int SortOrder)> target, string category) where TEnum : struct, Enum
    {
        var order = 1;
        foreach (var value in Enum.GetNames<TEnum>()) target.Add((category, value.ToUpperInvariant(), value, order++));
    }

}
