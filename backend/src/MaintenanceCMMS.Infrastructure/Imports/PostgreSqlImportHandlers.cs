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
              value.Equals("sí", StringComparison.OrdinalIgnoreCase) ||
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
        var faenas = await Db.Faenas.AsNoTracking().Include(item => item.TechnicalLocation).ToListAsync(ct);
        var users = await Db.Users.AsNoTracking().ToListAsync(ct);
        var locations = await Db.TechnicalLocations.AsNoTracking().ToListAsync(ct);
        var faenasByCode = faenas.ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);
        var usersByUsername = users.ToDictionary(item => item.Username, StringComparer.OrdinalIgnoreCase);
        var locationsByCode = locations.ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);
        var locationsByFaena = locations.GroupBy(item => item.FaenaId).ToDictionary(group => group.Key, group => group.ToArray());
        var duplicateFaenaCodes = DuplicateKeyErrors(rows, "Codigo").ToDictionary(item => item.RowNumber);
        var duplicateLocationCodes = DuplicateKeyErrors(rows, "UbicacionTecnicaCodigo").ToDictionary(item => item.RowNumber);

        return rows.Select(row =>
        {
            var errors = duplicateFaenaCodes[row.RowNumber].Errors.Concat(duplicateLocationCodes[row.RowNumber].Errors).ToList();
            ValidateRequired(row, errors, "Codigo", "Nombre", "UbicacionTecnicaCodigo", "UbicacionTecnicaNombre", "Zona", "Cliente", "TipoFaena", "Region", "Comuna", "ResponsableUsername");
            var code = Code(row.Values, "Codigo");
            var locationCode = Code(row.Values, "UbicacionTecnicaCodigo");
            var username = Value(row.Values, "ResponsableUsername");
            if (!TryOptionalDecimal(row.Values, "Latitud", out var latitude)) errors.Add(Error(row, "Latitud", "La latitud debe ser numerica."));
            if (!TryOptionalDecimal(row.Values, "Longitud", out var longitude)) errors.Add(Error(row, "Longitud", "La longitud debe ser numerica."));
            if (latitude is < -90 or > 90) errors.Add(Error(row, "Latitud", "La latitud debe estar entre -90 y 90."));
            if (longitude is < -180 or > 180) errors.Add(Error(row, "Longitud", "La longitud debe estar entre -180 y 180."));
            if (!TryReadFaenaState(row.Values, out _)) errors.Add(Error(row, "Estado", "Estado debe ser Activo, Inactivo, true, false, 1 o 0."));
            if (!usersByUsername.TryGetValue(username, out var responsible)) errors.Add(Error(row, "ResponsableUsername", "El usuario responsable no existe."));
            else if (!responsible.IsActive || responsible.IsLocked) errors.Add(Error(row, "ResponsableUsername", "El usuario responsable debe estar activo y no bloqueado."));

            faenasByCode.TryGetValue(code, out var faena);
            var currentLocation = faena?.TechnicalLocation;
            if (faena is not null && locationsByFaena.TryGetValue(faena.Id, out var assignedLocations) && assignedLocations.Length > 1)
            {
                errors.Add(Error(row, "UbicacionTecnicaCodigo", "La faena tiene mas de una ubicacion tecnica y requiere resolucion manual."));
            }

            if (locationsByCode.TryGetValue(locationCode, out var locationByCode) && locationByCode.Id != currentLocation?.Id)
            {
                errors.Add(Error(row, "UbicacionTecnicaCodigo", "El codigo de ubicacion tecnica ya pertenece a otra faena."));
            }

            var operation = errors.Count > 0
                ? "Error"
                : faena is null
                    ? "Nuevo"
                    : IsSame(faena, currentLocation, row, latitude, longitude, responsible!)
                        ? "SinCambios"
                        : "Actualizado";
            return new PostgreSqlImportRowResult(row.RowNumber, operation, errors);
        }).ToArray();
    }

    public override async Task ApplyAsync(IReadOnlyCollection<PostgreSqlImportRow> rows, string appliedBy, CancellationToken ct)
    {
        var faenaCodes = rows.Select(row => Code(row.Values, "Codigo")).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var usernames = rows.Select(row => Value(row.Values, "ResponsableUsername")).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var locationCodes = rows.Select(row => Code(row.Values, "UbicacionTecnicaCodigo")).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var faenas = (await Db.Faenas.Include(item => item.TechnicalLocation).Where(item => faenaCodes.Contains(item.Code)).ToListAsync(ct)).ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);
        var users = (await Db.Users.Where(item => usernames.Contains(item.Username)).ToListAsync(ct)).ToDictionary(item => item.Username, StringComparer.OrdinalIgnoreCase);
        var locationsByCode = (await Db.TechnicalLocations.Where(item => locationCodes.Contains(item.Code)).ToListAsync(ct)).ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var code = Code(row.Values, "Codigo");
            var locationCode = Code(row.Values, "UbicacionTecnicaCodigo");
            var username = Value(row.Values, "ResponsableUsername");
            if (!users.TryGetValue(username, out var responsible) || !responsible.IsActive || responsible.IsLocked)
            {
                throw new DomainException("El usuario responsable no existe o no esta disponible.");
            }

            if (!faenas.TryGetValue(code, out var faena))
            {
                faena = new FaenaEntity { Code = code };
                Db.Faenas.Add(faena);
                faenas[code] = faena;
            }

            var location = faena.TechnicalLocation;
            if (locationsByCode.TryGetValue(locationCode, out var locationByCode) && locationByCode.Id != location?.Id)
            {
                throw new DomainException("El codigo de ubicacion tecnica ya pertenece a otra faena.");
            }

            if (location is null)
            {
                location = new TechnicalLocationEntity { Faena = faena, FaenaId = faena.Id, IsObsolete = false };
                faena.TechnicalLocation = location;
                Db.TechnicalLocations.Add(location);
            }

            if (!TryReadFaenaState(row.Values, out var isActive))
            {
                throw new DomainException("Estado debe ser Activo, Inactivo, true, false, 1 o 0.");
            }

            if (!TryOptionalDecimal(row.Values, "Latitud", out var latitude) || !TryOptionalDecimal(row.Values, "Longitud", out var longitude) || latitude is < -90 or > 90 || longitude is < -180 or > 180)
            {
                throw new DomainException("Las coordenadas de la faena no son validas.");
            }

            faena.Name = Value(row.Values, "Nombre");
            faena.Zone = Value(row.Values, "Zona");
            faena.Client = Value(row.Values, "Cliente");
            faena.CostCenter = Empty(Value(row.Values, "CentroCostes"));
            faena.FaenaType = Value(row.Values, "TipoFaena");
            faena.Region = Value(row.Values, "Region");
            faena.Commune = Value(row.Values, "Comuna");
            faena.Latitude = latitude;
            faena.Longitude = longitude;
            faena.ResponsibleUserId = responsible.Id;
            faena.ResponsibleUser = responsible;
            faena.IsActive = isActive;
            faena.UpdatedAtUtc = DateTimeOffset.UtcNow;

            location.Code = locationCode;
            location.Name = Value(row.Values, "UbicacionTecnicaNombre");
            location.UpdatedAtUtc = DateTimeOffset.UtcNow;
            locationsByCode[locationCode] = location;
        }
    }

    private static void ValidateRequired(PostgreSqlImportRow row, ICollection<ExcelImportValidationError> errors, params string[] columns)
    {
        foreach (var column in columns)
        {
            if (string.IsNullOrWhiteSpace(Value(row.Values, column))) errors.Add(Error(row, column, "El valor es obligatorio."));
        }
    }

    private static bool TryOptionalDecimal(IReadOnlyDictionary<string, string?> values, string column, out decimal? value)
    {
        var raw = Value(values, column);
        if (string.IsNullOrWhiteSpace(raw))
        {
            value = null;
            return true;
        }

        if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) || decimal.TryParse(raw, NumberStyles.Number, CultureInfo.GetCultureInfo("es-CL"), out parsed))
        {
            value = parsed;
            return true;
        }

        value = null;
        return false;
    }

    private static bool IsSame(FaenaEntity faena, TechnicalLocationEntity? location, PostgreSqlImportRow row, decimal? latitude, decimal? longitude, AppUserEntity responsible)
    {
        if (!TryReadFaenaState(row.Values, out var isActive)) return false;

        return string.Equals(faena.Name, Value(row.Values, "Nombre"), StringComparison.Ordinal) &&
               string.Equals(faena.Zone, Value(row.Values, "Zona"), StringComparison.Ordinal) &&
               string.Equals(faena.Client, Value(row.Values, "Cliente"), StringComparison.Ordinal) &&
               string.Equals(faena.CostCenter, Empty(Value(row.Values, "CentroCostes")), StringComparison.Ordinal) &&
               string.Equals(faena.FaenaType, Value(row.Values, "TipoFaena"), StringComparison.Ordinal) &&
               string.Equals(faena.Region, Value(row.Values, "Region"), StringComparison.Ordinal) &&
               string.Equals(faena.Commune, Value(row.Values, "Comuna"), StringComparison.Ordinal) &&
               faena.Latitude == latitude &&
               faena.Longitude == longitude &&
               faena.ResponsibleUserId == responsible.Id &&
               faena.IsActive == isActive &&
               location is not null &&
               string.Equals(location.Code, Code(row.Values, "UbicacionTecnicaCodigo"), StringComparison.Ordinal) &&
               string.Equals(location.Name, Value(row.Values, "UbicacionTecnicaNombre"), StringComparison.Ordinal);
    }

    private static bool TryReadFaenaState(IReadOnlyDictionary<string, string?> values, out bool isActive)
    {
        var value = Value(values, "Estado");
        if (string.IsNullOrWhiteSpace(value))
        {
            isActive = true;
            return true;
        }

        switch (value.Trim().ToUpperInvariant())
        {
            case "SI":
            case "S\u00CD":
            case "ACTIVO":
            case "ACTIVA":
            case "TRUE":
            case "1":
                isActive = true;
                return true;
            case "NO":
            case "INACTIVO":
            case "INACTIVA":
            case "FALSE":
            case "0":
                isActive = false;
                return true;
            default:
                isActive = true;
                return false;
        }
    }
}

public sealed class TechnicalLocationPostgreSqlImportHandler(CmmsDbContext db) : PostgreSqlImportHandlerBase(db)
{
    public override string SchemaName => "ubicaciones_tecnicas";

    public override async Task<IReadOnlyCollection<PostgreSqlImportRowResult>> AnalyzeAsync(IReadOnlyCollection<PostgreSqlImportRow> rows, CancellationToken ct)
    {
        var faenas = await Db.Faenas.AsNoTracking().Include(item => item.TechnicalLocation).ToListAsync(ct);
        var locations = await Db.TechnicalLocations.AsNoTracking().ToListAsync(ct);
        var faenasByCode = faenas.ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);
        var locationsByCode = locations.ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);
        var duplicateFaenas = DuplicateKeyErrors(rows, "FaenaCodigo").ToDictionary(item => item.RowNumber);
        var duplicateCodes = DuplicateKeyErrors(rows, "Codigo").ToDictionary(item => item.RowNumber);

        return rows.Select(row =>
        {
            var errors = duplicateFaenas[row.RowNumber].Errors.Concat(duplicateCodes[row.RowNumber].Errors).ToList();
            foreach (var column in new[] { "Codigo", "Nombre", "FaenaCodigo" }) if (string.IsNullOrWhiteSpace(Value(row.Values, column))) errors.Add(Error(row, column, "El valor es obligatorio."));
            var faenaCode = Code(row.Values, "FaenaCodigo");
            var code = Code(row.Values, "Codigo");
            if (!faenasByCode.TryGetValue(faenaCode, out var faena)) errors.Add(Error(row, "FaenaCodigo", "La faena no existe."));
            else if (faena.TechnicalLocation is null) errors.Add(Error(row, "FaenaCodigo", "La faena no tiene ubicacion tecnica para actualizar."));
            else if (locationsByCode.TryGetValue(code, out var locationByCode) && locationByCode.Id != faena.TechnicalLocation.Id) errors.Add(Error(row, "Codigo", "El codigo de ubicacion tecnica ya pertenece a otra faena."));

            var operation = errors.Count > 0
                ? "Error"
                : string.Equals(faena!.TechnicalLocation!.Code, code, StringComparison.Ordinal) &&
                  string.Equals(faena.TechnicalLocation.Name, Value(row.Values, "Nombre"), StringComparison.Ordinal) &&
                  faena.TechnicalLocation.IsObsolete == Bool(row.Values, "Obsoleto")
                    ? "SinCambios"
                    : "Actualizado";
            return new PostgreSqlImportRowResult(row.RowNumber, operation, errors);
        }).ToArray();
    }

    public override async Task ApplyAsync(IReadOnlyCollection<PostgreSqlImportRow> rows, string appliedBy, CancellationToken ct)
    {
        var faenaCodes = rows.Select(row => Code(row.Values, "FaenaCodigo")).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var locationCodes = rows.Select(row => Code(row.Values, "Codigo")).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var faenas = (await Db.Faenas.Include(item => item.TechnicalLocation).Where(item => faenaCodes.Contains(item.Code)).ToListAsync(ct)).ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);
        var locationsByCode = (await Db.TechnicalLocations.Where(item => locationCodes.Contains(item.Code)).ToListAsync(ct)).ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var faenaCode = Code(row.Values, "FaenaCodigo");
            var code = Code(row.Values, "Codigo");
            if (!faenas.TryGetValue(faenaCode, out var faena) || faena.TechnicalLocation is null)
            {
                throw new DomainException("La ubicacion tecnica solo puede actualizar la ubicacion unica de una faena existente.");
            }

            var location = faena.TechnicalLocation;
            if (locationsByCode.TryGetValue(code, out var byCode) && byCode.Id != location.Id)
            {
                throw new DomainException("El codigo de ubicacion tecnica ya pertenece a otra faena.");
            }

            location.Code = code;
            location.Name = Value(row.Values, "Nombre");
            location.IsObsolete = Bool(row.Values, "Obsoleto");
            location.UpdatedAtUtc = DateTimeOffset.UtcNow;
            locationsByCode[code] = location;
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
            if (code.Length == 0) errors.Add(Error(row, "Codigo", "El codigo es obligatorio.")); if (Value(row.Values, "Nombre").Length == 0) errors.Add(Error(row, "Nombre", "El nombre es obligatorio."));
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
            if (code.Length == 0) errors.Add(Error(row, "Codigo", "El codigo es obligatorio.")); if (Value(row.Values, "Descripcion").Length == 0) errors.Add(Error(row, "Descripcion", "La descripcion es obligatoria."));
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
        var faenas = await Db.Faenas.AsNoTracking().Include(item => item.TechnicalLocation).ToListAsync(ct);
        var types = await Db.AssetTypes.AsNoTracking().ToListAsync(ct);
        var states = await Db.AssetOperationalStates.AsNoTracking().ToListAsync(ct);
        var criticalities = await Db.WorkCatalogs.AsNoTracking().Where(item => item.Category == "WorkNotificationCriticality" && item.IsActive).ToListAsync(ct);
        var existing = await Db.Assets.AsNoTracking().ToListAsync(ct);
        var faenasByCode = faenas.ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);
        var typeCodes = types.Select(item => item.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var stateCodes = states.Select(item => item.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingCodes = existing.Select(item => item.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var duplicate = DuplicateKeyErrors(rows, "Codigo").ToDictionary(item => item.RowNumber);
        return rows.Select(row =>
        {
            var errors = duplicate[row.RowNumber].Errors.ToList();
            var code = Code(row.Values, "Codigo");
            if (code.Length == 0) errors.Add(Error(row, "Codigo", "El codigo es obligatorio."));
            if (Value(row.Values, "Nombre").Length == 0) errors.Add(Error(row, "Nombre", "El nombre es obligatorio."));
            if (!typeCodes.Contains(Code(row.Values, "TipoActivoCodigo"))) errors.Add(Error(row, "TipoActivoCodigo", "El tipo de activo no existe."));
            if (!stateCodes.Contains(Code(row.Values, "EstadoOperacionalCodigo"))) errors.Add(Error(row, "EstadoOperacionalCodigo", "El estado operacional no existe."));
            var criticality = Empty(Value(row.Values, "Criticidad"));
            if (criticality is not null && !criticalities.Any(item => string.Equals(item.Code, criticality, StringComparison.OrdinalIgnoreCase) || string.Equals(item.Name, criticality, StringComparison.OrdinalIgnoreCase))) errors.Add(Error(row, "Criticidad", $"La criticidad '{criticality}' no existe en el catalogo WorkNotificationCriticality."));
            var faenaCode = Code(row.Values, "FaenaCodigo");
            if (faenaCode.Length > 0)
            {
                if (!faenasByCode.TryGetValue(faenaCode, out var faena)) errors.Add(Error(row, "FaenaCodigo", "La faena no existe."));
                else if (faena.TechnicalLocation is null) errors.Add(Error(row, "FaenaCodigo", "La faena no tiene ubicacion tecnica configurada."));
            }

            return new PostgreSqlImportRowResult(row.RowNumber, errors.Count > 0 ? "Error" : existingCodes.Contains(code) ? "Actualizado" : "Nuevo", errors);
        }).ToArray();
    }

    public override async Task ApplyAsync(IReadOnlyCollection<PostgreSqlImportRow> rows, string appliedBy, CancellationToken ct)
    {
        var faenaCodes = rows.Select(row => Code(row.Values, "FaenaCodigo")).Where(code => code.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var assetCodes = rows.Select(row => Code(row.Values, "Codigo")).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var faenas = (await Db.Faenas.Include(item => item.TechnicalLocation).Where(item => faenaCodes.Contains(item.Code)).ToListAsync(ct)).ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);
        var types = (await Db.AssetTypes.ToListAsync(ct)).ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);
        var states = (await Db.AssetOperationalStates.ToListAsync(ct)).ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);
        var criticalities = await Db.WorkCatalogs.Where(item => item.Category == "WorkNotificationCriticality" && item.IsActive).ToListAsync(ct);
        var existing = (await Db.Assets.Where(item => assetCodes.Contains(item.Code)).ToListAsync(ct)).ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var code = Code(row.Values, "Codigo");
            if (!existing.TryGetValue(code, out var entity))
            {
                entity = new AssetEntity { Code = code };
                Db.Assets.Add(entity);
                existing[code] = entity;
            }

            var faenaCode = Code(row.Values, "FaenaCodigo");
            FaenaEntity? faena = null;
            if (faenaCode.Length > 0)
            {
                if (!faenas.TryGetValue(faenaCode, out faena) || faena.TechnicalLocation is null) throw new DomainException("La faena indicada no tiene ubicacion tecnica configurada.");
            }

            entity.Name = Value(row.Values, "Nombre");
            entity.AssetTypeId = types[Code(row.Values, "TipoActivoCodigo")].Id;
            entity.OperationalStateId = states[Code(row.Values, "EstadoOperacionalCodigo")].Id;
            entity.FaenaId = faena?.Id;
            entity.Brand = Empty(Value(row.Values, "Marca"));
            entity.Model = Empty(Value(row.Values, "Modelo"));
            entity.SerialNumber = Empty(Value(row.Values, "NumeroSerie"));
            var criticality = Empty(Value(row.Values, "Criticidad"));
            var canonicalCriticality = criticality is null ? null : criticalities.SingleOrDefault(item => string.Equals(item.Code, criticality, StringComparison.OrdinalIgnoreCase) || string.Equals(item.Name, criticality, StringComparison.OrdinalIgnoreCase))?.Name;
            if (criticality is not null && canonicalCriticality is null) throw new DomainException($"Fila {row.RowNumber}: la criticidad '{criticality}' no existe en el catalogo WorkNotificationCriticality.");
            entity.Criticality = canonicalCriticality;
            entity.UsageMeasurementType = Empty(Value(row.Values, "TipoMedicionUso"));
            entity.Observations = Empty(Value(row.Values, "Observaciones"));
            entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }
    }
}