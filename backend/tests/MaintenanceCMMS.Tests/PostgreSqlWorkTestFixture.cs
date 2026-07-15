using MaintenanceCMMS.Application.Inventory;
using MaintenanceCMMS.Application.WorkNotifications;
using MaintenanceCMMS.Application.WorkOrders;
using MaintenanceCMMS.Domain.Enums;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace MaintenanceCMMS.Tests;

internal sealed class PostgreSqlWorkTestFixture : IAsyncDisposable
{
    private PostgreSqlWorkTestFixture(string databaseName, string adminConnectionString, CmmsDbContext dbContext)
    {
        DatabaseName = databaseName;
        AdminConnectionString = adminConnectionString;
        DbContext = dbContext;
    }

    private static readonly SemaphoreSlim ContainerGate = new(1, 1);
    private static readonly string ContainerPassword = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
    private static PostgreSqlContainer? Container;

    public string DatabaseName { get; }
    public string AdminConnectionString { get; }
    public CmmsDbContext DbContext { get; }

    public static async Task<PostgreSqlWorkTestFixture> CreateAsync()
    {
        var databaseName = $"cmms_work_tests_{Guid.NewGuid():N}";
        var adminConnectionString = await GetAdminConnectionStringAsync();
        await CreateDatabaseAsync(databaseName, adminConnectionString);
        var dbContext = new CmmsDbContext(new DbContextOptionsBuilder<CmmsDbContext>().UseNpgsql(ConnectionString(adminConnectionString, databaseName)).Options);
        await dbContext.Database.MigrateAsync();
        await SeedAsync(dbContext);
        return new PostgreSqlWorkTestFixture(databaseName, adminConnectionString, dbContext);
    }

    public CmmsDbContext NewContext() => new(new DbContextOptionsBuilder<CmmsDbContext>().UseNpgsql(ConnectionString(AdminConnectionString, DatabaseName)).Options);

    public static async Task<string> GetAdminConnectionStringAsync()
    {
        if (Container is not null) return Container.GetConnectionString();
        await ContainerGate.WaitAsync();
        try
        {
            if (Container is null)
            {
                Container = new PostgreSqlBuilder().WithDatabase("postgres").WithUsername("cmms_app").WithPassword(ContainerPassword).Build();
                await Container.StartAsync();
            }
            return Container.GetConnectionString();
        }
        finally { ContainerGate.Release(); }
    }

    public static async Task CreateDatabaseAsync(string databaseName, string? adminConnectionString = null)
    {
        await using var connection = new NpgsqlConnection(adminConnectionString ?? await GetAdminConnectionStringAsync());
        await connection.OpenAsync(); await using var command = connection.CreateCommand(); command.CommandText = $"CREATE DATABASE \"{databaseName}\""; await command.ExecuteNonQueryAsync();
    }

    public static async Task DropDatabaseAsync(string databaseName, string? adminConnectionString = null)
    {
        await using var connection = new NpgsqlConnection(adminConnectionString ?? await GetAdminConnectionStringAsync());
        await connection.OpenAsync(); await using var command = connection.CreateCommand(); command.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)"; await command.ExecuteNonQueryAsync();
    }

    public static string ConnectionString(string adminConnectionString, string databaseName)
    {
        var builder = new NpgsqlConnectionStringBuilder(adminConnectionString) { Database = databaseName };
        return builder.ConnectionString;
    }
    private static async Task SeedAsync(CmmsDbContext db)
    {
        foreach (var value in Enum.GetNames<WorkNotificationType>()) AddCatalog(db, "WorkNotificationType", value);
        foreach (var value in Enum.GetNames<WorkNotificationStatus>()) AddCatalog(db, "WorkNotificationStatus", value);
        foreach (var value in Enum.GetNames<WorkNotificationPriority>()) AddCatalog(db, "WorkNotificationPriority", value);
        foreach (var value in Enum.GetNames<WorkNotificationCriticality>()) AddCatalog(db, "WorkNotificationCriticality", value);
        foreach (var value in Enum.GetNames<WorkFailureClassification>()) AddCatalog(db, "WorkFailureClassification", value);
        foreach (var value in Enum.GetNames<WorkOrderLifecycleStatus>()) AddCatalog(db, "WorkOrderLifecycleStatus", value);
        foreach (var value in Enum.GetNames<WorkOrderSparePartStatus>()) AddCatalog(db, "WorkOrderSparePartStatus", value);
        foreach (var value in Enum.GetNames<WorkOrderEvidenceType>()) AddCatalog(db, "WorkOrderEvidenceType", value);
        foreach (var value in Enum.GetNames<WorkOrderChecklistResponseType>()) AddCatalog(db, "WorkOrderChecklistResponseType", value);
        foreach (var value in Enum.GetNames<MaintenanceType>()) AddCatalog(db, "MaintenanceType", value);
        AddCatalog(db, "MaintenanceType", "Corrective");
        AddCatalog(db, "MaintenanceType", "Preventive");
        foreach (var value in Enum.GetNames<WarehouseType>()) AddInventoryCatalog(db, "WarehouseType", value);
        foreach (var value in Enum.GetNames<StockMovementType>()) AddInventoryCatalog(db, "MovementType", value);
        AddInventoryCatalog(db, "Unit", "UN");

        var responseType = db.WorkCatalogs.Local.First(x => x.Category == "WorkOrderChecklistResponseType" && x.Code == WorkOrderChecklistResponseType.CumpleNoCumpleNoAplica.ToString());
        var template = new ChecklistTemplateEntity { Code = "TPL-BASE", Name = "Plantilla base", IsActive = true };
        template.Items.Add(new ChecklistTemplateItemEntity { Template = template, SortOrder = 1, ItemText = "Verificacion base", Mandatory = true, ResponseType = responseType, IsActive = true });
        db.ChecklistTemplates.Add(template);

        var faena = new FaenaEntity { Code = "FAE-1", Name = "Faena Uno", IsActive = true };
        var type = new AssetTypeEntity { Code = "EQUIPO", Name = "Equipo", IsActive = true };
        var family = new EquipmentFamilyEntity { Code = "FAM-1", Name = "Familia", AssetTypeId = type.Id, IsActive = true };
        var state = new AssetOperationalStateEntity { Code = "OPERATIVO_FAENA", Name = "Operativo", IsActive = true };
        db.AddRange(faena, type, family);
        db.AssetOperationalStates.Add(state);
        db.Assets.AddRange(
            new AssetEntity { Code = "ACT-1", Name = "Excavadora 01", Faena = faena, Family = family, OperationalState = state, AssetTypeId = type.Id },
            new AssetEntity { Code = "ACT-2", Name = "Camion 02", Faena = faena, Family = family, OperationalState = state, AssetTypeId = type.Id });
        await db.SaveChangesAsync();
    }

    private static void AddCatalog(CmmsDbContext db, string category, string code)
    {
        if (db.WorkCatalogs.Local.Any(x => x.Category == category && x.Code == code)) return;
        db.WorkCatalogs.Add(new WorkCatalogEntity { Category = category, Code = code, Name = code, IsActive = true, SortOrder = db.WorkCatalogs.Local.Count(x => x.Category == category) + 1 });
    }

    private static void AddInventoryCatalog(CmmsDbContext db, string category, string code)
    {
        var normalizedCode = code.Trim().ToUpperInvariant();
        if (db.InventoryCatalogs.Local.Any(item => item.Category == category && item.Code == normalizedCode)) return;
        db.InventoryCatalogs.Add(new InventoryCatalogEntity
        {
            Category = category,
            Code = normalizedCode,
            Name = code,
            IsActive = true,
            SortOrder = db.InventoryCatalogs.Local.Count(item => item.Category == category) + 1
        });
    }
    public async ValueTask DisposeAsync()
    {
        await DbContext.DisposeAsync();
        await DropDatabaseAsync(DatabaseName, AdminConnectionString);
    }
}
