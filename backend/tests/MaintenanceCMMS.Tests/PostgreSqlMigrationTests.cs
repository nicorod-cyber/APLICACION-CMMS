using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.Inventory;
using MaintenanceCMMS.Domain.Enums;
using MaintenanceCMMS.Infrastructure.Auditing;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using MaintenanceCMMS.Infrastructure.Inventory;
using MaintenanceCMMS.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace MaintenanceCMMS.Tests;

public sealed class PostgreSqlMigrationTests
{
    private static readonly UserAccessContext Admin = new(
        "migration-test",
        [AuthRoles.Admin],
        [AuthPermissions.Administration, AuthPermissions.AdjustStock, AuthPermissions.ViewGlobalWarehouses],
        []);

    [Fact]
    public async Task MigrateFromEmptyDatabase_CreatesInventoryAndSupportsServiceFlow()
    {
        await using var database = await MigrationDatabase.CreateAsync();
        await database.Context.Database.MigrateAsync();

        Assert.True(await database.ExistsAsync("usuarios"));
        Assert.True(await database.ExistsAsync("faenas"));
        Assert.True(await database.ExistsAsync("documentos"));
        Assert.True(await database.ExistsAsync("avisos_trabajo_sql"));
        Assert.True(await database.ExistsAsync("ordenes_trabajo_sql"));
        Assert.True(await database.ExistsAsync("audit_log"));
        foreach (var table in new[] { "catalogos_inventario", "bodegas", "ubicaciones_bodega", "repuestos", "stock_bodega", "reservas_stock", "transferencias_stock", "movimientos_stock" })
        {
            Assert.True(await database.ExistsAsync(table));
        }

        foreach (var sequence in new[] { "spare_part_number_seq", "stock_movement_number_seq", "stock_reservation_number_seq", "stock_transfer_number_seq" })
        {
            Assert.True(await database.ExistsAsync(sequence));
        }

        database.Context.Faenas.Add(new FaenaEntity { Code = "MIG-FAE", Name = "Faena migración", IsActive = true });
        await database.Context.SaveChangesAsync();

        var service = new InventoryService(
            database.Context,
            new PostgreSqlAuditService(database.Context, new AuditContextAccessor()),
            new AuthorizationPolicyService());
        await service.CreateWarehouseAsync(new CreateWarehouseRequest("MIG-BOD", "Bodega migración", "MIG-FAE", WarehouseType.Central), Admin, CancellationToken.None);
        var part = await service.CreateSparePartAsync(new CreateSparePartRequest("Repuesto migración", "UN"), Admin, CancellationToken.None);
        var movement = await service.RegisterMovementAsync(
            new StockMovementRequest(StockMovementType.Reception, part.Summary.Codigo, 2, "Recepción migración", BodegaCodigo: "MIG-BOD"),
            Admin,
            CancellationToken.None);

        Assert.StartsWith("MOV-", movement.MovimientoId);
        Assert.StartsWith("REP-", part.Summary.Codigo);
    }

    [Fact]
    public async Task MigrateFromWorkNotifications_PreservesPreviousRowsAndAddsInventory()
    {
        await using var database = await MigrationDatabase.CreateAsync();
        var migrator = database.Context.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(database.WorkMigrationId);

        var faena = new FaenaEntity { Code = "UPG-FAE", Name = "Faena actualización", IsActive = true };
        var family = new EquipmentFamilyEntity { Code = "UPG-FAM", Name = "Familia actualización", IsActive = true };
        var state = new AssetOperationalStateEntity { Code = "OPERATIVO_FAENA", Name = "Operativo", IsActive = true };
        var asset = new AssetEntity
        {
            Code = "UPG-ACT",
            Name = "Activo actualización",
            Faena = faena,
            Family = family,
            OperationalState = state,
            AssetType = "Equipo",
            RecordStatus = "vigente"
        };
        database.Context.AddRange(faena, family, state, asset);
        await database.Context.SaveChangesAsync();

        await database.Context.Database.MigrateAsync();

        Assert.Equal(1, await database.Context.Faenas.CountAsync(x => x.Code == "UPG-FAE"));
        Assert.Equal(1, await database.Context.Assets.CountAsync(x => x.Code == "UPG-ACT"));
        Assert.True(await database.ExistsAsync("repuestos"));
        Assert.True(await database.ExistsAsync("stock_movement_number_seq"));
    }

    [Fact]
    public async Task RollbackToWorkNotifications_RemovesOnlyInventoryObjects()
    {
        await using var database = await MigrationDatabase.CreateAsync();
        await database.Context.Database.MigrateAsync();
        await database.Context.Database.GetService<IMigrator>().MigrateAsync(database.WorkMigrationId);

        foreach (var table in new[] { "catalogos_inventario", "bodegas", "ubicaciones_bodega", "repuestos", "stock_bodega", "reservas_stock", "transferencias_stock", "movimientos_stock" })
        {
            Assert.False(await database.ExistsAsync(table));
        }

        foreach (var sequence in new[] { "spare_part_number_seq", "stock_movement_number_seq", "stock_reservation_number_seq", "stock_transfer_number_seq" })
        {
            Assert.False(await database.ExistsAsync(sequence));
        }

        foreach (var table in new[] { "usuarios", "roles", "faenas", "activos", "documentos", "avisos_trabajo_sql", "ordenes_trabajo_sql", "audit_log" })
        {
            Assert.True(await database.ExistsAsync(table));
        }
    }

    private sealed class MigrationDatabase : IAsyncDisposable
    {
        private MigrationDatabase(string name, CmmsDbContext context)
        {
            Name = name;
            Context = context;
        }

        public string Name { get; }
        public CmmsDbContext Context { get; }
        public string WorkMigrationId => "202607090003_WorkNotificationsAndOrdersPostgreSql";
        public static async Task<MigrationDatabase> CreateAsync()
        {
            var name = $"cmms_migration_tests_{Guid.NewGuid():N}";
            await using (var connection = new NpgsqlConnection("Host=localhost;Port=5432;Database=postgres;Username=cmms_app;Password=cmms_app_password"))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = $"CREATE DATABASE \"{name}\"";
                await command.ExecuteNonQueryAsync();
            }

            var options = new DbContextOptionsBuilder<CmmsDbContext>()
                .UseNpgsql($"Host=localhost;Port=5432;Database={name};Username=cmms_app;Password=cmms_app_password")
                .Options;
            return new MigrationDatabase(name, new CmmsDbContext(options));
        }

        public async Task<bool> ExistsAsync(string relation)
        {
            await using var command = Context.Database.GetDbConnection().CreateCommand();
            if (command.Connection!.State != System.Data.ConnectionState.Open)
            {
                await command.Connection.OpenAsync();
            }

            command.CommandText = "SELECT to_regclass(@relation) IS NOT NULL";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "relation";
            parameter.Value = relation;
            command.Parameters.Add(parameter);
            return (bool)(await command.ExecuteScalarAsync())!;
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await using var connection = new NpgsqlConnection("Host=localhost;Port=5432;Database=postgres;Username=cmms_app;Password=cmms_app_password");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"DROP DATABASE IF EXISTS \"{Name}\" WITH (FORCE)";
            await command.ExecuteNonQueryAsync();
        }
    }
}












