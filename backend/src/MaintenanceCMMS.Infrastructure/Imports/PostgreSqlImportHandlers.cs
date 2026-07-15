using System.Globalization;
using MaintenanceCMMS.Application.Imports;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceCMMS.Infrastructure.Imports;

/// <summary>
/// Import handlers are deliberately bounded to typed aggregate roots.  Excel is
/// an input format only; it is never used as the operational data store.
/// </summary>
public interface IPostgreSqlImportHandler
{
    string SchemaName { get; }

    Task<IReadOnlyCollection<PostgreSqlImportRowResult>> AnalyzeAsync(
        IReadOnlyCollection<PostgreSqlImportRow> rows,
        CancellationToken cancellationToken);

    Task ApplyAsync(
        IReadOnlyCollection<PostgreSqlImportRow> rows,
        string appliedBy,
        CancellationToken cancellationToken);
}

public sealed record PostgreSqlImportRow(int RowNumber, IReadOnlyDictionary<string, string?> Values);

public sealed record PostgreSqlImportRowResult(
    int RowNumber,
    string Operation,
    IReadOnlyCollection<ExcelImportValidationError> Errors);

public sealed class PostgreSqlImportHandlerResolver(IEnumerable<IPostgreSqlImportHandler> handlers)
{
    private readonly IReadOnlyDictionary<string, IPostgreSqlImportHandler> _handlers = handlers
        .ToDictionary(handler => handler.SchemaName, StringComparer.OrdinalIgnoreCase);

    public IPostgreSqlImportHandler GetRequired(string schemaName) =>
        _handlers.TryGetValue(schemaName, out var handler)
            ? handler
            : throw new DomainException($"El esquema '{schemaName}' no tiene un importador PostgreSQL habilitado.");
}

public abstract class PostgreSqlImportHandlerBase(CmmsDbContext db) : IPostgreSqlImportHandler
{
    protected CmmsDbContext Db { get; } = db;
    public abstract string SchemaName { get; }

    public abstract Task<IReadOnlyCollection<PostgreSqlImportRowResult>> AnalyzeAsync(
        IReadOnlyCollection<PostgreSqlImportRow> rows,
        CancellationToken cancellationToken);

    public abstract Task ApplyAsync(
        IReadOnlyCollection<PostgreSqlImportRow> rows,
        string appliedBy,
        CancellationToken cancellationToken);

    protected static string Value(IReadOnlyDictionary<string, string?> values, string name) =>
        values.TryGetValue(name, out var value) ? value?.Trim() ?? string.Empty : string.Empty;

    protected static string Code(IReadOnlyDictionary<string, string?> values, string name) =>
        Value(values, name).ToUpperInvariant();

    protected static string? Empty(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    protected static bool Bool(IReadOnlyDictionary<string, string?> values, string name, bool fallback = false)
    {
        var value = Value(values, name);
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Equals("si", StringComparison.OrdinalIgnoreCase) ||
              value.Equals("sÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â­", StringComparison.OrdinalIgnoreCase) ||
              value.Equals("activo", StringComparison.OrdinalIgnoreCase) ||
              value.Equals("activa", StringComparison.OrdinalIgnoreCase) ||
              value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
    }

    protected static decimal Decimal(IReadOnlyDictionary<string, string?> values, string name, decimal fallback = 0m) =>
        decimal.TryParse(Value(values, name), NumberStyles.Number, CultureInfo.InvariantCulture, out var invariant) ||
        decimal.TryParse(Value(values, name), NumberStyles.Number, CultureInfo.GetCultureInfo("es-CL"), out invariant)
            ? invariant
            : fallback;

    protected static ExcelImportValidationError Error(PostgreSqlImportRow row, string column, string message) =>
        new(row.RowNumber, column, message);

    protected static IReadOnlyCollection<PostgreSqlImportRowResult> DuplicateKeyErrors(
        IReadOnlyCollection<PostgreSqlImportRow> rows,
        string keyColumn)
    {
        var duplicates = rows
            .Where(row => !string.IsNullOrWhiteSpace(Code(row.Values, keyColumn)))
            .GroupBy(row => Code(row.Values, keyColumn), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .SelectMany(group => group.Select(row => row.RowNumber))
            .ToHashSet();

        return rows.Select(row => new PostgreSqlImportRowResult(
            row.RowNumber,
            duplicates.Contains(row.RowNumber) ? "Error" : "Nuevo",
            duplicates.Contains(row.RowNumber)
                ? [Error(row, keyColumn, "La clave se repite dentro del archivo.")]
                : Array.Empty<ExcelImportValidationError>())).ToArray();
    }
}

public sealed class FaenaPostgreSqlImportHandler(CmmsDbContext db) : PostgreSqlImportHandlerBase(db)
{
    public override string SchemaName => "faenas";

    public override async Task<IReadOnlyCollection<PostgreSqlImportRowResult>> AnalyzeAsync(IReadOnlyCollection<PostgreSqlImportRow> rows, CancellationToken ct)
    {
        var existing = (await Db.Faenas.AsNoTracking().ToListAsync(ct)).ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);
        var duplicate = DuplicateKeyErrors(rows, "Codigo").ToDictionary(item => item.RowNumber);
        return rows.Select(row =>
        {
            var errors = duplicate[row.RowNumber].Errors.ToList();
            if (string.IsNullOrWhiteSpace(Code(row.Values, "Codigo"))) errors.Add(Error(row, "Codigo", "El cÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â³digo es obligatorio."));
            if (string.IsNullOrWhiteSpace(Value(row.Values, "Nombre"))) errors.Add(Error(row, "Nombre", "El nombre es obligatorio."));
            var operation = errors.Count > 0 ? "Error" : existing.ContainsKey(Code(row.Values, "Codigo")) ? "Actualizado" : "Nuevo";
            return new PostgreSqlImportRowResult(row.RowNumber, operation, errors);
        }).ToArray();
    }

    public override async Task ApplyAsync(IReadOnlyCollection<PostgreSqlImportRow> rows, string appliedBy, CancellationToken ct)
    {
        var keys = rows.Select(row => Code(row.Values, "Codigo")).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existing = (await Db.Faenas.Where(item => keys.Contains(item.Code)).ToListAsync(ct)).ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var code = Code(row.Values, "Codigo");
            if (!existing.TryGetValue(code, out var entity)) Db.Faenas.Add(entity = new FaenaEntity { Code = code });
            entity.Name = Value(row.Values, "Nombre");
            entity.IsActive = Bool(row.Values, "Estado", true);
            entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }
    }
}

public sealed class TechnicalLocationPostgreSqlImportHandler(CmmsDbContext db) : PostgreSqlImportHandlerBase(db)
{
    public override string SchemaName => "ubicaciones_tecnicas";

    public override async Task<IReadOnlyCollection<PostgreSqlImportRowResult>> AnalyzeAsync(IReadOnlyCollection<PostgreSqlImportRow> rows, CancellationToken ct)
    {
        var faenas = (await Db.Faenas.AsNoTracking().ToListAsync(ct)).Select(item => item.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var locations = (await Db.TechnicalLocations.AsNoTracking().ToListAsync(ct)).Select(item => item.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var incoming = rows.Select(row => Code(row.Values, "Codigo")).Where(code => code.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existing = locations;
        var duplicate = DuplicateKeyErrors(rows, "Codigo").ToDictionary(item => item.RowNumber);
        return rows.Select(row =>
        {
            var errors = duplicate[row.RowNumber].Errors.ToList();
            var code = Code(row.Values, "Codigo");
            var faena = Code(row.Values, "FaenaCodigo");
            var parent = Code(row.Values, "CodigoPadre");
            if (code.Length == 0) errors.Add(Error(row, "Codigo", "El cÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â³digo es obligatorio."));
            if (Value(row.Values, "Nombre").Length == 0) errors.Add(Error(row, "Nombre", "El nombre es obligatorio."));
            if (!faenas.Contains(faena)) errors.Add(Error(row, "FaenaCodigo", $"La faena '{faena}' no existe."));
            if (parent.Length > 0 && parent.Equals(code, StringComparison.OrdinalIgnoreCase)) errors.Add(Error(row, "CodigoPadre", "Una ubicaciÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â³n no puede ser padre de sÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â­ misma."));
            else if (parent.Length > 0 && !locations.Contains(parent) && !incoming.Contains(parent)) errors.Add(Error(row, "CodigoPadre", $"La ubicaciÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â³n padre '{parent}' no existe."));
            return new PostgreSqlImportRowResult(row.RowNumber, errors.Count > 0 ? "Error" : existing.Contains(code) ? "Actualizado" : "Nuevo", errors);
        }).ToArray();
    }

    public override async Task ApplyAsync(IReadOnlyCollection<PostgreSqlImportRow> rows, string appliedBy, CancellationToken ct)
    {
        var faenas = (await Db.Faenas.ToListAsync(ct)).ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);
        var keys = rows.Select(row => Code(row.Values, "Codigo")).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var locations = (await Db.TechnicalLocations.Where(item => keys.Contains(item.Code)).ToListAsync(ct)).ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var code = Code(row.Values, "Codigo");
            if (!locations.TryGetValue(code, out var entity)) Db.TechnicalLocations.Add(entity = new TechnicalLocationEntity { Code = code, CreatedByUserId = appliedBy });
            entity.Name = Value(row.Values, "Nombre");
            entity.NormalizedName = entity.Name.ToUpperInvariant();
            entity.FaenaId = faenas[Code(row.Values, "FaenaCodigo")].Id;
            entity.UpdatedByUserId = appliedBy;
            entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
            locations[code] = entity;
        }
        await Db.SaveChangesAsync(ct);

        var allCodes = rows.Select(row => Code(row.Values, "Codigo")).Concat(rows.Select(row => Code(row.Values, "CodigoPadre"))).Where(code => code.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var allLocations = (await Db.TechnicalLocations.Where(item => allCodes.Contains(item.Code)).ToListAsync(ct)).ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var parentCode = Code(row.Values, "CodigoPadre");
            allLocations[Code(row.Values, "Codigo")].ParentId = parentCode.Length == 0 ? null : allLocations[parentCode].Id;
        }
    }
}

public sealed class WarehousePostgreSqlImportHandler(CmmsDbContext db) : PostgreSqlImportHandlerBase(db)
{
    public override string SchemaName => "bodegas";

    public override async Task<IReadOnlyCollection<PostgreSqlImportRowResult>> AnalyzeAsync(IReadOnlyCollection<PostgreSqlImportRow> rows, CancellationToken ct)
    {
        var faenas = (await Db.Faenas.AsNoTracking().ToListAsync(ct)).Select(item => item.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var types = (await Db.InventoryCatalogs.AsNoTracking().Where(item => item.Category == "WarehouseType" && item.IsActive).ToListAsync(ct)).Select(item => item.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existing = (await Db.Warehouses.AsNoTracking().ToListAsync(ct)).Select(item => item.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var duplicate = DuplicateKeyErrors(rows, "Codigo").ToDictionary(item => item.RowNumber);
        return rows.Select(row =>
        {
            var errors = duplicate[row.RowNumber].Errors.ToList(); var code = Code(row.Values, "Codigo"); var faena = Code(row.Values, "FaenaCodigo"); var type = Code(row.Values, "TipoBodega");
            if (code.Length == 0) errors.Add(Error(row, "Codigo", "El cÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â³digo es obligatorio.")); if (Value(row.Values, "Nombre").Length == 0) errors.Add(Error(row, "Nombre", "El nombre es obligatorio."));
            if (!faenas.Contains(faena)) errors.Add(Error(row, "FaenaCodigo", $"La faena '{faena}' no existe."));
            if (type.Length > 0 && !types.Contains(type)) errors.Add(Error(row, "TipoBodega", $"El tipo de bodega '{type}' no existe."));
            return new PostgreSqlImportRowResult(row.RowNumber, errors.Count > 0 ? "Error" : existing.Contains(code) ? "Actualizado" : "Nuevo", errors);
        }).ToArray();
    }

    public override async Task ApplyAsync(IReadOnlyCollection<PostgreSqlImportRow> rows, string appliedBy, CancellationToken ct)
    {
        var faenas = (await Db.Faenas.ToListAsync(ct)).ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);
        var types = (await Db.InventoryCatalogs.Where(item => item.Category == "WarehouseType" && item.IsActive).ToListAsync(ct)).ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);
        var keys = rows.Select(row => Code(row.Values, "Codigo")).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existing = (await Db.Warehouses.Where(item => keys.Contains(item.Code)).ToListAsync(ct)).ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var code = Code(row.Values, "Codigo"); if (!existing.TryGetValue(code, out var entity)) Db.Warehouses.Add(entity = new WarehouseEntity { Code = code, CreatedByUserId = appliedBy });
            var type = Code(row.Values, "TipoBodega"); if (type.Length == 0) type = "FAENA";
            entity.Name = Value(row.Values, "Nombre"); entity.FaenaId = faenas[Code(row.Values, "FaenaCodigo")].Id; entity.TypeId = types[type].Id;
            entity.Location = Empty(Value(row.Values, "Ubicacion")); entity.ResponsibleUserId = Empty(Value(row.Values, "Responsable")); entity.IsActive = Bool(row.Values, "Activa", true); entity.AllowsNegativeStock = Bool(row.Values, "PermiteStockNegativo"); entity.UpdatedByUserId = appliedBy; entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }
    }
}

public sealed class SparePartPostgreSqlImportHandler(CmmsDbContext db) : PostgreSqlImportHandlerBase(db)
{
    public override string SchemaName => "repuestos";

    public override async Task<IReadOnlyCollection<PostgreSqlImportRowResult>> AnalyzeAsync(IReadOnlyCollection<PostgreSqlImportRow> rows, CancellationToken ct)
    {
        var units = (await Db.InventoryCatalogs.AsNoTracking().Where(item => item.Category == "Unit" && item.IsActive).ToListAsync(ct)).Select(item => item.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existing = (await Db.SpareParts.AsNoTracking().ToListAsync(ct)).Select(item => item.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var duplicate = DuplicateKeyErrors(rows, "Codigo").ToDictionary(item => item.RowNumber);
        return rows.Select(row =>
        {
            var errors = duplicate[row.RowNumber].Errors.ToList(); var code = Code(row.Values, "Codigo"); var unit = Code(row.Values, "UnidadMedida");
            if (code.Length == 0) errors.Add(Error(row, "Codigo", "El cÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â³digo es obligatorio.")); if (Value(row.Values, "Descripcion").Length == 0) errors.Add(Error(row, "Descripcion", "La descripciÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â³n es obligatoria."));
            if (unit.Length > 0 && !units.Contains(unit)) errors.Add(Error(row, "UnidadMedida", $"La unidad '{unit}' no existe."));
            foreach (var number in new[] { "StockMinimo", "StockMaximo", "PuntoReposicion" }) if (Decimal(row.Values, number) < 0) errors.Add(Error(row, number, "El valor no puede ser negativo."));
            return new PostgreSqlImportRowResult(row.RowNumber, errors.Count > 0 ? "Error" : existing.Contains(code) ? "Actualizado" : "Nuevo", errors);
        }).ToArray();
    }

    public override async Task ApplyAsync(IReadOnlyCollection<PostgreSqlImportRow> rows, string appliedBy, CancellationToken ct)
    {
        var units = (await Db.InventoryCatalogs.Where(item => item.Category == "Unit" && item.IsActive).ToListAsync(ct)).ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);
        var keys = rows.Select(row => Code(row.Values, "Codigo")).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existing = (await Db.SpareParts.Where(item => keys.Contains(item.Code)).ToListAsync(ct)).ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var code = Code(row.Values, "Codigo"); if (!existing.TryGetValue(code, out var entity)) Db.SpareParts.Add(entity = new SparePartEntity { Code = code, CreatedByUserId = appliedBy });
            var unit = Code(row.Values, "UnidadMedida"); if (unit.Length == 0) unit = "UN";
            entity.SapCode = Empty(Value(row.Values, "CodigoSap")); entity.SupplierCode = Empty(Value(row.Values, "CodigoProveedor")); entity.Description = Value(row.Values, "Descripcion"); entity.TechnicalDescription = Empty(Value(row.Values, "DescripcionTecnica")) ?? entity.Description; entity.UnitId = units[unit].Id;
            entity.Manufacturer = Empty(Value(row.Values, "MarcaFabricante")) ?? Empty(Value(row.Values, "Marca")); entity.ModelReference = Empty(Value(row.Values, "ModeloReferencia")) ?? Empty(Value(row.Values, "Modelo")); entity.IsCritical = Bool(row.Values, "Critico"); entity.MinimumStock = Decimal(row.Values, "StockMinimo"); entity.MaximumStock = Decimal(row.Values, "StockMaximo"); entity.ReorderPoint = Decimal(row.Values, "PuntoReposicion"); entity.LeadTimeDays = (int)Decimal(row.Values, "LeadTimeEsperadoDias"); entity.AverageUnitCost = TryDecimal(row.Values, "CostoUnitarioPromedio"); entity.Status = Empty(Value(row.Values, "Estado")) ?? "Activo"; entity.PreferredSupplier = Empty(Value(row.Values, "ProveedorPreferente")); entity.ReplacementCode = Empty(Value(row.Values, "ReemplazoCodigo")); entity.UpdatedByUserId = appliedBy; entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }
    }

    private static decimal? TryDecimal(IReadOnlyDictionary<string, string?> values, string name) => string.IsNullOrWhiteSpace(Value(values, name)) ? null : Decimal(values, name);
}

public sealed class AssetPostgreSqlImportHandler(CmmsDbContext db) : PostgreSqlImportHandlerBase(db)
{
    public override string SchemaName => "activos";

    public override async Task<IReadOnlyCollection<PostgreSqlImportRowResult>> AnalyzeAsync(IReadOnlyCollection<PostgreSqlImportRow> rows, CancellationToken ct)
    {
        var faenas = (await Db.Faenas.AsNoTracking().ToListAsync(ct)).Select(item => item.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var types = (await Db.AssetTypes.AsNoTracking().ToListAsync(ct)).Select(item => item.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var states = (await Db.AssetOperationalStates.AsNoTracking().ToListAsync(ct)).Select(item => item.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var locations = (await Db.TechnicalLocations.AsNoTracking().ToListAsync(ct)).Select(item => item.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existing = (await Db.Assets.AsNoTracking().ToListAsync(ct)).Select(item => item.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var duplicate = DuplicateKeyErrors(rows, "Codigo").ToDictionary(item => item.RowNumber);
        return rows.Select(row =>
        {
            var errors = duplicate[row.RowNumber].Errors.ToList(); var code = Code(row.Values, "Codigo");
            if (code.Length == 0) errors.Add(Error(row, "Codigo", "El cÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â³digo es obligatorio.")); if (Value(row.Values, "Nombre").Length == 0) errors.Add(Error(row, "Nombre", "El nombre es obligatorio."));
            if (!types.Contains(Code(row.Values, "TipoActivoCodigo"))) errors.Add(Error(row, "TipoActivoCodigo", "El tipo de activo no existe.")); if (!states.Contains(Code(row.Values, "EstadoOperacionalCodigo"))) errors.Add(Error(row, "EstadoOperacionalCodigo", "El estado operacional no existe."));
            var faena = Code(row.Values, "FaenaCodigo"); if (faena.Length > 0 && !faenas.Contains(faena)) errors.Add(Error(row, "FaenaCodigo", $"La faena '{faena}' no existe.")); var location = Code(row.Values, "UbicacionTecnicaCodigo"); if (location.Length > 0 && !locations.Contains(location)) errors.Add(Error(row, "UbicacionTecnicaCodigo", $"La ubicaciÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â³n '{location}' no existe."));
            return new PostgreSqlImportRowResult(row.RowNumber, errors.Count > 0 ? "Error" : existing.Contains(code) ? "Actualizado" : "Nuevo", errors);
        }).ToArray();
    }

    public override async Task ApplyAsync(IReadOnlyCollection<PostgreSqlImportRow> rows, string appliedBy, CancellationToken ct)
    {
        var faenas = (await Db.Faenas.ToListAsync(ct)).ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase); var types = (await Db.AssetTypes.ToListAsync(ct)).ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase); var states = (await Db.AssetOperationalStates.ToListAsync(ct)).ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase); var locations = (await Db.TechnicalLocations.ToListAsync(ct)).ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);
        var keys = rows.Select(row => Code(row.Values, "Codigo")).ToHashSet(StringComparer.OrdinalIgnoreCase); var existing = (await Db.Assets.Where(item => keys.Contains(item.Code)).ToListAsync(ct)).ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var code = Code(row.Values, "Codigo"); if (!existing.TryGetValue(code, out var entity)) Db.Assets.Add(entity = new AssetEntity { Code = code });
            entity.Name = Value(row.Values, "Nombre"); entity.AssetTypeId = types[Code(row.Values, "TipoActivoCodigo")].Id; entity.OperationalStateId = states[Code(row.Values, "EstadoOperacionalCodigo")].Id; var faena = Code(row.Values, "FaenaCodigo"); entity.FaenaId = faena.Length == 0 ? null : faenas[faena].Id; var location = Code(row.Values, "UbicacionTecnicaCodigo"); entity.TechnicalLocationId = location.Length == 0 ? null : locations[location].Id; entity.Brand = Empty(Value(row.Values, "Marca")); entity.Model = Empty(Value(row.Values, "Modelo")); entity.SerialNumber = Empty(Value(row.Values, "NumeroSerie")); entity.Criticality = Empty(Value(row.Values, "Criticidad")); entity.UsageMeasurementType = Empty(Value(row.Values, "TipoMedicionUso")); entity.Observations = Empty(Value(row.Values, "Observaciones")); entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }
    }
}

