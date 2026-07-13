using System.Text.Json;
using MaintenanceCMMS.Application.Abstractions.Data;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql;

public static class CmmsDbContextOperationalExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<IReadOnlyList<DataRow>> ReadOperationalRowsAsync(this CmmsDbContext dbContext, string collectionCode, CancellationToken cancellationToken)
    {
        var dataSet = await dbContext.OperationalDataSets.AsNoTracking().SingleOrDefaultAsync(item => item.Code == collectionCode, cancellationToken);
        var rows = dataSet is null ? [] : Deserialize(dataSet.Payload);
        rows.AddRange(await ReadTypedRowsAsync(dbContext, collectionCode, cancellationToken));
        return rows;
    }

    public static async Task SaveOperationalRowsAsync(this CmmsDbContext dbContext, string collectionCode, IReadOnlyCollection<DataRow> rows, CancellationToken cancellationToken)
    {
        var dataSet = await dbContext.OperationalDataSets.SingleOrDefaultAsync(item => item.Code == collectionCode, cancellationToken);
        var payload = JsonSerializer.Serialize(rows.Select(row => row.Values), JsonOptions);
        if (dataSet is null)
        {
            dbContext.OperationalDataSets.Add(new OperationalDataSetEntity { Code = collectionCode, Payload = payload });
        }
        else
        {
            dataSet.Payload = payload;
            dataSet.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task<List<DataRow>> ReadTypedRowsAsync(CmmsDbContext dbContext, string collectionCode, CancellationToken cancellationToken)
    {
        if (collectionCode == "activos")
        {
            return (await dbContext.Assets.AsNoTracking().Include(item => item.Faena).Include(item => item.Family).Include(item => item.OperationalState).ToListAsync(cancellationToken))
                .Select(item => Row(("Codigo", item.Code), ("Nombre", item.Name), ("FaenaCodigo", item.Faena.Code), ("Familia", item.Family.Code), ("Marca", item.Brand), ("Modelo", item.Model), ("Estado", item.RecordStatus), ("EstadoOperacional", item.OperationalState.Code), ("TipoActivo", item.AssetType))).ToList();
        }

        if (collectionCode == "faenas")
        {
            return (await dbContext.Faenas.AsNoTracking().ToListAsync(cancellationToken))
                .Select(item => Row(("Codigo", item.Code), ("Nombre", item.Name), ("Activa", item.IsActive.ToString()))).ToList();
        }

        if (collectionCode == "ordenes_trabajo")
        {
            return (await dbContext.WorkOrders.AsNoTracking().Include(item => item.Asset).Include(item => item.Faena).Include(item => item.Status).Include(item => item.MaintenanceType).Include(item => item.Priority).Include(item => item.Criticality).ToListAsync(cancellationToken))
                .Select(item => Row(("NumeroOT", item.WorkOrderNumber), ("ActivoCodigo", item.Asset.Code), ("FaenaCodigo", item.Faena.Code), ("Estado", item.Status.Code), ("TipoMantenimiento", item.MaintenanceType.Code), ("Prioridad", item.Priority?.Code), ("Criticidad", item.Criticality?.Code), ("Descripcion", item.Description), ("FechaProgramada", item.ScheduledStartUtc?.ToString("O")), ("FechaFinProgramada", item.ScheduledEndUtc?.ToString("O")), ("PlanPreventivoCodigo", item.PreventivePlanCode))).ToList();
        }

        if (collectionCode == "solicitudes_repuestos")
        {
            return (await dbContext.MaterialRequests.AsNoTracking().Include(item => item.Faena).Include(item => item.WorkOrder).Include(item => item.Warehouse).Include(item => item.Items).ThenInclude(item => item.SparePart).ToListAsync(cancellationToken))
                .Select(item => Row(("NumeroSolicitud", item.RequestNumber), ("SolicitadoEnUtc", item.RequestedAtUtc.ToString("O")), ("AprobadoMantenimientoEnUtc", item.MaintenanceApprovedAtUtc?.ToString("O")), ("RepuestoCodigo", item.Items.FirstOrDefault()?.SparePart?.Code ?? item.Items.FirstOrDefault()?.MasterSparePartCode), ("DescripcionTecnica", item.TechnicalDescription), ("Cantidad", item.Items.Sum(line => line.RequestedQuantity).ToString(System.Globalization.CultureInfo.InvariantCulture)), ("Unidad", item.Unit), ("FaenaCodigo", item.Faena?.Code), ("BodegaCodigo", item.Warehouse?.Code), ("OT", item.WorkOrder?.WorkOrderNumber))).ToList();
        }

        return [];
    }

    private static List<DataRow> Deserialize(string payload)
    {
        var dictionaries = JsonSerializer.Deserialize<List<Dictionary<string, string?>>>(payload, JsonOptions) ?? [];
        return dictionaries.Select(values => new DataRow(new Dictionary<string, string?>(values, StringComparer.OrdinalIgnoreCase))).ToList();
    }

    private static DataRow Row(params (string Key, string? Value)[] values) => new(values.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase));
}
