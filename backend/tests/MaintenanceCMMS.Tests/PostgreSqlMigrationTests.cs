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
    private static Task<int> SeedLegacyAssetAsync(CmmsDbContext context, string faenaCode, string familyCode, string assetCode) =>
        context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO faenas (id, codigo, nombre, activo, created_at_utc)
            VALUES (gen_random_uuid(), {faenaCode}, {faenaCode}, true, now());
            INSERT INTO familias_equipo (id, codigo, nombre, activo, created_at_utc)
            VALUES (gen_random_uuid(), {familyCode}, {familyCode}, true, now());
            INSERT INTO estados_operacionales_activo (id, codigo, nombre, activo, created_at_utc)
            VALUES (gen_random_uuid(), 'OPERATIVO_FAENA', 'Operativo', true, now());
            INSERT INTO activos (id, codigo, nombre, faena_id, familia_equipo_id, estado_operacional_id, estado_registro, tipo_activo, ficha_validada, created_at_utc)
            SELECT gen_random_uuid(), {assetCode}, 'Activo legado', f.id, fam.id, s.id, 'vigente', 'Equipo', false, now()
            FROM faenas f CROSS JOIN familias_equipo fam CROSS JOIN estados_operacionales_activo s
            WHERE f.codigo = {faenaCode} AND fam.codigo = {familyCode} AND s.codigo = 'OPERATIVO_FAENA';
            """);

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

        database.Context.Faenas.Add(new FaenaEntity { Code = "MIG-FAE", Name = "Faena migraciÃƒÆ’Ã‚Â³n", IsActive = true });
        await database.Context.SaveChangesAsync();

        var service = new InventoryService(
            database.Context,
            new PostgreSqlAuditService(database.Context, new AuditContextAccessor()),
            new AuthorizationPolicyService());
        await service.CreateWarehouseAsync(new CreateWarehouseRequest("MIG-BOD", "Bodega migraciÃƒÆ’Ã‚Â³n", "MIG-FAE", WarehouseType.Central), Admin, CancellationToken.None);
        var part = await service.CreateSparePartAsync(new CreateSparePartRequest("Repuesto migraciÃƒÆ’Ã‚Â³n", "UN"), Admin, CancellationToken.None);
        var movement = await service.RegisterMovementAsync(
            new StockMovementRequest(StockMovementType.Reception, part.Summary.Codigo, 2, "RecepciÃƒÆ’Ã‚Â³n migraciÃƒÆ’Ã‚Â³n", BodegaCodigo: "MIG-BOD"),
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
        await SeedLegacyAssetAsync(database.Context, "UPG-FAE", "UPG-FAM", "UPG-ACT");

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
        var type = new AssetTypeEntity { Code = "TH-TIPO", Name = "Tipo TH", IsActive = true };
        database.Context.AssetTypes.Add(type);
        await database.Context.SaveChangesAsync();
        var faena = new FaenaEntity { Code = "TH-FAE", Name = "Faena TH", IsActive = true };
        var technicalLocation = new TechnicalLocationEntity { Code = "TH-UT", Name = "Ubicacion TH", Faena = faena, IsObsolete = false };
        var family = new EquipmentFamilyEntity { Code = "TH-FAM", Name = "Familia TH", AssetTypeId = type.Id, IsActive = true };
        var state = new AssetOperationalStateEntity { Code = "OPERATIVO_FAENA", Name = "Operativo", IsActive = true };
        faena.TechnicalLocation = technicalLocation;
        database.Context.AddRange(faena, technicalLocation, family, state, new AssetEntity { Code = "TH-ACT", Name = "Activo TH", AssetTypeId = type.Id, Faena = faena, Family = family, OperationalState = state });
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
        await SeedLegacyAssetAsync(database.Context, "INV-FAE", "INV-FAM", "INV-ACT");
        database.Context.InventoryCatalogs.Add(new InventoryCatalogEntity { Category = "Unit", Code = "UN", Name = "UN", IsActive = true });
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
    [Fact]
    public async Task MigrateFromEmptyOperationalDataSet_RemovesLegacyTableAndRecordsRelationalMigration()
    {
        await using var database = await MigrationDatabase.CreateAsync();
        var migrator = database.Context.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(database.OperationalUnitAllowedComponentsMigrationId);

        Assert.True(await database.ExistsAsync("conjuntos_datos_operacionales"));

        await database.Context.Database.MigrateAsync();

        Assert.False(await database.ExistsAsync("conjuntos_datos_operacionales"));
        Assert.Contains(
            database.RelationalOperationalModulesMigrationId,
            await database.Context.Database.GetAppliedMigrationsAsync());
    }

    [Fact]
    public async Task MigrateFromPopulatedOperationalDataSet_RefusesMigrationAndPreservesLegacyTableAndHistory()
    {
        await using var database = await MigrationDatabase.CreateAsync();
        var migrator = database.Context.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(database.OperationalUnitAllowedComponentsMigrationId);
        await database.Context.Database.ExecuteSqlRawAsync("""
            INSERT INTO conjuntos_datos_operacionales (id, codigo, contenido, created_at_utc)
            VALUES (gen_random_uuid(), 'LEGACY-JSON', '{{"source":"legacy"}}'::jsonb, NOW());
            """);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => database.Context.Database.MigrateAsync());

        Assert.Equal("P0001", exception.SqlState);
        Assert.True(await database.ExistsAsync("conjuntos_datos_operacionales"));
        var appliedMigrations = await database.Context.Database.GetAppliedMigrationsAsync();
        Assert.Contains(database.OperationalUnitAllowedComponentsMigrationId, appliedMigrations);
        Assert.DoesNotContain(database.RelationalOperationalModulesMigrationId, appliedMigrations);
    }
    [Fact]
    public async Task MigrateFromDuplicateTechnicalLocations_RefusesOneToOneMigrationAndPreservesRows()
    {
        await using var database = await MigrationDatabase.CreateAsync();
        var migrator = database.Context.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(database.RelationalOperationalModulesMigrationId);

        var faenaId = Guid.NewGuid();
        await database.Context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO faenas (id, codigo, nombre, activo, created_at_utc)
            VALUES ({faenaId}, 'MIG-DUP-FAENA', 'Faena duplicada de migracion', true, now());

            INSERT INTO ubicaciones_tecnicas (id, codigo, nombre, nombre_normalizado, faena_id, obsoleto, created_at_utc)
            VALUES
                (gen_random_uuid(), 'MIG-DUP-UT-01', 'Ubicacion tecnica 01', 'ubicacion tecnica 01', {faenaId}, false, now()),
                (gen_random_uuid(), 'MIG-DUP-UT-02', 'Ubicacion tecnica 02', 'ubicacion tecnica 02', {faenaId}, false, now());
            """);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => database.Context.Database.MigrateAsync());

        Assert.Equal("P0001", exception.SqlState);
        Assert.Contains("resolve faenas with more than one technical location", exception.MessageText);

        await using var verification = database.NewContext();
        Assert.Equal(1, await verification.Faenas.CountAsync(item => item.Id == faenaId));
        Assert.Equal(2, await verification.TechnicalLocations.CountAsync(item => item.FaenaId == faenaId));

        var appliedMigrations = await verification.Database.GetAppliedMigrationsAsync();
        Assert.Contains(database.RelationalOperationalModulesMigrationId, appliedMigrations);
        Assert.DoesNotContain(database.FaenaTechnicalLocationMigrationId, appliedMigrations);
    }

    [Fact]
    public async Task MigrateFromNodeWithOnlyTechnicalLocation_BackfillsFaenaBeforeRemovingDirectLink()
    {
        await using var database = await MigrationDatabase.CreateAsync();
        var migrator = database.Context.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(database.RelationalOperationalModulesMigrationId);

        var faenaId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();
        await database.Context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO faenas (id, codigo, nombre, activo, created_at_utc)
            VALUES ({faenaId}, 'MIG-BACKFILL-FAENA', 'Faena de backfill', true, now());

            INSERT INTO ubicaciones_tecnicas (id, codigo, nombre, nombre_normalizado, faena_id, obsoleto, created_at_utc)
            VALUES ({locationId}, 'MIG-BACKFILL-UT', 'Ubicacion de backfill', 'ubicacion de backfill', {faenaId}, false, now());

            INSERT INTO nodos_tecnicos (
                id, codigo, nombre, nombre_normalizado, nivel, faena_id, ubicacion_tecnica_id, obsoleto, created_at_utc)
            VALUES (
                {nodeId}, 'MIG-BACKFILL-NODE', 'Nodo de backfill', 'nodo de backfill', 'Sistema', NULL, {locationId}, false, now());
            """);

        await database.Context.Database.MigrateAsync();

        await using var verification = database.NewContext();
        var node = await verification.TechnicalNodes.SingleAsync(item => item.Id == nodeId);
        Assert.Equal(faenaId, node.FaenaId);
        Assert.False(await database.ColumnExistsAsync("nodos_tecnicos", "ubicacion_tecnica_id"));
        Assert.Contains(database.FaenaTechnicalLocationMigrationId, await verification.Database.GetAppliedMigrationsAsync());
    }
    private sealed class MigrationDatabase : IAsyncDisposable
    {
        private MigrationDatabase(string name, string adminConnectionString, CmmsDbContext context)
        {
            Name = name;
            AdminConnectionString = adminConnectionString;
            Context = context;
        }

        public string Name { get; }
        public string AdminConnectionString { get; }
        public CmmsDbContext Context { get; }
        public string WorkMigrationId => "202607090003_WorkNotificationsAndOrdersPostgreSql";
        public string InventoryMigrationId => "20260710134248_InventoryDomainPostgreSql";
        public string TechnicalHierarchyMigrationId => "20260710164638_TechnicalHierarchyDomainPostgreSql";
        public string OperationalUnitAllowedComponentsMigrationId => "20260714170216_OperationalUnitAllowedComponents";
        public string RelationalOperationalModulesMigrationId => "20260715123551_RelationalOperationalModules";
        public string FaenaTechnicalLocationMigrationId => "20260715201243_FaenaTechnicalLocationOneToOne";
        public static async Task<MigrationDatabase> CreateAsync()
        {
            var name = $"cmms_test_migration_{Guid.NewGuid():N}";
            var admin = await PostgreSqlWorkTestFixture.GetAdminConnectionStringAsync();
            await PostgreSqlWorkTestFixture.CreateDatabaseAsync(name, admin);
            var options = new DbContextOptionsBuilder<CmmsDbContext>().UseNpgsql(PostgreSqlWorkTestFixture.ConnectionString(admin, name)).Options;
            return new MigrationDatabase(name, admin, new CmmsDbContext(options));
        }

        public CmmsDbContext NewContext() => new(new DbContextOptionsBuilder<CmmsDbContext>().UseNpgsql(PostgreSqlWorkTestFixture.ConnectionString(AdminConnectionString, Name)).Options);

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
            await PostgreSqlWorkTestFixture.DropDatabaseAsync(Name, AdminConnectionString);
        }
    }
}
