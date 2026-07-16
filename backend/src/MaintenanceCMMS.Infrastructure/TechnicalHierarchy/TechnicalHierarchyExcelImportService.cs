using ClosedXML.Excel;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.TechnicalHierarchy;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceCMMS.Infrastructure.TechnicalHierarchy;

public sealed class TechnicalHierarchyExcelImportService : ITechnicalHierarchyExcelImportService
{
    private readonly CmmsDbContext _db;
    private readonly IAuthorizationPolicyService _auth;
    private readonly IAuditService _audit;

    public TechnicalHierarchyExcelImportService(CmmsDbContext db, IAuthorizationPolicyService auth, IAuditService audit) => (_db, _auth, _audit) = (db, auth, audit);

    public async Task<TechnicalHierarchyExcelImportResult> ImportAsync(TechnicalHierarchyExcelImportCommand command, UserAccessContext user, CancellationToken ct)
    {
        if (!_auth.CanManageTechnicalHierarchy(user)) throw new UnauthorizedAccessException("El usuario no tiene permiso para importar jerarquía técnica.");

        var files = new[] { command.UbicacionesTecnicasPath, command.SistemasComponentesPath };
        var errors = new List<string>();
        var warnings = new List<string>();
        var missing = new List<string>();
        foreach (var file in files)
            if (string.IsNullOrWhiteSpace(file) || !File.Exists(file)) errors.Add($"Archivo no encontrado: {file}");
        if (errors.Count > 0) return Result(files, 0, 0, 0, 0, warnings, errors, missing);

        var locations = ReadRows(command.UbicacionesTecnicasPath);
        var nodes = ReadRows(command.SistemasComponentesPath);
        var rowsRead = locations.Count + nodes.Count;
        await ValidateAsync(locations, nodes, errors, missing, ct);
        if (errors.Count > 0 || missing.Count > 0) return Result(files, rowsRead, 0, 0, rowsRead, warnings, errors, missing);

        var inserted = 0;
        var updated = 0;
        var skipped = 0;
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var row in locations)
            {
                var code = Code(Get(row, "Codigo"))!;
                var faena = await FaenaAsync(Get(row, "FaenaCodigo"), ct) ?? throw new DomainException("La faena indicada no existe.");
                var location = faena.TechnicalLocation ?? throw new DomainException($"La faena '{faena.Code}' no tiene una ubicación técnica que se pueda actualizar.");
                var conflictingLocation = await _db.TechnicalLocations.SingleOrDefaultAsync(x => x.Code.ToUpper() == code, ct);
                if (conflictingLocation is not null && conflictingLocation.Id != location.Id)
                    throw new DomainException($"La ubicación técnica '{code}' ya pertenece a otra faena.");

                var obsolete = HasValue(row, "Obsoleto") ? ParseBool(Get(row, "Obsoleto")) : location.IsObsolete;
                var unchanged = string.Equals(location.Code, code, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(location.Name, Get(row, "Nombre"), StringComparison.Ordinal) &&
                                location.IsObsolete == obsolete;
                if (unchanged)
                {
                    skipped++;
                    continue;
                }

                location.Code = code;
                location.Name = Get(row, "Nombre")!;
                location.IsObsolete = obsolete;
                location.UpdatedAtUtc = DateTimeOffset.UtcNow;
                updated++;
                await _db.SaveChangesAsync(ct);
            }

            foreach (var level in new[] { "Sistema", "Subsistema", "Componente", "Subcomponente" })
            foreach (var row in nodes.Where(x => string.Equals(Get(x, "Nivel"), level, StringComparison.OrdinalIgnoreCase)))
            {
                var code = Code(Get(row, "Codigo"))!;
                var existing = await NodeQuery(true).SingleOrDefaultAsync(x => x.Code.ToUpper() == code, ct);
                var parent = await NodeAsync(Get(row, "CodigoPadre"), ct);
                var faena = await FaenaAsync(Get(row, "FaenaCodigo"), ct);
                if (existing is null)
                {
                    existing = new TechnicalNodeEntity { Code = code, CreatedByUserId = command.ImportedBy };
                    _db.TechnicalNodes.Add(existing);
                    inserted++;
                }
                else
                {
                    updated++;
                }

                existing.Name = Get(row, "Nombre")!;
                existing.NormalizedName = string.IsNullOrWhiteSpace(Get(row, "NombreNormalizado"))
                    ? TechnicalHierarchyService.NormalizeName(existing.Name)
                    : Get(row, "NombreNormalizado")!;
                existing.Level = ParseLevel(Get(row, "Nivel")).ToString();
                existing.ParentId = parent?.Id;
                existing.FaenaId = faena?.Id;
                existing.IsObsolete = ParseBool(Get(row, "Obsoleto"));
                existing.UpdatedAtUtc = DateTimeOffset.UtcNow;
                existing.UpdatedByUserId = command.ImportedBy;
                await SyncFamiliesAsync(existing, Split(Get(row, "FamiliasEquipo")), ct);
                await SyncAssetsAsync(existing, Split(Get(row, "ActivosAsignados")), ct);
                SyncAliases(existing, Split(Get(row, "AliasHistoricos")), "Import");
                await _db.SaveChangesAsync(ct);
            }

            await transaction.CommitAsync(ct);
            await _audit.RecordAsync(new AuditEventRequest(command.ImportedBy, "technical_hierarchy.imported", AuditModules.TechnicalHierarchy, "TechnicalHierarchyImport", DateTimeOffset.UtcNow.ToString("O"), NewValue: $"Filas: {rowsRead}", Severity: AuditSeverity.High, Detail: "Importación explícita de jerarquía técnica desde Excel"), ct);
            return Result(files, rowsRead, inserted, updated, skipped, warnings, errors, missing);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            errors.Add(ex.Message);
            return Result(files, rowsRead, 0, 0, rowsRead, warnings, errors, missing);
        }
    }

    private async Task ValidateAsync(IReadOnlyCollection<Dictionary<string, string?>> locations, IReadOnlyCollection<Dictionary<string, string?>> nodes, List<string> errors, List<string> missing, CancellationToken ct)
    {
        var nodeCodes = nodes.Select(x => Code(Get(x, "Codigo"))).Where(x => x is not null).Select(x => x!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var locationCodes = locations.Select(x => Code(Get(x, "Codigo"))).Where(x => x is not null).Select(x => x!).ToArray();
        foreach (var duplicate in locationCodes.GroupBy(x => x, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).Select(group => group.Key))
            errors.Add($"Código de ubicación técnica repetido en el archivo: {duplicate}");

        foreach (var row in locations)
        {
            Required(row, "Codigo", errors);
            Required(row, "Nombre", errors);
            Required(row, "FaenaCodigo", errors);
            var faena = await FaenaAsync(Get(row, "FaenaCodigo"), ct);
            if (faena is null) missing.Add($"Faena inexistente: {Code(Get(row, "FaenaCodigo"))}");
            else if (faena.TechnicalLocation is null) errors.Add($"La faena '{faena.Code}' no tiene una ubicación técnica que se pueda actualizar.");
            else
            {
                var code = Code(Get(row, "Codigo"))!;
                var conflictingLocation = await _db.TechnicalLocations.AsNoTracking().SingleOrDefaultAsync(x => x.Code.ToUpper() == code, ct);
                if (conflictingLocation is not null && conflictingLocation.Id != faena.TechnicalLocation.Id)
                    errors.Add($"La ubicación técnica '{code}' ya pertenece a otra faena.");
            }
        }

        foreach (var row in nodes)
        {
            Required(row, "Codigo", errors);
            Required(row, "Nombre", errors);
            Required(row, "Nivel", errors);
            var level = ParseLevel(Get(row, "Nivel"));
            var parent = Code(Get(row, "CodigoPadre"));
            if (level == TechnicalHierarchyLevel.Sistema && parent is not null) errors.Add($"Sistema con padre no permitido: {Get(row, "Codigo")}");
            if (level != TechnicalHierarchyLevel.Sistema && parent is null) errors.Add($"Nodo sin padre requerido: {Get(row, "Codigo")}");
            if (parent is not null && !nodeCodes.Contains(parent) && !await _db.TechnicalNodes.AnyAsync(x => x.Code.ToUpper() == parent, ct)) missing.Add($"Nodo padre inexistente: {parent}");
            if (!string.IsNullOrWhiteSpace(Get(row, "FaenaCodigo")))
            {
                var faena = await FaenaAsync(Get(row, "FaenaCodigo"), ct);
                if (faena is null) missing.Add($"Faena inexistente: {Code(Get(row, "FaenaCodigo"))}");
                else if (faena.TechnicalLocation is null) errors.Add($"La faena '{faena.Code}' no tiene una ubicación técnica configurada.");
            }
            foreach (var family in Split(Get(row, "FamiliasEquipo")))
                if (!await _db.EquipmentFamilies.AnyAsync(x => x.Code.ToUpper() == Code(family), ct)) missing.Add($"Familia inexistente: {family}");
            foreach (var asset in Split(Get(row, "ActivosAsignados")))
                if (!await _db.Assets.AnyAsync(x => x.Code.ToUpper() == Code(asset), ct)) missing.Add($"Activo inexistente: {asset}");
        }
    }

    private static IReadOnlyCollection<Dictionary<string, string?>> ReadRows(string path)
    {
        using var workbook = new XLWorkbook(path);
        var sheet = workbook.Worksheets.First();
        var used = sheet.RangeUsed();
        if (used is null) return [];
        var headers = used.FirstRow().Cells().Select(cell => cell.GetString().Trim()).ToArray();
        var result = new List<Dictionary<string, string?>>();
        foreach (var row in used.RowsUsed().Skip(1))
        {
            var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < headers.Length; index++) values[headers[index]] = row.Cell(index + 1).GetString();
            if (values.Values.Any(value => !string.IsNullOrWhiteSpace(value))) result.Add(values);
        }
        return result;
    }

    private IQueryable<TechnicalNodeEntity> NodeQuery(bool tracked = false)
    {
        var query = _db.TechnicalNodes.Include(x => x.Families).ThenInclude(x => x.EquipmentFamily).Include(x => x.Assets).ThenInclude(x => x.Asset).Include(x => x.Aliases).AsSplitQuery();
        return tracked ? query : query.AsNoTracking();
    }

    private async Task<FaenaEntity?> FaenaAsync(string? code, CancellationToken ct) => string.IsNullOrWhiteSpace(code)
        ? null
        : await _db.Faenas.Include(x => x.TechnicalLocation).SingleOrDefaultAsync(x => x.Code.ToUpper() == Code(code), ct);

    private async Task<TechnicalNodeEntity?> NodeAsync(string? code, CancellationToken ct) => string.IsNullOrWhiteSpace(code)
        ? null
        : await _db.TechnicalNodes.SingleAsync(x => x.Code.ToUpper() == Code(code), ct);

    private static void Required(Dictionary<string, string?> row, string column, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(Get(row, column))) errors.Add($"Columna requerida vacía: {column}");
    }

    private async Task SyncFamiliesAsync(TechnicalNodeEntity node, IReadOnlyCollection<string> codes, CancellationToken ct)
    {
        var desired = new HashSet<Guid>();
        foreach (var code in codes)
        {
            var normalized = Code(code)!;
            desired.Add((await _db.EquipmentFamilies.SingleAsync(x => x.Code.ToUpper() == normalized, ct)).Id);
        }
        foreach (var item in node.Families.Where(x => !desired.Contains(x.EquipmentFamilyId)).ToArray()) node.Families.Remove(item);
        foreach (var id in desired)
            if (node.Families.All(x => x.EquipmentFamilyId != id)) node.Families.Add(new TechnicalNodeFamilyEntity { TechnicalNodeId = node.Id, EquipmentFamilyId = id });
    }

    private async Task SyncAssetsAsync(TechnicalNodeEntity node, IReadOnlyCollection<string> codes, CancellationToken ct)
    {
        var desired = new HashSet<Guid>();
        foreach (var code in codes)
        {
            var normalized = Code(code)!;
            desired.Add((await _db.Assets.SingleAsync(x => x.Code.ToUpper() == normalized, ct)).Id);
        }
        foreach (var item in node.Assets.Where(x => !desired.Contains(x.AssetId)).ToArray()) node.Assets.Remove(item);
        foreach (var id in desired)
            if (node.Assets.All(x => x.AssetId != id)) node.Assets.Add(new TechnicalNodeAssetEntity { TechnicalNodeId = node.Id, AssetId = id });
    }

    private static void SyncAliases(TechnicalNodeEntity node, IReadOnlyCollection<string> aliases, string source)
    {
        var existing = node.Aliases.Select(x => x.NormalizedAlias).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var alias in aliases)
        {
            var normalized = TechnicalHierarchyService.NormalizeName(alias);
            if (existing.Contains(normalized)) continue;
            node.Aliases.Add(new TechnicalNodeAliasEntity { TechnicalNodeId = node.Id, Alias = alias, NormalizedAlias = normalized, Source = source });
            existing.Add(normalized);
        }
    }

    private static TechnicalHierarchyExcelImportResult Result(IEnumerable<string> files, int read, int inserted, int updated, int skipped, IReadOnlyCollection<string> warnings, IReadOnlyCollection<string> errors, IReadOnlyCollection<string> missing) =>
        new(files.Select(Path.GetFileName).Where(x => x is not null).Select(x => x!).ToArray(), read, inserted, updated, skipped, warnings, errors, missing.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());

    private static string? Get(IReadOnlyDictionary<string, string?> row, string key) => row.TryGetValue(key, out var value) ? (string.IsNullOrWhiteSpace(value) ? null : value.Trim()) : null;
    private static bool HasValue(IReadOnlyDictionary<string, string?> row, string key) => !string.IsNullOrWhiteSpace(Get(row, key));
    private static IReadOnlyCollection<string> Split(string? value) => string.IsNullOrWhiteSpace(value) ? [] : value.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    private static string? Code(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
    private static bool ParseBool(string? value) => !string.IsNullOrWhiteSpace(value) && (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("si", StringComparison.OrdinalIgnoreCase) || value == "1");
    private static TechnicalHierarchyLevel ParseLevel(string? value) => Enum.TryParse<TechnicalHierarchyLevel>(value, true, out var level) ? level : TechnicalHierarchyLevel.Sistema;
}