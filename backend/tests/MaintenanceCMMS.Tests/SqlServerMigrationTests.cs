using MaintenanceCMMS.Infrastructure.Data.SqlServer.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MaintenanceCMMS.Tests;

public sealed class SqlServerMigrationTests
{
    [Fact]
    public async Task Baseline_creates_sql_server_schema_sequences_and_rowversion()
    {
        await using var fixture = await SqlServerWorkTestFixture.CreateAsync();
        var context = fixture.DbContext;

        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.Contains("20260830050450_SqlServerBaseline", await context.Database.GetAppliedMigrationsAsync());
        Assert.Equal(1, await ScalarAsync<int>(context, "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.activos') AND name = N'row_version' AND system_type_id = 189;"));
        Assert.Equal(8, await ScalarAsync<int>(context, "SELECT COUNT(*) FROM sys.sequences WHERE name IN (N'asset_number_seq', N'material_request_number_seq', N'work_notification_number_seq', N'work_order_number_seq', N'spare_part_number_seq', N'stock_movement_number_seq', N'stock_reservation_number_seq', N'stock_transfer_number_seq');"));
        Assert.Equal(7, await ScalarAsync<int>(context, "SELECT COUNT(*) FROM sys.triggers WHERE name IN (N'trg_activos_estado_operacional_requires_event', N'trg_activos_faena_requires_transfer', N'trg_componentes_unidad_rol_critico', N'trg_valores_atributo_activo_unico', N'trg_vigencias_ubicacion_activo_sin_solape', N'trg_vigencias_ubicacion_fisica_activo_sin_solape', N'trg_matrices_requisitos_sin_solape');"));
    }

    [Fact]
    public async Task Direct_asset_state_change_is_rejected_but_persisted_event_allows_it()
    {
        await using var fixture = await SqlServerWorkTestFixture.CreateAsync();
        var context = fixture.DbContext;
        var asset = await context.Assets.Include(item => item.OperationalState).SingleAsync(item => item.Code == "ACT-1");
        var target = new AssetOperationalStateEntity { Code = "TALLER", Name = "En taller", IsActive = true };
        context.AssetOperationalStates.Add(target);
        await context.SaveChangesAsync();

        var direct = await Assert.ThrowsAsync<SqlException>(() => context.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.activos SET estado_operacional_id = {target.Id} WHERE id = {asset.Id};"));
        Assert.Equal(51001, direct.Number);

        await using var transaction = await context.Database.BeginTransactionAsync();
        var stateEvent = new AssetStateEventEntity
        {
            AssetId = asset.Id,
            PreviousStateId = asset.OperationalStateId,
            NewStateId = target.Id,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            UserId = "migration-test",
            Reason = "Prueba de integridad"
        };
        context.AssetStateEvents.Add(stateEvent);
        await context.SaveChangesAsync();
        await context.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_set_session_context @key=N'cmms.asset_state_event_ids', @value={stateEvent.Id.ToString("D")};");
        asset.OperationalStateId = target.Id;
        await context.SaveChangesAsync();
        await context.Database.ExecuteSqlRawAsync("EXEC sys.sp_set_session_context @key=N'cmms.asset_state_event_ids', @value=NULL;");
        await transaction.CommitAsync();

        Assert.Equal(target.Id, (await context.Assets.AsNoTracking().SingleAsync(item => item.Id == asset.Id)).OperationalStateId);
    }

    [Fact]
    public async Task Direct_asset_faena_change_is_rejected_but_persisted_transfer_allows_it()
    {
        await using var fixture = await SqlServerWorkTestFixture.CreateAsync();
        var context = fixture.DbContext;
        var asset = await context.Assets.SingleAsync(item => item.Code == "ACT-1");
        var destination = new FaenaEntity { Code = "FAE-2", Name = "Faena Dos", IsActive = true };
        context.Faenas.Add(destination);
        await context.SaveChangesAsync();

        var direct = await Assert.ThrowsAsync<SqlException>(() => context.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.activos SET faena_id = {destination.Id} WHERE id = {asset.Id};"));
        Assert.Equal(51002, direct.Number);

        await using var transaction = await context.Database.BeginTransactionAsync();
        var transfer = new AssetTransferEntity
        {
            AssetId = asset.Id,
            OriginFaenaId = asset.FaenaId,
            DestinationFaenaId = destination.Id,
            EffectiveAtUtc = DateTimeOffset.UtcNow,
            Reason = "Prueba de traslado",
            UserId = "migration-test",
            RegisteredAtUtc = DateTimeOffset.UtcNow
        };
        context.AssetTransfers.Add(transfer);
        await context.SaveChangesAsync();
        await context.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_set_session_context @key=N'cmms.asset_transfer_ids', @value={transfer.Id.ToString("D")};");
        asset.FaenaId = destination.Id;
        await context.SaveChangesAsync();
        await context.Database.ExecuteSqlRawAsync("EXEC sys.sp_set_session_context @key=N'cmms.asset_transfer_ids', @value=NULL;");
        await transaction.CommitAsync();

        Assert.Equal(destination.Id, (await context.Assets.AsNoTracking().SingleAsync(item => item.Id == asset.Id)).FaenaId);
    }

    [Fact]
    public async Task Critical_history_delete_and_overlapping_location_period_are_rejected()
    {
        await using var fixture = await SqlServerWorkTestFixture.CreateAsync();
        var context = fixture.DbContext;
        var asset = await context.Assets.SingleAsync(item => item.Code == "ACT-1");
        var startsAt = DateTimeOffset.UtcNow.AddDays(-5);
        context.AssetLocationPeriods.Add(new AssetLocationPeriodEntity { AssetId = asset.Id, FaenaId = asset.FaenaId, ValidFromUtc = startsAt, ValidToUtc = startsAt.AddDays(3) });
        await context.SaveChangesAsync();

        context.AssetLocationPeriods.Add(new AssetLocationPeriodEntity { AssetId = asset.Id, FaenaId = asset.FaenaId, ValidFromUtc = startsAt.AddDays(1), ValidToUtc = startsAt.AddDays(4) });
        var overlap = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal(51006, Assert.IsType<SqlException>(overlap.InnerException).Number);
        context.ChangeTracker.Clear();

        var deletion = await Assert.ThrowsAsync<SqlException>(() => context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dbo.activos WHERE id = {asset.Id};"));
        Assert.Equal(51011, deletion.Number);
    }

    private static async Task<T> ScalarAsync<T>(DbContext context, string sql)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        if (command.Connection!.State != System.Data.ConnectionState.Open) await command.Connection.OpenAsync();
        command.CommandText = sql;
        return (T)Convert.ChangeType((await command.ExecuteScalarAsync())!, typeof(T));
    }
}