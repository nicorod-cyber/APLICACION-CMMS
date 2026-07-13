using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.Inventory;
using MaintenanceCMMS.Application.TechnicalHierarchy;
using MaintenanceCMMS.Domain.Enums;
using MaintenanceCMMS.Infrastructure.Auditing;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using MaintenanceCMMS.Infrastructure.Inventory;
using MaintenanceCMMS.Infrastructure.TechnicalHierarchy;
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
        [AuthPermissions.Administration, AuthPermissions.AdjustStock, AuthPermissions.ViewGlobalWarehouses, AuthPermissions.ManageTechnicalHierarchy],
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

    [Fact]
    public async Task MigrateFromEmptyDatabase_CreatesTechnicalHierarchyAndSupportsServiceFlow()
    {
        await using var database = await MigrationDatabase.CreateAsync();
        await database.Context.Database.MigrateAsync();
        foreach (var table in new[] { "ubicaciones_tecnicas", "nodos_tecnicos", "nodo_tecnico_familias", "nodo_tecnico_activos", "nodo_tecnico_aliases" }) Assert.True(await database.ExistsAsync(table));
        var faena = new FaenaEntity { Code = "TH-FAE", Name = "Faena TH", IsActive = true };
        var family = new EquipmentFamilyEntity { Code = "TH-FAM", Name = "Familia TH", IsActive = true };
        var state = new AssetOperationalStateEntity { Code = "OPERATIVO_FAENA", Name = "Operativo", IsActive = true };
        database.Context.AddRange(faena, family, state, new AssetEntity { Code = "TH-ACT", Name = "Activo TH", Faena = faena, Family = family, OperationalState = state, AssetType = "Equipo", RecordStatus = "vigente" });
        await database.Context.SaveChangesAsync();
        var service = new TechnicalHierarchyService(database.Context, new PostgreSqlAuditService(database.Context, new AuditContextAccessor()), new AuthorizationPolicyService());
        var node = await service.CreateAsync(new CreateTechnicalNodeRequest("TH-SIS", "Sistema TH", TechnicalHierarchyLevel.Sistema, FaenaCodigo: "TH-FAE", FamiliasEquipo: ["TH-FAM"], ActivosAsignados: ["TH-ACT"]), Admin, CancellationToken.None);
        Assert.Equal("Sistema TH", node.Ruta);
        await using var second = database.NewContext();
        Assert.True(await second.TechnicalNodes.AnyAsync(x => x.Code == "TH-SIS"));
    }

    [Fact]
    public async Task MigrateFromInventory_PreservesPreviousRowsAndAddsTechnicalHierarchy()
    {
        await using var database = await MigrationDatabase.CreateAsync();
        await database.Context.Database.GetService<IMigrator>().MigrateAsync(database.InventoryMigrationId);
        var faena = new FaenaEntity { Code = "INV-FAE", Name = "Faena previa", IsActive = true };
        var family = new EquipmentFamilyEntity { Code = "INV-FAM", Name = "Familia previa", IsActive = true };
        var state = new AssetOperationalStateEntity { Code = "OPERATIVO_FAENA", Name = "Operativo", IsActive = true };
        database.Context.AddRange(faena, family, state, new AssetEntity { Code = "INV-ACT", Name = "Activo previo", Faena = faena, Family = family, OperationalState = state, AssetType = "Equipo", RecordStatus = "vigente" }, new InventoryCatalogEntity { Category = "Unit", Code = "UN", Name = "UN", IsActive = true });
        await database.Context.SaveChangesAsync();
        await database.Context.Database.MigrateAsync();
        Assert.Equal(1, await database.Context.Faenas.CountAsync(x => x.Code == "INV-FAE"));
        Assert.Equal(1, await database.Context.InventoryCatalogs.CountAsync(x => x.Code == "UN"));
        Assert.True(await database.ExistsAsync("nodos_tecnicos"));
        Assert.True(await database.ExistsAsync("ubicaciones_tecnicas"));
    }

    [Fact]
    public async Task RollbackToInventory_RemovesOnlyTechnicalHierarchyObjects()
    {
        await using var database = await MigrationDatabase.CreateAsync();
        await database.Context.Database.MigrateAsync();
        database.Context.InventoryCatalogs.Add(new InventoryCatalogEntity { Category = "Unit", Code = "UN", Name = "UN", IsActive = true });
        await database.Context.SaveChangesAsync();
        await database.Context.Database.GetService<IMigrator>().MigrateAsync(database.InventoryMigrationId);
        foreach (var table in new[] { "ubicaciones_tecnicas", "nodos_tecnicos", "nodo_tecnico_familias", "nodo_tecnico_activos", "nodo_tecnico_aliases" }) Assert.False(await database.ExistsAsync(table));
        foreach (var table in new[] { "usuarios", "faenas", "activos", "catalogos_inventario", "repuestos", "bodegas", "audit_log" }) Assert.True(await database.ExistsAsync(table));
        Assert.Equal(1, await database.Context.InventoryCatalogs.CountAsync(x => x.Code == "UN"));
    }

    [Fact]
    public async Task GeneratedScript_DoesNotDuplicateInventoryOrTechnicalHierarchyTables()
    {
        await using var database = await MigrationDatabase.CreateAsync();
        var script = database.Context.Database.GetService<IMigrator>().GenerateScript("0", null);
        foreach (var table in new[] { "catalogos_inventario", "repuestos", "bodegas", "ubicaciones_tecnicas", "nodos_tecnicos", "nodo_tecnico_familias", "nodo_tecnico_activos", "nodo_tecnico_aliases" })
        {
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(script, $"CREATE TABLE(?: IF NOT EXISTS)? \\\"?{table}\\\"?"));
        }
    }
    [Fact]
    public async Task MigrateFromEmptyDatabase_CreatesFileMetadataColumnsAndIndexes()
    {
        await using var database = await MigrationDatabase.CreateAsync();
        await database.Context.Database.MigrateAsync();

        foreach (var column in new[] { "nombre_almacenado", "extension", "modo_almacenamiento", "proposito", "tipo_entidad", "entidad_id", "ubicacion_fisica", "version_archivo", "eliminado" })
        {
            Assert.True(await database.ColumnExistsAsync("archivos", column));
        }

        foreach (var index in new[] { "IX_archivos_checksum", "IX_archivos_created_at_utc", "IX_archivos_proveedor", "IX_archivos_tipo_entidad_entidad_id_eliminado", "IX_archivos_uri_logica" })
        {
            Assert.True(await database.IndexExistsAsync(index));
        }
    }

    [Fact]
    public async Task MigrateFromTechnicalHierarchy_PreservesLegacyFileMetadata()
    {
        await using var database = await MigrationDatabase.CreateAsync();
        var migrator = database.Context.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(database.TechnicalHierarchyMigrationId);
        await database.Context.Database.ExecuteSqlRawAsync("""
            INSERT INTO archivos (id, created_at_utc, file_key, nombre, proveedor, uri_logica, estado)
            VALUES (gen_random_uuid(), NOW(), 'legacy/file.pdf', 'file.pdf', 'LocalSimulation', '/api/sharepoint/download?fileKey=legacy%2Ffile.pdf', 'vigente');
            """);

        await database.Context.Database.MigrateAsync();

        var file = await database.Context.Files.SingleAsync(item => item.FileKey == "legacy/file.pdf");
        Assert.Equal("file.pdf", file.StoredFileName);
        Assert.Equal("pdf", file.Extension);
        Assert.Equal("Stored", file.Status);
        Assert.Equal(1, file.FileVersion);
    }

    [Fact]
    public async Task RollbackToTechnicalHierarchy_RemovesOnlyFileMetadataColumns()
    {
        await using var database = await MigrationDatabase.CreateAsync();
        await database.Context.Database.MigrateAsync();
        await database.Context.Database.GetService<IMigrator>().MigrateAsync(database.TechnicalHierarchyMigrationId);

        Assert.False(await database.ColumnExistsAsync("archivos", "nombre_almacenado"));
        Assert.False(await database.ColumnExistsAsync("archivos", "modo_almacenamiento"));
        Assert.True(await database.ExistsAsync("archivos"));
        Assert.True(await database.ExistsAsync("nodos_tecnicos"));
        Assert.True(await database.ExistsAsync("repuestos"));
    }
    [Fact]
    public async Task MigrateFromEmptyDatabase_CreatesPhaseBAlertObjects()
    {
        await using var database = await MigrationDatabase.CreateAsync();
        await database.Context.Database.MigrateAsync();
        foreach (var table in new[] { "plantillas_pdf", "reglas_alerta", "regla_alerta_destinatarios", "alertas", "notificaciones", "notificacion_destinatarios", "notificacion_intentos" })
        {
            Assert.True(await database.ExistsAsync(table));
        }
    }

    [Fact]
    public async Task RollbackToFileMetadata_RemovesOnlyPhaseBObjects()
    {
        await using var database = await MigrationDatabase.CreateAsync();
        await database.Context.Database.MigrateAsync();
        await database.Context.Database.GetService<IMigrator>().MigrateAsync("20260711041342_FileMetadataPostgreSql");
        foreach (var table in new[] { "plantillas_pdf", "reglas_alerta", "regla_alerta_destinatarios", "alertas", "notificaciones", "notificacion_destinatarios", "notificacion_intentos" })
        {
            Assert.False(await database.ExistsAsync(table));
        }
        Assert.True(await database.ExistsAsync("archivos"));
        Assert.True(await database.ExistsAsync("nodos_tecnicos"));
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
        public string InventoryMigrationId => "20260710134248_InventoryDomainPostgreSql";
        public string TechnicalHierarchyMigrationId => "20260710164638_TechnicalHierarchyDomainPostgreSql";
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

        public CmmsDbContext NewContext()
        {
            var options = new DbContextOptionsBuilder<CmmsDbContext>()
                .UseNpgsql($"Host=localhost;Port=5432;Database={Name};Username=cmms_app;Password=cmms_app_password")
                .Options;
            return new CmmsDbContext(options);
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

        public async Task<bool> ColumnExistsAsync(string table, string column)
        {
            await using var command = Context.Database.GetDbConnection().CreateCommand();
            if (command.Connection!.State != System.Data.ConnectionState.Open) await command.Connection.OpenAsync();
            command.CommandText = "SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = @table AND column_name = @column)";
            var tableParameter = command.CreateParameter(); tableParameter.ParameterName = "table"; tableParameter.Value = table;
            var columnParameter = command.CreateParameter(); columnParameter.ParameterName = "column"; columnParameter.Value = column;
            command.Parameters.Add(tableParameter); command.Parameters.Add(columnParameter);
            return (bool)(await command.ExecuteScalarAsync())!;
        }

        public async Task<bool> IndexExistsAsync(string index)
        {
            await using var command = Context.Database.GetDbConnection().CreateCommand();
            if (command.Connection!.State != System.Data.ConnectionState.Open) await command.Connection.OpenAsync();
            command.CommandText = "SELECT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = @index)";
            var parameter = command.CreateParameter(); parameter.ParameterName = "index"; parameter.Value = index;
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












