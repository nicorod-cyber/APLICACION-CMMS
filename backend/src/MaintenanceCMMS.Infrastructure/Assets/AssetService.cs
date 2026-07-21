using System.Text.Json;
using System.Text.RegularExpressions;
using MaintenanceCMMS.Application.Assets;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceCMMS.Infrastructure.Assets;

public sealed class AssetService : IAssetService
{
    private static readonly HashSet<string> Measurements = new(StringComparer.Ordinal) { "HOROMETRO", "KILOMETRAJE" };
    private static readonly HashSet<string> Sources = new(StringComparer.Ordinal) { "MANUAL", "ORDEN_TRABAJO", "IMPORTACION", "SAP", "TELEMETRIA" };
    private readonly CmmsDbContext _db;
    private readonly IAuditService _audit;
    private readonly IAuthorizationPolicyService _authorization;
    public AssetService(CmmsDbContext db, IAuditService audit, IAuthorizationPolicyService authorization) => (_db, _audit, _authorization) = (db, audit, authorization);

    public async Task<AssetCatalogResponse> GetCatalogAsync(UserAccessContext user, CancellationToken ct)
    {
        var types = await _db.AssetTypes.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.SortOrder).ThenBy(x => x.Code)
            .Select(x => new AssetCatalogItem(x.Code, x.Name, null, null)).ToArrayAsync(ct);
        var families = await _db.EquipmentFamilies.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Code)
            .Select(x => new AssetCatalogItem(x.Code, x.Name, x.AssetType.Code, null)).ToArrayAsync(ct);
        var states = await _db.AssetOperationalStates.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Code)
            .Select(x => new AssetCatalogItem(x.Code, x.Name, null, null)).ToArrayAsync(ct);
        var locations = (await _db.TechnicalLocations.AsNoTracking().Include(x => x.Faena).Where(x => !x.IsObsolete).OrderBy(x => x.Code).ToListAsync(ct))
            .Where(x => x.Faena is null || _authorization.CanViewFaena(user, x.Faena.Code))
            .Select(x => new AssetCatalogItem(x.Code, x.Name, null, x.Faena?.Code)).ToArray();
var criticalities = await _db.WorkCatalogs.AsNoTracking()
            .Where(x => x.Category == "WorkNotificationCriticality" && x.IsActive)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Code)
            .Select(x => new AssetCatalogItem(x.Code, x.Name, null, null)).ToArrayAsync(ct);
        return new AssetCatalogResponse(types, families, states, locations, criticalities);
    }

    public async Task<IReadOnlyCollection<AssetAttributeDefinitionResponse>> GetApplicableDefinitionsAsync(string typeCode, string? familyCode, UserAccessContext user, CancellationToken ct)
    {
        var type = await _db.AssetTypes.AsNoTracking().SingleOrDefaultAsync(x => x.Code == Code(typeCode) && x.IsActive, ct) ?? throw new DomainException("Tipo de activo inexistente.");
        EquipmentFamilyEntity? family = null;
        if (!string.IsNullOrWhiteSpace(familyCode))
        {
            family = await _db.EquipmentFamilies.AsNoTracking().SingleOrDefaultAsync(x => x.Code == Code(familyCode) && x.IsActive, ct) ?? throw new DomainException("Familia inexistente.");
            if (family.AssetTypeId != type.Id) throw new DomainException("La familia no pertenece al tipo indicado.");
        }
        return (await DefinitionsAsync(type.Id, family?.Id, ct)).Select(ToDefinition).ToArray();
    }
    public async Task<IReadOnlyCollection<AssetSummary>> ListAsync(AssetListQuery query, UserAccessContext user, CancellationToken ct)
    {
        if (query.FaenaCodigo is not null && !_authorization.CanViewFaena(user, query.FaenaCodigo)) throw new UnauthorizedAccessException("No tiene acceso a la faena solicitada.");
        var assets = await Query().AsNoTracking().ToListAsync(ct);
        var list = new List<AssetSummary>();
        foreach (var asset in assets.Where(a => CanView(user, a)))
        {
            var item = await SummaryAsync(asset, ct);
            if ((query.FaenaCodigo is null || Same(item.FaenaCodigo, query.FaenaCodigo)) && (query.TipoActivoCodigo is null || Same(item.TipoActivoCodigo, query.TipoActivoCodigo)) && (query.FamiliaEquipoCodigo is null || Same(item.FamiliaEquipoCodigo, query.FamiliaEquipoCodigo)) && (query.Criticidad is null || Same(item.Criticidad, query.Criticidad)) && (query.EstadoOperacionalCodigo is null || Same(item.EstadoOperacionalCodigo, query.EstadoOperacionalCodigo)) && (query.Texto is null || string.Join(' ', item.Codigo, item.Nombre, item.TipoActivoCodigo, item.FamiliaEquipoCodigo).Contains(query.Texto, StringComparison.OrdinalIgnoreCase))) list.Add(item);
        }
        return list.OrderBy(x => x.Codigo).ToArray();
    }

    public async Task<AssetDetail?> GetByIdAsync(string codigo, UserAccessContext user, CancellationToken ct)
    {
        var asset = await FindAsync(codigo, false, ct); if (asset is null) return null; View(user, asset); return await DetailAsync(asset, ct);
    }

    public async Task<AssetDetail> CreateAsync(CreateAssetRequest r, UserAccessContext u, CancellationToken ct)
    {
        Maintain(u); Require(r.Nombre, nameof(r.Nombre)); var code = await NextCodeAsync(ct);
        var refs = await ReferencesAsync(r.TipoActivoCodigo, r.FamiliaEquipoCodigo, r.FaenaCodigo, r.EstadoOperacionalCodigo, u, ct); ValidateDates(r.AnioFabricacion, r.FechaPuestaServicio, r.FechaBaja);
        var entity = new AssetEntity { Code = code, Name = r.Nombre.Trim(), AssetTypeId = refs.Type.Id, FamilyId = refs.Family?.Id, FaenaId = refs.Faena?.Id, OperationalStateId = refs.State.Id, Brand = Empty(r.Marca), Model = Empty(r.Modelo), SerialNumber = Empty(r.NumeroSerie), Ownership = Empty(r.Propiedad), Criticality = await CriticalityAsync(r.Criticidad, ct), ManufacturingYear = r.AnioFabricacion, AcquisitionDate = r.FechaAdquisicion, CommissioningDate = r.FechaPuestaServicio, DecommissioningDate = r.FechaBaja, UsageMeasurementType = Measurement(r.TipoMedicionUso), Observations = Empty(r.Observaciones) };
        _db.Assets.Add(entity); await AttributesAsync(entity, r.Atributos ?? [], refs.Type.Id, refs.Family?.Id, true, ct); await _db.SaveChangesAsync(ct); await AuditAsync(u, "asset.created", entity, null, entity, ct); return (await GetByIdAsync(code, u, ct))!;
    }
public async Task<AssetDetail?> UpdateAsync(string codigo, UpdateAssetRequest r, UserAccessContext u, CancellationToken ct)
    {
        Maintain(u); Require(r.Nombre, nameof(r.Nombre)); var asset = await FindAsync(codigo, true, ct); if (asset is null) return null; View(u, asset);
        var refs = await ReferencesAsync(r.TipoActivoCodigo, r.FamiliaEquipoCodigo, r.FaenaCodigo, r.EstadoOperacionalCodigo, u, ct); var measurement = Measurement(r.TipoMedicionUso);
        if (!Same(asset.UsageMeasurementType, measurement) && await _db.AssetReadings.AnyAsync(x => x.AssetId == asset.Id, ct)) throw new DomainException("No se puede cambiar el tipo de medicion cuando existen lecturas.");
        ValidateDates(r.AnioFabricacion, r.FechaPuestaServicio, r.FechaBaja); var old = new { asset.AssetTypeId, asset.FamilyId, asset.FaenaId, asset.OperationalStateId };
        asset.Name = r.Nombre.Trim(); asset.AssetTypeId = refs.Type.Id; asset.FamilyId = refs.Family?.Id; asset.FaenaId = refs.Faena?.Id; asset.OperationalStateId = refs.State.Id; asset.Brand = Empty(r.Marca); asset.Model = Empty(r.Modelo); asset.SerialNumber = Empty(r.NumeroSerie); asset.Ownership = Empty(r.Propiedad); asset.Criticality = await CriticalityAsync(r.Criticidad, ct); asset.ManufacturingYear = r.AnioFabricacion; asset.AcquisitionDate = r.FechaAdquisicion; asset.CommissioningDate = r.FechaPuestaServicio; asset.DecommissioningDate = r.FechaBaja; asset.UsageMeasurementType = measurement; asset.Observations = Empty(r.Observaciones); asset.UpdatedAtUtc = DateTimeOffset.UtcNow;
        if (r.Atributos is not null) await AttributesAsync(asset, r.Atributos, refs.Type.Id, refs.Family?.Id, true, ct); await _db.SaveChangesAsync(ct); await AuditAsync(u, "asset.updated", asset, old, asset, ct); return await GetByIdAsync(asset.Code, u, ct);
    }

    public async Task<AssetStateEventResponse?> AddStateEventAsync(string codigo, CreateAssetStateEventRequest r, UserAccessContext u, CancellationToken ct)
    {
        Maintain(u); Require(r.EstadoOperacionalCodigo, nameof(r.EstadoOperacionalCodigo)); Require(r.Motivo, nameof(r.Motivo)); var asset = await FindAsync(codigo, true, ct); if (asset is null) return null; View(u, asset);
        var state = await _db.AssetOperationalStates.SingleOrDefaultAsync(x => x.Code == Code(r.EstadoOperacionalCodigo) && x.IsActive, ct) ?? throw new DomainException("Estado operacional inexistente."); var previous = asset.OperationalStateId; asset.OperationalStateId = state.Id; var evt = new AssetStateEventEntity { AssetId = asset.Id, PreviousStateId = previous, NewStateId = state.Id, OccurredAtUtc = r.FechaEventoUtc ?? DateTimeOffset.UtcNow, UserId = u.UserId, Reason = r.Motivo.Trim() }; _db.AssetStateEvents.Add(evt); await _db.SaveChangesAsync(ct); return new(evt.Id.ToString("D"), asset.Code, null, state.Code, evt.OccurredAtUtc, evt.Reason, u.UserId);
    }

    public async Task<IReadOnlyCollection<AssetReadingResponse>> GetReadingsAsync(string codigo, UserAccessContext u, CancellationToken ct)
    {
        var asset = await FindAsync(codigo, false, ct); if (asset is null) return []; View(u, asset); return MapReadings(await ValidReadingsAsync(asset.Id, ct), asset.UsageMeasurementType).OrderByDescending(x => x.FechaLecturaUtc).ToArray();
    }

    public async Task<AssetReadingResponse> AddReadingAsync(string codigo, CreateAssetReadingRequest r, UserAccessContext u, CancellationToken ct)
    {
        RegisterReadings(u); var asset = await FindAsync(codigo, true, ct) ?? throw new DomainException("Activo inexistente."); View(u, asset); if (asset.UsageMeasurementType is null) throw new DomainException("El activo no tiene medicion de uso."); if (r.Valor < 0) throw new DomainException("La lectura no puede ser negativa.");
        var valid = await ValidReadingsAsync(asset.Id, ct); var last = valid.OrderByDescending(x => x.ReadAtUtc).ThenByDescending(x => x.CreatedAtUtc).FirstOrDefault(); if (last is not null && r.Valor < last.Value) throw new DomainException("Una lectura normal no puede disminuir.");
        var reading = new AssetReadingEntity { AssetId = asset.Id, ReadAtUtc = r.FechaLecturaUtc ?? DateTimeOffset.UtcNow, Value = r.Valor, Source = Source(r.Origen), RegisteredByUserId = u.UserId, EvidenceReference = Empty(r.EvidenciaReferencia), Observations = Empty(r.Observaciones) }; _db.AssetReadings.Add(reading); await _db.SaveChangesAsync(ct); return MapReadings([.. valid, reading], asset.UsageMeasurementType).Single(x => x.Id == reading.Id.ToString("D"));
    }

    public async Task<AssetReadingResponse> CorrectReadingAsync(string codigo, string readingId, CorrectAssetReadingRequest r, UserAccessContext u, CancellationToken ct)
    {
        CorrectReadings(u); Require(r.MotivoCorreccion, nameof(r.MotivoCorreccion)); if (!Guid.TryParse(readingId, out var id)) throw new DomainException("Lectura invalida.");
        var asset = await FindAsync(codigo, true, ct) ?? throw new DomainException("Activo inexistente."); View(u, asset); if (asset.UsageMeasurementType is null || r.Valor < 0) throw new DomainException("Correccion invalida."); var original = await _db.AssetReadings.SingleOrDefaultAsync(x => x.Id == id && x.AssetId == asset.Id, ct) ?? throw new DomainException("Lectura inexistente."); if (await _db.AssetReadings.AnyAsync(x => x.CorrectedReadingId == original.Id, ct)) throw new DomainException("La lectura ya fue corregida.");
        var correction = new AssetReadingEntity { AssetId = asset.Id, ReadAtUtc = r.FechaLecturaUtc ?? DateTimeOffset.UtcNow, Value = r.Valor, Source = Source(r.Origen), RegisteredByUserId = u.UserId, EvidenceReference = Empty(r.EvidenciaReferencia), Observations = Empty(r.Observaciones), IsCorrection = true, CorrectedReadingId = original.Id, CorrectionReason = r.MotivoCorreccion.Trim(), AuthorizedByUserId = u.UserId }; _db.AssetReadings.Add(correction); await _db.SaveChangesAsync(ct); return MapReadings(await ValidReadingsAsync(asset.Id, ct), asset.UsageMeasurementType).Single(x => x.Id == correction.Id.ToString("D"));
    }

    public async Task<IReadOnlyCollection<AssetHistoryEntry>> GetHistoryAsync(string codigo, UserAccessContext u, CancellationToken ct)
    {
        var asset = await FindAsync(codigo, false, ct); if (asset is null) return []; View(u, asset); return (await _db.AssetStateEvents.AsNoTracking().Include(x => x.PreviousState).Include(x => x.NewState).Where(x => x.AssetId == asset.Id).OrderByDescending(x => x.OccurredAtUtc).ToListAsync(ct)).Select(x => new AssetHistoryEntry(x.Id.ToString("D"), x.OccurredAtUtc, "STATE_CHANGED", "EVENTOS_ESTADO", x.UserId, x.PreviousState?.Code, x.NewState.Code, x.Reason)).ToArray();
    }

    public async Task<IReadOnlyCollection<AssetDocumentResponse>> GetDocumentsAsync(string codigo, UserAccessContext u, CancellationToken ct) => (await GetDocumentMatrixAsync(codigo, u, ct)).Select(x => new AssetDocumentResponse("Activo", codigo, x.TipoDocumento, x.Estado, x.FechaVencimiento, x.DocumentoVigente, x.Critico, x.Estado == "VENCIDO", x.BloqueaDisponibilidad)).ToArray();

    public async Task<IReadOnlyCollection<AssetDocumentMatrixRow>> GetDocumentMatrixAsync(string codigo, UserAccessContext u, CancellationToken ct)
    {
        var asset = await FindAsync(codigo, false, ct); if (asset is null) return []; View(u, asset); return await MatrixAsync(asset, ct);
    }

    public async Task<AssetCostSummary?> GetCostsAsync(string codigo, UserAccessContext u, CancellationToken ct)
    {
        var asset = await FindAsync(codigo, false, ct); if (asset is null) return null; View(u, asset); var items = await _db.CostEntries.AsNoTracking().Where(x => x.AssetId == asset.Id).OrderByDescending(x => x.OccurredAtUtc).Select(x => new AssetCostLine("Costos", x.Category, x.Amount, x.Currency, x.CostNumber)).ToArrayAsync(ct); return new AssetCostSummary(asset.Code, items.Sum(x => x.Amount), items.FirstOrDefault()?.Currency ?? "CLP", items);
    }

    public async Task<AssetAvailabilityResponse?> GetAvailabilityAsync(string codigo, UserAccessContext u, CancellationToken ct)
    {
        var asset = await FindAsync(codigo, false, ct); if (asset is null) return null; View(u, asset); var matrix = await MatrixAsync(asset, ct); var documents = matrix.Where(x => x.BloqueaDisponibilidad && x.Estado is not "VALIDADO" and not "POR_VENCER").Select(x => $"Documento {x.TipoDocumento}: {x.Estado}").ToArray(); var operational = asset.OperationalState.Code == "OPERATIVO_FAENA"; var blocks = (operational ? [] : new[] { $"Estado operacional: {asset.OperationalState.Code}" }).Concat(documents).ToArray(); return new(asset.Code, blocks.Length == 0, operational, documents.Length == 0, asset.OperationalState.Code, DocumentState(matrix), blocks, blocks.Length == 0 ? 100 : 0);
    }

    private IQueryable<AssetEntity> Query() => _db.Assets.Include(x => x.AssetTypeDefinition).Include(x => x.Family).Include(x => x.Faena).ThenInclude(x => x.TechnicalLocation).Include(x => x.OperationalState);
    private Task<AssetEntity?> FindAsync(string code, bool tracking, CancellationToken ct) { var query = Query(); if (!tracking) query = query.AsNoTracking(); return query.SingleOrDefaultAsync(x => x.Code == Code(code), ct); }

    private async Task<(AssetTypeEntity Type, EquipmentFamilyEntity? Family, FaenaEntity? Faena, AssetOperationalStateEntity State)> ReferencesAsync(string type, string? family, string? faena, string state, UserAccessContext u, CancellationToken ct)
    {
        Require(type, "TipoActivoCodigo"); Require(state, "EstadoOperacionalCodigo"); var typeEntity = await _db.AssetTypes.SingleOrDefaultAsync(x => x.Code == Code(type) && x.IsActive, ct) ?? throw new DomainException("Tipo de activo inexistente.");
        var familyEntity = family is null or "" ? null : await _db.EquipmentFamilies.SingleOrDefaultAsync(x => x.Code == Code(family) && x.IsActive, ct) ?? throw new DomainException("Familia inexistente."); if (familyEntity is not null && familyEntity.AssetTypeId != typeEntity.Id) throw new DomainException("La familia no pertenece al tipo indicado.");
        var faenaEntity = faena is null or "" ? null : await _db.Faenas.Include(x => x.TechnicalLocation).SingleOrDefaultAsync(x => x.Code == Code(faena) && x.IsActive, ct) ?? throw new DomainException("Faena inexistente."); if (faenaEntity is not null && !_authorization.CanViewFaena(u, faenaEntity.Code)) throw new UnauthorizedAccessException("No tiene acceso a la faena indicada."); if (faenaEntity is not null && faenaEntity.TechnicalLocation is null) throw new DomainException("La faena indicada no tiene una ubicacion tecnica configurada.");
        var stateEntity = await _db.AssetOperationalStates.SingleOrDefaultAsync(x => x.Code == Code(state) && x.IsActive, ct) ?? throw new DomainException("Estado operacional inexistente."); return (typeEntity, familyEntity, faenaEntity, stateEntity);
    }
    private static AssetAttributeDefinitionResponse ToDefinition(AssetAttributeDefinitionEntity x) => new(x.Code, x.Name, x.DataType, x.Unit, x.IsRequired, x.IsIdentifier, x.IsUnique, x.IsSearchable, x.IsFilterable, x.ShowInList, x.MinimumValue, x.MaximumValue, x.ValidationPattern, x.OptionsJson, x.DisplayGroup, x.SortOrder);
    private async Task<IReadOnlyCollection<AssetAttributeDefinitionEntity>> DefinitionsAsync(Guid typeId, Guid? familyId, CancellationToken ct) => (await _db.AssetAttributeDefinitions.Where(x => x.IsActive && x.AssetTypeId == typeId && (x.EquipmentFamilyId == null || x.EquipmentFamilyId == familyId)).ToListAsync(ct)).GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase).Select(g => g.OrderByDescending(x => x.EquipmentFamilyId.HasValue).First()).OrderBy(x => x.SortOrder).ToArray();

    private async Task AttributesAsync(AssetEntity asset, IReadOnlyCollection<AssetAttributeValueInput> inputs, Guid typeId, Guid? familyId, bool replace, CancellationToken ct)
    {
        var definitions = await DefinitionsAsync(typeId, familyId, ct); var definitionByCode = definitions.ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase); var inputByCode = inputs.GroupBy(x => x.DefinicionCodigo, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.Single(), StringComparer.OrdinalIgnoreCase);
        if (inputByCode.Keys.Any(x => !definitionByCode.ContainsKey(x))) throw new DomainException("Existe un atributo que no aplica al tipo o familia."); foreach (var definition in definitions.Where(x => x.IsRequired && !inputByCode.ContainsKey(x.Code))) throw new DomainException($"Falta el atributo obligatorio {definition.Name}.");
        var existing = await _db.AssetAttributeValues.Where(x => x.AssetId == asset.Id).ToListAsync(ct);
        foreach (var definition in definitions) if (inputByCode.TryGetValue(definition.Code, out var input)) { Validate(definition, input); await ValidateUniqueAsync(asset.Id, definition, input, ct); var value = existing.SingleOrDefault(x => x.AttributeDefinitionId == definition.Id); if (value is null) { value = new AssetAttributeValueEntity { AssetId = asset.Id, AttributeDefinitionId = definition.Id }; _db.AssetAttributeValues.Add(value); } value.TextValue = Empty(input.ValorTexto); value.NumericValue = input.ValorNumerico; value.BooleanValue = input.ValorBooleano; value.DateValue = input.ValorFecha; value.Observations = Empty(input.Observaciones); }
        if (replace) foreach (var old in existing.Where(x => definitions.Any(d => d.Id == x.AttributeDefinitionId) && !inputByCode.ContainsKey(definitions.Single(d => d.Id == x.AttributeDefinitionId).Code))) _db.AssetAttributeValues.Remove(old);
    }

    private static void Validate(AssetAttributeDefinitionEntity d, AssetAttributeValueInput i)
    {
        if (new object?[] { Empty(i.ValorTexto), i.ValorNumerico, i.ValorBooleano, i.ValorFecha }.Count(x => x is not null) != 1) throw new DomainException($"El atributo {d.Name} debe tener un solo valor.");
        var expected = d.DataType switch { "TEXTO" or "OPCION" => i.ValorTexto is not null, "NUMERO" => i.ValorNumerico.HasValue, "ENTERO" => i.ValorNumerico.HasValue && decimal.Truncate(i.ValorNumerico.Value) == i.ValorNumerico.Value, "BOOLEANO" => i.ValorBooleano.HasValue, "FECHA" => i.ValorFecha.HasValue, _ => false }; if (!expected) throw new DomainException($"El valor no coincide con el tipo de {d.Name}.");
        if (i.ValorNumerico is { } number && ((d.MinimumValue.HasValue && number < d.MinimumValue) || (d.MaximumValue.HasValue && number > d.MaximumValue))) throw new DomainException($"El valor de {d.Name} esta fuera de rango."); if (i.ValorTexto is { } text && d.ValidationPattern is { Length: > 0 } && !Regex.IsMatch(text, d.ValidationPattern)) throw new DomainException($"El valor de {d.Name} no cumple el patron."); if (d.DataType == "OPCION" && !Option(d.OptionsJson, i.ValorTexto!)) throw new DomainException($"El valor de {d.Name} no es una opcion permitida.");
    }

    private async Task ValidateUniqueAsync(Guid assetId, AssetAttributeDefinitionEntity d, AssetAttributeValueInput i, CancellationToken ct)
    {
        if (!d.IsUnique) return; var values = _db.AssetAttributeValues.Where(x => x.AttributeDefinitionId == d.Id && x.AssetId != assetId); var exists = d.DataType is "TEXTO" or "OPCION" ? await values.AnyAsync(x => x.TextValue == i.ValorTexto, ct) : d.DataType is "NUMERO" or "ENTERO" ? await values.AnyAsync(x => x.NumericValue == i.ValorNumerico, ct) : d.DataType == "BOOLEANO" ? await values.AnyAsync(x => x.BooleanValue == i.ValorBooleano, ct) : await values.AnyAsync(x => x.DateValue == i.ValorFecha, ct); if (exists) throw new DomainException($"El atributo unico {d.Name} ya esta asignado.");
    }

    private async Task<AssetSummary> SummaryAsync(AssetEntity asset, CancellationToken ct)
    {
        var definitions = await DefinitionsAsync(asset.AssetTypeId, asset.FamilyId, ct); var values = await _db.AssetAttributeValues.AsNoTracking().Where(x => x.AssetId == asset.Id).ToListAsync(ct); var readings = await ValidReadingsAsync(asset.Id, ct); var latest = readings.OrderByDescending(x => x.ReadAtUtc).ThenByDescending(x => x.CreatedAtUtc).FirstOrDefault(); var matrix = await MatrixAsync(asset, ct);
        var required = definitions.Where(x => x.IsRequired).ToArray(); var ids = values.Where(x => x.TextValue is not null || x.NumericValue.HasValue || x.BooleanValue.HasValue || x.DateValue.HasValue).Select(x => x.AttributeDefinitionId).ToHashSet(); var missing = required.Where(x => !ids.Contains(x.Id)).Select(x => x.Code).ToArray(); var completed = required.Length - missing.Length; var percentage = required.Length == 0 ? 100 : (int)Math.Round(completed * 100m / required.Length); var completeness = new AssetCompleteness(required.Length, completed, percentage, percentage == 100 ? "COMPLETA" : completed == 0 ? "PENDIENTE" : "PARCIAL", missing);
        return new AssetSummary(asset.Code, asset.Name, asset.AssetTypeDefinition.Code, asset.AssetTypeDefinition.Name, asset.Family?.Code, asset.Family?.Name, asset.Faena?.Code, asset.Faena?.TechnicalLocation?.Code, asset.OperationalState.Code, asset.Criticality, asset.UsageMeasurementType, latest?.Value, Unit(asset.UsageMeasurementType), completeness, DocumentState(matrix), !matrix.Any(x => x.BloqueaDisponibilidad && x.Estado is not "VALIDADO" and not "POR_VENCER"));
    }

    private async Task<AssetDetail> DetailAsync(AssetEntity asset, CancellationToken ct)
    {
        var summary = await SummaryAsync(asset, ct); var definitions = await DefinitionsAsync(asset.AssetTypeId, asset.FamilyId, ct); var values = await _db.AssetAttributeValues.AsNoTracking().Include(x => x.AttributeDefinition).Where(x => x.AssetId == asset.Id).ToListAsync(ct); var readings = MapReadings(await ValidReadingsAsync(asset.Id, ct), asset.UsageMeasurementType); var components = await _db.OperationalUnitComponents.AsNoTracking().Include(x => x.OperationalUnit).Include(x => x.ComponentRole).Where(x => x.AssetId == asset.Id).OrderByDescending(x => x.InstalledAtUtc).ToListAsync(ct); var orders = await _db.WorkOrders.AsNoTracking().Include(x => x.Status).Include(x => x.MaintenanceType).Where(x => x.AssetId == asset.Id || x.RelatedAssets.Any(related => related.AssetId == asset.Id)).OrderByDescending(x => x.ScheduledAtUtc ?? x.CreatedAtUtc).Select(x => new AssetWorkOrderSummary(x.WorkOrderNumber, x.Status.Code, x.MaintenanceType.Code, x.Description, x.ScheduledAtUtc.HasValue ? DateOnly.FromDateTime(x.ScheduledAtUtc.Value.UtcDateTime) : null)).ToArrayAsync(ct); var documents = (await MatrixAsync(asset, ct)).Select(x => new AssetDocumentResponse("Activo", asset.Code, x.TipoDocumento, x.Estado, x.FechaVencimiento, x.DocumentoVigente, x.Critico, x.Estado == "VENCIDO", x.BloqueaDisponibilidad)).ToArray();
        return new AssetDetail(summary, asset.Brand, asset.Model, asset.SerialNumber, asset.Ownership, asset.ManufacturingYear, asset.AcquisitionDate, asset.CommissioningDate, asset.DecommissioningDate, asset.Observations, values.Select(x => new AssetAttributeValueResponse(x.AttributeDefinition.Code, x.AttributeDefinition.Name, x.AttributeDefinition.DataType, x.AttributeDefinition.Unit, x.TextValue, x.NumericValue, x.BooleanValue, x.DateValue, x.Observations)).ToArray(), definitions.Select(ToDefinition).ToArray(), readings, orders, components.FirstOrDefault(x => x.RemovedAtUtc is null)?.OperationalUnit.Code, components.Select(x => new AssetCompositionHistoryEntry(x.OperationalUnit.Code, x.ComponentRole.Code, x.InstalledAtUtc, x.RemovedAtUtc, x.Observations)).ToArray(), documents);
    }

    private async Task<IReadOnlyCollection<AssetDocumentMatrixRow>> MatrixAsync(AssetEntity asset, CancellationToken ct)
    {
        var requirements = await _db.AssetDocumentRequirements.AsNoTracking().Include(x => x.DocumentType).Where(x => x.IsActive && x.AssetTypeId == asset.AssetTypeId && (x.EquipmentFamilyId == null || x.EquipmentFamilyId == asset.FamilyId)).ToListAsync(ct); var documents = await _db.DocumentAssets.AsNoTracking().Include(x => x.Document).ThenInclude(x => x.DocumentType).Include(x => x.Document).ThenInclude(x => x.Versions).Where(x => x.AssetId == asset.Id && x.IsActive).Select(x => x.Document).ToListAsync(ct); var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return requirements.GroupBy(x => x.DocumentTypeId).Select(g => g.OrderByDescending(x => x.EquipmentFamilyId.HasValue).First()).OrderBy(x => x.DocumentType.Code).Select(r => MatrixRow(r, documents.Where(d => d.DocumentTypeId == r.DocumentTypeId).OrderByDescending(d => d.CreatedAtUtc).FirstOrDefault(), today)).ToArray();
    }

    private static AssetDocumentMatrixRow MatrixRow(AssetDocumentRequirementEntity r, DocumentEntity? d, DateOnly today)
    {
        if (d is null) return new AssetDocumentMatrixRow(r.DocumentType.Code, r.IsMandatory, r.IsCritical, r.BlocksAvailability, "PENDIENTE_CARGA", null, null, null, null, "No existe documento vigente."); var expiration = d.ExpiresOn; var days = expiration?.DayNumber - today.DayNumber; var status = d.IsAnnulled ? "ANULADO" : d.Status.ToUpperInvariant().Contains("RECHAZ") ? "RECHAZADO" : d.Status.ToUpperInvariant().Contains("VALID") ? "VALIDADO" : d.Status.ToUpperInvariant().Contains("REEMPLAZ") ? "REEMPLAZADO" : "PENDIENTE_VALIDACION"; if (status == "VALIDADO" && expiration is { } date) status = date < today ? "VENCIDO" : days <= (r.AlertDays ?? 0) ? "POR_VENCER" : "VALIDADO"; return new AssetDocumentMatrixRow(r.DocumentType.Code, r.IsMandatory, r.IsCritical, r.BlocksAvailability, status, d.Code, d.Versions.OrderByDescending(x => x.VersionNumber).FirstOrDefault()?.VersionNumber, expiration, days, status is "VALIDADO" or "POR_VENCER" ? null : $"Documento {status}.");
    }

    private async Task<List<AssetReadingEntity>> ValidReadingsAsync(Guid assetId, CancellationToken ct) { var all = await _db.AssetReadings.Where(x => x.AssetId == assetId).ToListAsync(ct); var replaced = all.Where(x => x.CorrectedReadingId.HasValue).Select(x => x.CorrectedReadingId!.Value).ToHashSet(); return all.Where(x => !replaced.Contains(x.Id)).ToList(); }
    private static IReadOnlyCollection<AssetReadingResponse> MapReadings(IReadOnlyCollection<AssetReadingEntity> readings, string? measurement) { AssetReadingEntity? previous = null; return readings.OrderBy(x => x.ReadAtUtc).ThenBy(x => x.CreatedAtUtc).Select(x => { var result = new AssetReadingResponse(x.Id.ToString("D"), x.ReadAtUtc, x.Value, Unit(measurement) ?? string.Empty, previous is null ? null : x.Value - previous.Value, x.Source, x.IsCorrection, x.CorrectedReadingId?.ToString("D"), x.IsAnomalous, x.ValidationMessage, x.Observations); previous = x; return result; }).ToArray(); }
    private static string DocumentState(IReadOnlyCollection<AssetDocumentMatrixRow> rows) => rows.Any(x => x.Obligatorio && x.Estado == "PENDIENTE_CARGA") ? "PENDIENTE_CARGA" : rows.Any(x => x.Obligatorio && x.Estado == "PENDIENTE_VALIDACION") ? "PENDIENTE_VALIDACION" : rows.Any(x => x.Obligatorio && x.Estado == "VENCIDO") ? "VENCIDO" : rows.Any(x => x.Obligatorio && x.Estado == "POR_VENCER") ? "POR_VENCER" : "VALIDADO";
    private static string? Unit(string? type) => type == "HOROMETRO" ? "horas" : type == "KILOMETRAJE" ? "kilometros" : null;
    private async Task<string> NextCodeAsync(CancellationToken ct)
    {
        var number = await _db.Database.SqlQueryRaw<long>("SELECT nextval('asset_number_seq') AS \"Value\"").SingleAsync(ct);
        return $"ACT-{number:D6}";
    }
    private async Task<string?> CriticalityAsync(string? value, CancellationToken ct)
    {
        var requested = Empty(value);
        if (requested is null) return null;
        var items = await _db.WorkCatalogs.AsNoTracking().Where(x => x.Category == "WorkNotificationCriticality" && x.IsActive).ToListAsync(ct);
        var match = items.SingleOrDefault(x => Same(x.Name, requested) || Same(x.Code, requested));
        return match?.Name ?? throw new DomainException($"La criticidad '{requested}' no existe en el catalogo WorkNotificationCriticality.");
    }
    private static bool Option(string? json, string value) { try { using var d = JsonDocument.Parse(json ?? "[]"); return d.RootElement.ValueKind == JsonValueKind.Array && d.RootElement.EnumerateArray().Any(x => string.Equals(x.ValueKind == JsonValueKind.String ? x.GetString() : x.ToString(), value, StringComparison.OrdinalIgnoreCase)); } catch { return false; } }
    private static string? Measurement(string? value) { if (string.IsNullOrWhiteSpace(value)) return null; var type = Code(value); if (!Measurements.Contains(type)) throw new DomainException("TipoMedicionUso solo permite HOROMETRO, KILOMETRAJE o null."); return type; }
    private static string Source(string value) { var source = Code(value); if (!Sources.Contains(source)) throw new DomainException("Origen de lectura invalido."); return source; }
    private static void ValidateDates(short? year, DateOnly? start, DateOnly? end) { if (year is { } y && (y < 1900 || y > DateTime.UtcNow.Year + 2)) throw new DomainException("Año de fabricacion invalido."); if (start is { } a && end is { } b && b < a) throw new DomainException("La fecha de baja no puede ser anterior a la puesta en servicio."); }
    private void RegisterReadings(UserAccessContext u)
    {
        if (u.Permissions.Contains(AuthPermissions.RegisterAssetReadings, StringComparer.OrdinalIgnoreCase) || _authorization.CanAdminister(u)) return;
        throw new UnauthorizedAccessException("No tiene permisos para registrar lecturas de activos.");
    }

    private void CorrectReadings(UserAccessContext u)
    {
        if (u.Permissions.Contains(AuthPermissions.CorrectAssetReadings, StringComparer.OrdinalIgnoreCase) || _authorization.CanAdminister(u)) return;
        throw new UnauthorizedAccessException("No tiene permisos para corregir lecturas de activos.");
    }
    private void Maintain(UserAccessContext u) { if (!_authorization.CanAdminister(u) && !u.Roles.Contains(AuthRoles.Planner, StringComparer.OrdinalIgnoreCase) && !u.Roles.Contains(AuthRoles.MaintenanceSupervisor, StringComparer.OrdinalIgnoreCase)) throw new UnauthorizedAccessException("No tiene permisos para administrar activos."); }
    private static bool CanView(UserAccessContext u, AssetEntity a) => a.Faena is null || u.Roles.Contains(AuthRoles.Admin, StringComparer.OrdinalIgnoreCase) || u.Roles.Contains(AuthRoles.Management, StringComparer.OrdinalIgnoreCase) || u.Faenas.Contains(a.Faena.Code, StringComparer.OrdinalIgnoreCase);
    private static void View(UserAccessContext u, AssetEntity a) { if (!CanView(u, a)) throw new UnauthorizedAccessException("No tiene acceso al activo."); }
    private async Task AuditAsync(UserAccessContext u, string action, AssetEntity a, object? previous, object? next, CancellationToken ct) =>
        await _audit.RecordAsync(new AuditEventRequest(
            u.UserId,
            action,
            AuditModules.Assets,
            "Asset",
            a.Code,
            previous is null ? null : JsonSerializer.Serialize(AuditValue(previous)),
            next is null ? null : JsonSerializer.Serialize(AuditValue(next)),
            a.Faena?.Code,
            AuditSeverity.Medium), ct);

    private static object AuditValue(object value) => value is AssetEntity asset
        ? new
        {
            asset.Id,
            asset.Code,
            asset.Name,
            asset.AssetTypeId,
            asset.FaenaId,
            asset.FamilyId,
            asset.OperationalStateId,
            asset.Brand,
            asset.Model,
            asset.SerialNumber,
            asset.Ownership,
            asset.Criticality,
            asset.ManufacturingYear,
            asset.AcquisitionDate,
            asset.CommissioningDate,
            asset.DecommissioningDate,
            asset.UsageMeasurementType,
            asset.Observations,
            asset.CreatedAtUtc,
            asset.UpdatedAtUtc
        }
        : value;
    private static bool Same(string? a, string? b) => string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);
    private static string Code(string? v) => v?.Trim().ToUpperInvariant() ?? string.Empty;
    private static string? Empty(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    private static void Require(string? v, string name) { if (string.IsNullOrWhiteSpace(v)) throw new DomainException($"{name} es obligatorio."); }
}
