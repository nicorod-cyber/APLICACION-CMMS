using System.Data;
using MaintenanceCMMS.Application.Abstractions.Pagination;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Assets;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.OperationalUnits;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Infrastructure.Assets;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceCMMS.Infrastructure.OperationalUnits;

public sealed class OperationalUnitService : IOperationalUnitService
{
    private readonly CmmsDbContext db;
    private readonly IAuditService audit;
    private readonly IAssetService? assetService;

    public OperationalUnitService(CmmsDbContext db, IAuditService audit) : this(db, audit, null) { }

    public OperationalUnitService(CmmsDbContext db, IAuditService audit, IAssetService? assetService)
    {
        this.db = db;
        this.audit = audit;
        this.assetService = assetService;
    }
    private static readonly HashSet<string> CriticalRoleCodes = new(StringComparer.OrdinalIgnoreCase) { "FABRICA", "CHASIS" };

    public async Task<PagedResponse<OperationalUnitSummary>> ListPageAsync(OperationalUnitListQuery query, UserAccessContext user, CancellationToken ct)
    {
        View(user);
        if (!string.IsNullOrWhiteSpace(query.FaenaCodigo)) EnsureView(user, query.FaenaCodigo);
        var page = Math.Max(1, query.Page);
        var pageSize = query.PageSize is 25 or 50 or 100 ? query.PageSize : 25;
        var canViewAll = user.Roles.Contains(AuthRoles.Admin, StringComparer.OrdinalIgnoreCase) || user.Roles.Contains(AuthRoles.Management, StringComparer.OrdinalIgnoreCase);
        var authorizedFaenas = user.Faenas.Select(Code).Distinct().ToArray();
        IQueryable<OperationalUnitEntity> source = db.OperationalUnits.AsNoTracking();
        if (!canViewAll) source = source.Where(x => x.FaenaId == null || (x.Faena != null && authorizedFaenas.Contains(x.Faena.Code)));
        if (!string.IsNullOrWhiteSpace(query.FaenaCodigo)) source = source.Where(x => x.Faena != null && x.Faena.Code == Code(query.FaenaCodigo));
        if (!string.IsNullOrWhiteSpace(query.Texto))
        {
            var term = query.Texto.Trim();
            source = source.Where(x => EF.Functions.ILike(x.Code, "%" + term + "%") || EF.Functions.ILike(x.Name, "%" + term + "%"));
        }
        var total = await source.CountAsync(ct);
        var units = await source.OrderBy(x => x.Code).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Id, x.Code, x.Name, x.OperationalUnitTypeId, TypeCode = x.OperationalUnitType.Code, FaenaCode = x.Faena == null ? null : x.Faena.Code, TechnicalLocationCode = x.Faena == null || x.Faena.TechnicalLocation == null ? null : x.Faena.TechnicalLocation.Code, OperationalStateCode = x.OperationalState.Code, x.Criticality })
            .ToListAsync(ct);
        var unitIds = units.Select(x => x.Id).ToArray();
        var typeIds = units.Select(x => x.OperationalUnitTypeId).Distinct().ToArray();
        var componentCounts = await db.OperationalUnitComponents.AsNoTracking().Where(x => unitIds.Contains(x.OperationalUnitId) && x.RemovedAtUtc == null)
            .GroupBy(x => new { x.OperationalUnitId, x.ComponentRoleId }).Select(x => new { x.Key.OperationalUnitId, x.Key.ComponentRoleId, Count = x.Count() }).ToListAsync(ct);
        var rules = await db.OperationalUnitCompositionRules.AsNoTracking().Where(x => typeIds.Contains(x.OperationalUnitTypeId) && x.IsActive && x.IsMandatory)
            .Select(x => new { x.OperationalUnitTypeId, x.ComponentRoleId, x.MinimumQuantity, RoleCode = x.ComponentRole.Code }).ToListAsync(ct);
        var countByUnitAndRole = componentCounts.ToDictionary(x => (x.OperationalUnitId, x.ComponentRoleId), x => x.Count);
        var items = units.Select(unit =>
        {
            var missing = rules.Where(rule => rule.OperationalUnitTypeId == unit.OperationalUnitTypeId && (!countByUnitAndRole.TryGetValue((unit.Id, rule.ComponentRoleId), out var count) || count < rule.MinimumQuantity)).Select(rule => rule.RoleCode).OrderBy(code => code).ToArray();
            return new OperationalUnitSummary(unit.Code, unit.Name, unit.TypeCode, unit.FaenaCode, unit.TechnicalLocationCode, unit.OperationalStateCode, unit.Criticality, missing.Length == 0, missing);
        }).ToArray();
        var totalPages = (int)Math.Ceiling(total / (double)pageSize);
        return new PagedResponse<OperationalUnitSummary>(items, page, pageSize, total, totalPages, page < totalPages, page > 1);
    }

    public async Task<IReadOnlyCollection<OperationalUnitResponse>> ListAsync(string? faenaCodigo, UserAccessContext user, CancellationToken ct)
    {
        View(user);
        var units = await Units().Where(x => string.IsNullOrWhiteSpace(faenaCodigo) || x.Faena!.Code == Code(faenaCodigo)).ToListAsync(ct);
        var result = new List<OperationalUnitResponse>();
        foreach (var unit in units.Where(x => CanView(user, x))) result.Add(await MapAsync(unit, ct));
        return result.OrderBy(x => x.Codigo).ToArray();
    }
    public async Task<OperationalUnitResponse?> GetAsync(string codigo, UserAccessContext user, CancellationToken ct)
    {
        View(user);
        var unit = await FindUnitAsync(codigo, ct);
        if (unit is null) return null;
        EnsureView(user, unit);
        return await MapAsync(unit, ct);
    }

    public async Task<IReadOnlyCollection<OperationalUnitRuleResponse>> GetRulesAsync(string codigo, UserAccessContext user, CancellationToken ct)
    {
        View(user);
        var unit = await FindUnitAsync(codigo, ct);
        if (unit is null) return [];
        EnsureView(user, unit);
        return await db.OperationalUnitCompositionRules.AsNoTracking()
            .Where(x => x.OperationalUnitTypeId == unit.OperationalUnitTypeId && x.IsActive)
            .OrderBy(x => x.ComponentRole.Code)
            .Select(x => new OperationalUnitRuleResponse(
                unit.OperationalUnitType.Code,
                x.ComponentRole.Code,
                x.MinimumQuantity,
                x.MaximumQuantity,
                x.IsMandatory,
                x.AllowedAssets.Select(a => new AllowedComponentRequest(
                    a.AssetType == null ? null : a.AssetType.Code,
                    a.EquipmentFamily == null ? null : a.EquipmentFamily.Code)).ToArray()))
            .ToArrayAsync(ct);
    }
    public async Task<OperationalUnitTypeRequest> CreateTypeAsync(OperationalUnitTypeRequest r, UserAccessContext u, CancellationToken ct)
    {
        ManageUnits(u); Require(r.Codigo, "Codigo"); Require(r.Nombre, "Nombre"); var code = Code(r.Codigo);
        if (await db.OperationalUnitTypes.AnyAsync(x => x.Code == code, ct)) throw new DomainException("El tipo de unidad ya existe.");
        db.OperationalUnitTypes.Add(new OperationalUnitTypeEntity { Code = code, Name = r.Nombre.Trim(), Description = Text(r.Descripcion), ParticipatesInAvailability = r.ParticipaEnDisponibilidad, IsActive = true });
        await db.SaveChangesAsync(ct);
        return r with { Codigo = code, Nombre = r.Nombre.Trim(), Descripcion = Text(r.Descripcion) };
    }

    public async Task<OperationalUnitRoleRequest> CreateRoleAsync(OperationalUnitRoleRequest r, UserAccessContext u, CancellationToken ct)
    {
        ManageUnits(u); Require(r.Codigo, "Codigo"); Require(r.Nombre, "Nombre"); var code = Code(r.Codigo);
        if (await db.OperationalUnitComponentRoles.AnyAsync(x => x.Code == code, ct)) throw new DomainException("El rol de componente ya existe.");
        var critical = r.Critico || CriticalRoleCodes.Contains(code);
        db.OperationalUnitComponentRoles.Add(new OperationalUnitComponentRoleEntity { Code = code, Name = r.Nombre.Trim(), Description = Text(r.Descripcion), IsCritical = critical, IsActive = true });
        await db.SaveChangesAsync(ct);
        return r with { Codigo = code, Nombre = r.Nombre.Trim(), Descripcion = Text(r.Descripcion), Critico = critical };
    }

    public async Task<OperationalUnitRuleResponse> UpsertRuleAsync(OperationalUnitRuleRequest r, UserAccessContext u, CancellationToken ct)
    {
        ManageComposition(u);
        if (r.CantidadMinima < 0 || r.CantidadMaxima < r.CantidadMinima) throw new DomainException("Las cantidades de la regla son invalidas.");
        if (r.Obligatorio && r.CantidadMinima == 0) throw new DomainException("Una regla obligatoria debe exigir al menos un componente.");
        var type = await db.OperationalUnitTypes.SingleOrDefaultAsync(x => x.Code == Code(r.TipoUnidadCodigo) && x.IsActive, ct) ?? throw new DomainException("Tipo de unidad inexistente.");
        var role = await db.OperationalUnitComponentRoles.SingleOrDefaultAsync(x => x.Code == Code(r.RolComponenteCodigo) && x.IsActive, ct) ?? throw new DomainException("Rol inexistente.");
        if (role.IsCritical && r.CantidadMaxima > 1) throw new DomainException("Un rol critico solo admite un componente vigente por unidad.");
        var rule = await db.OperationalUnitCompositionRules.Include(x => x.AllowedAssets).SingleOrDefaultAsync(x => x.OperationalUnitTypeId == type.Id && x.ComponentRoleId == role.Id, ct);
        if (rule is null) { rule = new OperationalUnitCompositionRuleEntity { OperationalUnitTypeId = type.Id, ComponentRoleId = role.Id }; db.OperationalUnitCompositionRules.Add(rule); }
        rule.MinimumQuantity = r.CantidadMinima; rule.MaximumQuantity = r.CantidadMaxima; rule.IsMandatory = r.Obligatorio; rule.IsActive = true;
        db.OperationalUnitCompositionRuleAllowedAssets.RemoveRange(rule.AllowedAssets);
        var allowed = new List<AllowedComponentRequest>();
        foreach (var item in r.Permitidos ?? [])
        {
            if (string.IsNullOrWhiteSpace(item.TipoActivoCodigo) && string.IsNullOrWhiteSpace(item.FamiliaEquipoCodigo)) throw new DomainException("Cada permitido debe indicar tipo o familia.");
            var at = string.IsNullOrWhiteSpace(item.TipoActivoCodigo) ? null : await db.AssetTypes.SingleOrDefaultAsync(x => x.Code == Code(item.TipoActivoCodigo) && x.IsActive, ct) ?? throw new DomainException("Tipo de activo permitido inexistente.");
            var family = string.IsNullOrWhiteSpace(item.FamiliaEquipoCodigo) ? null : await db.EquipmentFamilies.SingleOrDefaultAsync(x => x.Code == Code(item.FamiliaEquipoCodigo) && x.IsActive, ct) ?? throw new DomainException("Familia permitida inexistente.");
            if (at is not null && family is not null && family.AssetTypeId != at.Id) throw new DomainException("La familia permitida no corresponde al tipo indicado.");
            if (allowed.Any(x => Same(x.TipoActivoCodigo, at?.Code) && Same(x.FamiliaEquipoCodigo, family?.Code))) continue;
            db.OperationalUnitCompositionRuleAllowedAssets.Add(new OperationalUnitCompositionRuleAllowedAssetEntity { OperationalUnitCompositionRule = rule, AssetTypeId = at?.Id, EquipmentFamilyId = family?.Id });
            allowed.Add(new AllowedComponentRequest(at?.Code, family?.Code));
        }
        await db.SaveChangesAsync(ct);
        return new(type.Code, role.Code, rule.MinimumQuantity, rule.MaximumQuantity, rule.IsMandatory, allowed);
    }

    public async Task<OperationalUnitResponse> CreateAsync(OperationalUnitRequest r, UserAccessContext u, CancellationToken ct)
    {
        ManageUnits(u); Require(r.Codigo, "Codigo"); Require(r.Nombre, "Nombre"); var code = Code(r.Codigo);
        if (await db.OperationalUnits.AnyAsync(x => x.Code == code, ct)) throw new DomainException("La unidad operativa ya existe.");
        var type = await db.OperationalUnitTypes.SingleOrDefaultAsync(x => x.Code == Code(r.TipoUnidadCodigo) && x.IsActive, ct) ?? throw new DomainException("Tipo de unidad inexistente.");
        var state = await db.AssetOperationalStates.SingleOrDefaultAsync(x => x.Code == Code(r.EstadoOperacionalCodigo) && x.IsActive, ct) ?? throw new DomainException("Estado operacional inexistente.");
        var faena = string.IsNullOrWhiteSpace(r.FaenaCodigo) ? null : await db.Faenas.Include(x => x.TechnicalLocation).SingleOrDefaultAsync(x => x.Code == Code(r.FaenaCodigo) && x.IsActive, ct) ?? throw new DomainException("Faena inexistente.");
        if (faena is not null) EnsureView(u, faena.Code);
        if (faena is not null && faena.TechnicalLocation is null) throw new DomainException("La faena indicada no tiene una ubicacion tecnica configurada.");
        if (faena is not null) AssetOperationalPolicy.EnsureCompatibleWithPhysicalLocation("unidad operativa", "FAENA", state.Code, state.Name);
        if (r.FechaBaja is { } end && r.FechaPuestaServicio is { } start && end < start) throw new DomainException("La baja no puede preceder a la puesta en servicio.");
        var unit = new OperationalUnitEntity { Code = code, Name = r.Nombre.Trim(), OperationalUnitTypeId = type.Id, FaenaId = faena?.Id, OperationalStateId = state.Id, BaselineOperationalStateId = state.Id, Criticality = Text(r.Criticidad), CommissioningDate = r.FechaPuestaServicio, DecommissioningDate = r.FechaBaja, Observations = Text(r.Observaciones) };
        db.OperationalUnits.Add(unit); await db.SaveChangesAsync(ct);
        await Audit(u, "operational_unit.created", unit.Code, faena?.Code, ct);
        return (await GetAsync(unit.Code, u, ct))!;
    }

    public async Task<OperationalUnitResponse?> UpdateAsync(string codigo, UpdateOperationalUnitRequest r, UserAccessContext u, CancellationToken ct)
    {
        ManageUnits(u);
        Require(r.Nombre, nameof(r.Nombre));
        var unit = await FindUnitAsync(codigo, ct);
        if (unit is null) return null;
        EnsureView(u, unit);
        unit.Name = r.Nombre.Trim();
        unit.Criticality = Text(r.Criticidad);
        unit.Observations = Text(r.Observaciones);
        unit.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await Audit(u, "operational_unit.updated", unit.Code, unit.Faena?.Code, ct);
        return await GetAsync(unit.Code, u, ct);
    }
    public async Task<OperationalUnitCompositionResponse?> MountAsync(string unidadCodigo, MountOperationalUnitComponentRequest r, UserAccessContext u, CancellationToken ct)
    {
        ManageComposition(u);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await LockCompositionAsync(ct);
        var unit = await FindUnitAsync(unidadCodigo, ct); if (unit is null) return null; EnsureView(u, unit);
        var mountedAt = r.FechaMontajeUtc ?? DateTimeOffset.UtcNow;
        await MountCoreAsync(unit, r.ActivoCodigo, r.RolComponenteCodigo, r.OrdenTrabajoNumero, mountedAt, r.Observaciones, r.Motivo, u, ct);
        await SaveCompositionAsync(ct);
        await SynchronizeCompositionReferenceAsync(unit, r.ActivoCodigo, r.RolComponenteCodigo, mountedAt, u, ct);
        await OperationalUnitStateCalculator.RecalculateAsync(db, unit, r.Motivo, ct);
        await SaveCompositionAsync(ct); await tx.CommitAsync(ct);
        await Audit(u, "operational_unit.component_mounted", unit.Code, unit.Faena?.Code, ct);
        return await CompositionAsync(unit, ct);
    }

    public async Task<OperationalUnitCompositionResponse?> UnmountAsync(string unidadCodigo, string activoCodigo, UnmountOperationalUnitComponentRequest r, UserAccessContext u, CancellationToken ct)
    {
        ManageComposition(u);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await LockCompositionAsync(ct);
        var unit = await FindUnitAsync(unidadCodigo, ct); if (unit is null) return null; EnsureView(u, unit);
        var component = await db.OperationalUnitComponents.Include(x => x.Asset).Include(x => x.ComponentRole).SingleOrDefaultAsync(x => x.OperationalUnitId == unit.Id && x.Asset.Code == Code(activoCodigo) && x.RemovedAtUtc == null, ct) ?? throw new DomainException("El activo no esta montado en la unidad.");
        RequireCriticalReason(component.ComponentRole, r.Motivo);
        var date = r.FechaDesmontajeUtc ?? DateTimeOffset.UtcNow;
        if (date < component.InstalledAtUtc) throw new DomainException("La fecha de desmontaje no puede preceder el montaje.");
        component.RemovedAtUtc = date; component.RemovalWorkOrderId = await WorkOrderIdAsync(r.OrdenTrabajoNumero, ct); component.RemovedByUserId = u.UserId; component.RemovalReason = Text(r.Motivo); component.Observations = Text(r.Observaciones) ?? component.Observations;
        await SaveCompositionAsync(ct);
        await OperationalUnitStateCalculator.RecalculateAsync(db, unit, r.Motivo, ct);
        await SaveCompositionAsync(ct); await tx.CommitAsync(ct);
        await Audit(u, "operational_unit.component_unmounted", unit.Code, unit.Faena?.Code, ct);
        return await CompositionAsync(unit, ct);
    }

    public async Task<OperationalUnitCompositionResponse?> ReplaceAsync(string unidadCodigo, ReplaceOperationalUnitComponentRequest r, UserAccessContext u, CancellationToken ct)
    {
        ManageComposition(u);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await LockCompositionAsync(ct);
        var unit = await FindUnitAsync(unidadCodigo, ct); if (unit is null) return null; EnsureView(u, unit);
        var outgoing = await db.OperationalUnitComponents.Include(x => x.Asset).Include(x => x.ComponentRole).SingleOrDefaultAsync(x => x.OperationalUnitId == unit.Id && x.Asset.Code == Code(r.ActivoSalienteCodigo) && x.ComponentRole.Code == Code(r.RolComponenteCodigo) && x.RemovedAtUtc == null, ct) ?? throw new DomainException("El componente saliente no esta montado en el rol indicado.");
        RequireCriticalReason(outgoing.ComponentRole, r.Motivo);
        var date = r.FechaOperacionUtc ?? DateTimeOffset.UtcNow;
        if (date < outgoing.InstalledAtUtc) throw new DomainException("La fecha de reemplazo no puede preceder el montaje original.");
        outgoing.RemovedAtUtc = date; outgoing.RemovalWorkOrderId = await WorkOrderIdAsync(r.OrdenTrabajoNumero, ct); outgoing.RemovedByUserId = u.UserId; outgoing.RemovalReason = Text(r.Motivo);
        await db.SaveChangesAsync(ct);
        await MountCoreAsync(unit, r.ActivoEntranteCodigo, r.RolComponenteCodigo, r.OrdenTrabajoNumero, date, r.Observaciones, r.Motivo, u, ct);
        await SaveCompositionAsync(ct);
        await SynchronizeCompositionReferenceAsync(unit, r.ActivoEntranteCodigo, r.RolComponenteCodigo, date, u, ct);
        await OperationalUnitStateCalculator.RecalculateAsync(db, unit, r.Motivo, ct);
        await SaveCompositionAsync(ct); await tx.CommitAsync(ct);
        await Audit(u, "operational_unit.component_replaced", unit.Code, unit.Faena?.Code, ct);
        return await CompositionAsync(unit, ct);
    }

    public async Task<OperationalUnitReadingResponse> AddReadingAsync(string codigo, CreateAssetReadingRequest request, UserAccessContext user, CancellationToken ct)
    {
        RegisterReadings(user);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var (unit, chassis, factory) = await RequiredComponentsAsync(codigo, user, ct, true);
        var readAt = request.FechaLecturaUtc ?? DateTimeOffset.UtcNow;
        var operationId = Guid.NewGuid();
        var readings = new List<AssetReadingEntity>();
        foreach (var asset in new[] { chassis, factory })
        {
            AssetReadingPolicy.EnsureCanRegister(asset, request.Valor, request.FechaLecturaUtc, "nuevas lecturas de unidad");
            var last = await LastValidReadingAsync(asset.Id, ct);
            if (last is not null && request.Valor < last.Value) throw new DomainException($"La lectura de unidad no puede disminuir para {asset.Code}.");
            var reading = new AssetReadingEntity { AssetId = asset.Id, OperationalUnitId = unit.Id, OperationalUnitReadingOperationId = operationId, ReadAtUtc = readAt, Value = request.Valor, Source = ReadingSource(request.Origen), RegisteredByUserId = user.UserId, EvidenceReference = Text(request.EvidenciaReferencia), Observations = UnitTrace(unit.Code, request.Observaciones) };
            db.AssetReadings.Add(reading);
            readings.Add(reading);
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        await Audit(user, "operational_unit.reading_registered", unit.Code, unit.Faena?.Code, ct);
        return new OperationalUnitReadingResponse(unit.Code, readings.Select(reading => ToReadingResponse(reading)).ToArray());
    }

    public async Task<IReadOnlyCollection<OperationalUnitCorrectableReadingResponse>> GetCorrectableReadingsAsync(string codigo, UserAccessContext user, CancellationToken ct)
    {
        CorrectReadings(user);
        var (unit, chassis, factory) = await RequiredComponentsAsync(codigo, user, ct, true);
        var readings = await db.AssetReadings.AsNoTracking().Where(item => item.AssetId == chassis.Id && item.OperationalUnitId == unit.Id && item.OperationalUnitReadingOperationId != null && !item.IsCompositionSynchronization && !item.IsCorrection).ToListAsync(ct);
        var corrected = (await db.AssetReadings.AsNoTracking().Where(item => item.CorrectedReadingId != null).Select(item => item.CorrectedReadingId!.Value).ToListAsync(ct)).ToHashSet();
        var factoryOperations = (await db.AssetReadings.AsNoTracking().Where(item => item.AssetId == factory.Id && item.OperationalUnitId == unit.Id && item.OperationalUnitReadingOperationId != null && !item.IsCompositionSynchronization && !item.IsCorrection && !corrected.Contains(item.Id)).Select(item => item.OperationalUnitReadingOperationId!.Value).ToListAsync(ct)).ToHashSet();
        return readings.Where(item => !corrected.Contains(item.Id) && factoryOperations.Contains(item.OperationalUnitReadingOperationId!.Value)).OrderByDescending(item => item.ReadAtUtc).ThenByDescending(item => item.CreatedAtUtc).Select(item => new OperationalUnitCorrectableReadingResponse(item.Id.ToString("D"), item.Value, "horas", item.ReadAtUtc)).ToArray();
    }

    public async Task<OperationalUnitReadingResponse> CorrectReadingAsync(string codigo, CorrectOperationalUnitReadingRequest request, UserAccessContext user, CancellationToken ct)
    {
        CorrectReadings(user);
        Require(request.LecturaChasisId, nameof(request.LecturaChasisId));
        Require(request.MotivoCorreccion, nameof(request.MotivoCorreccion));
        if (!Guid.TryParse(request.LecturaChasisId, out var chassisReadingId)) throw new DomainException("Lectura de CHASIS inválida.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var (unit, chassis, factory) = await RequiredComponentsAsync(codigo, user, ct, true);
        var chassisReading = await db.AssetReadings.SingleOrDefaultAsync(item => item.Id == chassisReadingId && item.AssetId == chassis.Id && item.OperationalUnitId == unit.Id && item.OperationalUnitReadingOperationId != null && !item.IsCompositionSynchronization && !item.IsCorrection, ct) ?? throw new DomainException("La lectura seleccionada no es una lectura corregible de CHASIS de esta unidad.");
        var operationId = chassisReading.OperationalUnitReadingOperationId!.Value;
        var factoryReading = await db.AssetReadings.SingleOrDefaultAsync(item => item.AssetId == factory.Id && item.OperationalUnitId == unit.Id && item.OperationalUnitReadingOperationId == operationId && !item.IsCompositionSynchronization && !item.IsCorrection, ct) ?? throw new DomainException("No se pudo identificar de forma inequívoca la lectura de FÁBRICA asociada.");
        if (await db.AssetReadings.AnyAsync(item => item.CorrectedReadingId == chassisReading.Id || item.CorrectedReadingId == factoryReading.Id, ct)) throw new DomainException("La lectura seleccionada ya fue corregida.");
        AssetReadingPolicy.EnsureCanRegister(chassis, request.Valor, null, "correcciones de lectura de unidad");
        AssetReadingPolicy.EnsureCanRegister(factory, request.Valor, null, "correcciones de lectura de unidad");
        var correctionAt = DateTimeOffset.UtcNow;
        var correctionOperationId = Guid.NewGuid();
        var corrections = new[]
        {
            new AssetReadingEntity { AssetId = chassis.Id, OperationalUnitId = unit.Id, OperationalUnitReadingOperationId = correctionOperationId, ReadAtUtc = correctionAt, Value = request.Valor, Source = ReadingSource(request.Origen), RegisteredByUserId = user.UserId, EvidenceReference = Text(request.EvidenciaReferencia), Observations = UnitTrace(unit.Code, request.Observaciones), IsCorrection = true, CorrectedReadingId = chassisReading.Id, CorrectionReason = request.MotivoCorreccion.Trim(), AuthorizedByUserId = user.UserId },
            new AssetReadingEntity { AssetId = factory.Id, OperationalUnitId = unit.Id, OperationalUnitReadingOperationId = correctionOperationId, ReadAtUtc = correctionAt, Value = request.Valor, Source = ReadingSource(request.Origen), RegisteredByUserId = user.UserId, EvidenceReference = Text(request.EvidenciaReferencia), Observations = UnitTrace(unit.Code, request.Observaciones), IsCorrection = true, CorrectedReadingId = factoryReading.Id, CorrectionReason = request.MotivoCorreccion.Trim(), AuthorizedByUserId = user.UserId }
        };
        db.AssetReadings.AddRange(corrections);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        await Audit(user, "operational_unit.reading_corrected", unit.Code, unit.Faena?.Code, ct);
        return new OperationalUnitReadingResponse(unit.Code, corrections.Select(ToReadingResponse).ToArray());
    }

    public async Task<OperationalUnitReadingResponse> CorrectLatestReadingAsync(string codigo, CorrectAssetReadingRequest request, UserAccessContext user, CancellationToken ct)
    {
        var candidates = await GetCorrectableReadingsAsync(codigo, user, ct);
        var selected = candidates.FirstOrDefault() ?? throw new DomainException("No existe una lectura de unidad válida y corregible para CHASIS y FÁBRICA.");
        return await CorrectReadingAsync(codigo, new CorrectOperationalUnitReadingRequest(selected.Id, request.Valor, request.MotivoCorreccion, request.Origen, request.EvidenciaReferencia, request.Observaciones), user, ct);
    }
    public async Task<OperationalUnitStateEventResponse> AddStateEventAsync(string codigo, CreateAssetStateEventRequest request, UserAccessContext user, CancellationToken ct)
    {
        MaintainAssets(user);
        Require(request.EstadoOperacionalCodigo, nameof(request.EstadoOperacionalCodigo));
        Require(request.Motivo, nameof(request.Motivo));
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var (unit, chassis, factory) = await RequiredComponentsAsync(codigo, user, ct);
        var state = await db.AssetOperationalStates.SingleOrDefaultAsync(item => item.Code == Code(request.EstadoOperacionalCodigo) && item.IsActive, ct) ?? throw new DomainException("Estado operacional inexistente.");
        var occurred = request.FechaEventoUtc ?? DateTimeOffset.UtcNow;
        var events = new List<AssetStateEventEntity>();
        foreach (var asset in new[] { chassis, factory })
        {
            var location = await db.AssetPhysicalLocationPeriods.AsNoTracking().SingleOrDefaultAsync(item => item.AssetId == asset.Id && item.ValidToUtc == null, ct) ?? throw new DomainException($"El activo {asset.Code} no tiene ubicación física vigente.");
            AssetOperationalPolicy.EnsureTransitionAllowed(asset.OperationalState.Code, state.Code);
            AssetOperationalPolicy.EnsureCompatibleWithPhysicalLocation(asset.Code, location.LocationType, state.Code, state.Name);
            var previous = asset.OperationalState;
            asset.OperationalStateId = state.Id;
            asset.OperationalState = state;
            asset.UpdatedAtUtc = DateTimeOffset.UtcNow;
            var item = new AssetStateEventEntity { AssetId = asset.Id, PreviousStateId = previous.Id, NewStateId = state.Id, OccurredAtUtc = occurred, UserId = user.UserId, Reason = request.Motivo.Trim(), ReferenceType = "OPERATIONAL_UNIT", ReferenceId = unit.Id.ToString("D"), ReferenceText = unit.Code };
            db.AssetStateEvents.Add(item);
            events.Add(item);
        }
        await SetAssetStateEventCorrelationAsync(events.Select(item => item.Id), ct);
        await OperationalUnitStateCalculator.RecalculateAsync(db, unit, $"UNIDAD:{unit.Code} {request.Motivo}", ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        await Audit(user, "operational_unit.state_changed", unit.Code, unit.Faena?.Code, ct);
        return new OperationalUnitStateEventResponse(unit.Code, events.Select(item => new AssetStateEventResponse(item.Id.ToString("D"), item.Asset.Code, item.PreviousStateId == null ? null : chassis.Id == item.AssetId ? chassis.OperationalState.Code : factory.OperationalState.Code, state.Code, item.OccurredAtUtc, item.Reason, user.UserId, item.ReferenceType, item.ReferenceId, item.ReferenceText)).ToArray());
    }

    public Task<IReadOnlyCollection<AssetTransferResponse>> TransferAsync(string codigo, TransferAssetRequest request, UserAccessContext user, CancellationToken ct)
    {
        if (assetService is null) throw new InvalidOperationException("El servicio de traslado de activos no está disponible.");
        return TransferThroughAssetServiceAsync(codigo, request, user, ct);
    }

    private async Task<IReadOnlyCollection<AssetTransferResponse>> TransferThroughAssetServiceAsync(string codigo, TransferAssetRequest request, UserAccessContext user, CancellationToken ct)
    {
        var (unit, _, _) = await RequiredComponentsAsync(codigo, user, ct);
        var result = await assetService!.TransferOperationalUnitAsync(unit.Code, request, user, ct);
        await Audit(user, "operational_unit.transferred", unit.Code, unit.Faena?.Code, ct);
        return result;
    }

    public Task<IReadOnlyCollection<AssetPhysicalLocationResponse>> RegisterWorkshopEntryAsync(string codigo, RegisterWorkshopEntryRequest request, UserAccessContext user, CancellationToken ct) => MovePhysicalLocationAsync(codigo, "TALLER", request.TallerCodigo, request.FechaEfectivaUtc, request.EstadoOperacionalDestinoCodigo, request.OrdenTrabajoId, request.Motivo, request.Observaciones, user, ct);
    public Task<IReadOnlyCollection<AssetPhysicalLocationResponse>> RegisterReturnToSiteAsync(string codigo, RegisterReturnToSiteRequest request, UserAccessContext user, CancellationToken ct) => MovePhysicalLocationAsync(codigo, "FAENA", null, request.FechaEfectivaUtc, request.EstadoOperacionalDestinoCodigo, request.OrdenTrabajoId, request.Motivo, request.Observaciones, user, ct);

    private async Task<IReadOnlyCollection<AssetPhysicalLocationResponse>> MovePhysicalLocationAsync(string codigo, string targetType, string? workshopCode, DateTimeOffset effectiveAt, string? destinationStateCode, string? workOrderNumber, string? reason, string? observations, UserAccessContext user, CancellationToken ct)
    {
        MaintainAssets(user);
        if (effectiveAt == default) throw new DomainException("La fecha efectiva es obligatoria.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (db.Database.IsNpgsql()) await db.Database.ExecuteSqlRawAsync("LOCK TABLE vigencias_ubicacion_fisica_activo IN SHARE ROW EXCLUSIVE MODE", ct);
        var (unit, chassis, factory) = await RequiredComponentsAsync(codigo, user, ct);
        WorkshopEntity? workshop = null;
        FaenaEntity? faena = null;
        if (targetType == "TALLER")
        {
            Require(workshopCode, nameof(workshopCode));
            workshop = await db.Workshops.Include(item => item.SupervisorUser).SingleOrDefaultAsync(item => item.Code == Code(workshopCode) && item.IsActive, ct) ?? throw new DomainException("El taller no existe o está inactivo.");
            if (string.IsNullOrWhiteSpace(workshop.Commune) || workshop.SupervisorUser is null) throw new DomainException("El taller no está habilitado operacionalmente: requiere comuna y supervisor.");
        }
        else faena = unit.Faena ?? throw new DomainException("La unidad no tiene faena asignada.");
        var state = string.IsNullOrWhiteSpace(destinationStateCode)
            ? throw new DomainException(targetType == "TALLER" ? "Debe seleccionar el estado operacional de destino para ingresar al taller." : "Debe seleccionar el estado operacional de destino para retornar a faena.")
            : await db.AssetOperationalStates.SingleOrDefaultAsync(item => item.Code == Code(destinationStateCode) && item.IsActive, ct) ?? throw new DomainException("Estado operacional destino inexistente.");
        var order = string.IsNullOrWhiteSpace(workOrderNumber) ? null : await db.WorkOrders.SingleOrDefaultAsync(item => item.WorkOrderNumber == Code(workOrderNumber), ct) ?? throw new DomainException("La OT asociada no existe.");
        var components = new[] { chassis, factory };
        var currentLocations = new List<AssetPhysicalLocationPeriodEntity>();
        foreach (var asset in components)
        {
            var current = await db.AssetPhysicalLocationPeriods.SingleOrDefaultAsync(item => item.AssetId == asset.Id && item.ValidToUtc == null, ct) ?? throw new DomainException($"El activo {asset.Code} no tiene ubicación física vigente.");
            if (effectiveAt <= current.ValidFromUtc) throw new DomainException($"La fecha efectiva debe ser posterior al inicio de la ubicación vigente de {asset.Code}.");
            currentLocations.Add(current);
        }
        if (currentLocations.Select(item => item.LocationType + ":" + (item.WorkshopId?.ToString() ?? item.FaenaId?.ToString() ?? string.Empty)).Distinct().Count() != 1) throw new DomainException("La unidad tiene una inconsistencia física preexistente; no se puede registrar un nuevo movimiento.");
        var stateEvents = new List<AssetStateEventEntity>();
        foreach (var asset in components)
        {
            AssetOperationalPolicy.EnsureCompatibleWithPhysicalLocation(asset.Code, targetType, state.Code, state.Name);
            AssetOperationalPolicy.EnsureTransitionAllowed(asset.OperationalState.Code, state.Code);
            var current = currentLocations.Single(item => item.AssetId == asset.Id);
            current.ValidToUtc = effectiveAt;
            db.AssetPhysicalLocationPeriods.Add(new AssetPhysicalLocationPeriodEntity { AssetId = asset.Id, LocationType = targetType, FaenaId = faena?.Id, WorkshopId = workshop?.Id, ValidFromUtc = effectiveAt, Reason = Text(reason), RegisteredByUserId = user.UserId, WorkOrderId = order?.Id, OperationalUnitId = unit.Id, Observations = Text(observations) });
            var previous = asset.OperationalState;
            if (previous.Id != state.Id)
            {
                asset.OperationalStateId = state.Id;
                asset.OperationalState = state;
                var stateEvent = new AssetStateEventEntity { AssetId = asset.Id, PreviousStateId = previous.Id, NewStateId = state.Id, OccurredAtUtc = effectiveAt, UserId = user.UserId, Reason = Text(reason) ?? (targetType == "TALLER" ? "Ingreso efectivo a taller" : "Retorno efectivo a faena"), ReferenceType = "OPERATIONAL_UNIT", ReferenceId = unit.Id.ToString("D"), ReferenceText = unit.Code };
                db.AssetStateEvents.Add(stateEvent);
                stateEvents.Add(stateEvent);
            }
            asset.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }
        await SetAssetStateEventCorrelationAsync(stateEvents.Select(item => item.Id), ct);
        await OperationalUnitStateCalculator.RecalculateAsync(db, unit, $"UNIDAD:{unit.Code} UBICACION_FISICA:{targetType}", ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        await Audit(user, targetType == "TALLER" ? "operational_unit.workshop_entered" : "operational_unit.returned_to_site", unit.Code, unit.Faena?.Code, ct);
        return components.Select(asset => new AssetPhysicalLocationResponse(asset.Code, targetType, targetType == "TALLER" ? workshop!.Name : faena!.Name, workshop?.Commune, effectiveAt, null, user.UserId, order?.WorkOrderNumber, Text(reason), Text(observations), unit.Code, components.Select(item => item.Code).ToArray())).ToArray();
    }
    private async Task MountCoreAsync(OperationalUnitEntity unit, string assetCode, string roleCode, string? workOrder, DateTimeOffset mountedAt, string? observations, string? reason, UserAccessContext u, CancellationToken ct)
    {
        var role = await db.OperationalUnitComponentRoles.SingleOrDefaultAsync(x => x.Code == Code(roleCode) && x.IsActive, ct) ?? throw new DomainException("Rol de componente inexistente.");
        RequireCriticalReason(role, reason);
        var rule = await db.OperationalUnitCompositionRules.Include(x => x.AllowedAssets).SingleOrDefaultAsync(x => x.OperationalUnitTypeId == unit.OperationalUnitTypeId && x.ComponentRoleId == role.Id && x.IsActive, ct) ?? throw new DomainException("No existe regla activa para el rol en este tipo de unidad.");
        var asset = await db.Assets.Include(x => x.AssetTypeDefinition).Include(x => x.Family).Include(x => x.OperationalState).Include(x => x.Faena).ThenInclude(x => x!.TechnicalLocation).SingleOrDefaultAsync(x => x.Code == Code(assetCode), ct) ?? throw new DomainException("Activo inexistente.");
        if (!asset.AssetTypeDefinition.IsMountable) throw new DomainException("El activo no es montable.");
        if (!AssetOperationalPolicy.AllowsMounting(asset.OperationalState.Code)) throw new DomainException("No se puede montar un activo dado de baja en una unidad operativa.");
        var assetLocation = await db.AssetPhysicalLocationPeriods.AsNoTracking().SingleOrDefaultAsync(item => item.AssetId == asset.Id && item.ValidToUtc == null, ct) ?? throw new DomainException("El activo no tiene ubicación física vigente.");
        var mountedLocationTypes = await (from component in db.OperationalUnitComponents.AsNoTracking()
                                          join location in db.AssetPhysicalLocationPeriods.AsNoTracking() on component.AssetId equals location.AssetId
                                          where component.OperationalUnitId == unit.Id && component.RemovedAtUtc == null && location.ValidToUtc == null
                                          select location.LocationType).Distinct().ToArrayAsync(ct);
        if (mountedLocationTypes.Length > 1 || (mountedLocationTypes.Length == 1 && !Same(mountedLocationTypes[0], assetLocation.LocationType)))
            throw new DomainException("Inconsistencia de ubicación física: los componentes vigentes de la unidad deben permanecer en el mismo lugar.");
        if (asset.FaenaId != unit.FaenaId) throw new DomainException("Inconsistencia territorial: activo y unidad deben pertenecer a la misma faena. Traslade la unidad completa o desmonte previamente el componente.");
        if (await db.OperationalUnitComponents.AnyAsync(x => x.AssetId == asset.Id && x.RemovedAtUtc == null, ct)) throw new DomainException("El activo ya esta montado en otra unidad.");
        var allowed = rule.AllowedAssets;
        if (allowed.Count > 0 && !allowed.Any(x => x.AssetTypeId == asset.AssetTypeId || (asset.FamilyId.HasValue && x.EquipmentFamilyId == asset.FamilyId))) throw new DomainException("El tipo o familia del activo no esta permitido para el rol.");
        var count = await db.OperationalUnitComponents.CountAsync(x => x.OperationalUnitId == unit.Id && x.ComponentRoleId == role.Id && x.RemovedAtUtc == null, ct);
        if (count >= rule.MaximumQuantity || (role.IsCritical && count > 0)) throw new DomainException("Se excede la cantidad maxima vigente del rol.");
        db.OperationalUnitComponents.Add(new OperationalUnitComponentEntity { OperationalUnitId = unit.Id, AssetId = asset.Id, ComponentRoleId = role.Id, InstalledAtUtc = mountedAt, InstallationWorkOrderId = await WorkOrderIdAsync(workOrder, ct), InstalledByUserId = u.UserId, InstallationReason = Text(reason), CriticalRoleCode = role.IsCritical ? role.Code : null, Observations = Text(observations) });
    }

    private async Task SynchronizeCompositionReferenceAsync(OperationalUnitEntity unit, string incomingAssetCode, string roleCode, DateTimeOffset occurredAt, UserAccessContext user, CancellationToken ct)
    {
        var components = await db.OperationalUnitComponents.Include(item => item.ComponentRole).Include(item => item.Asset)
            .Where(item => item.OperationalUnitId == unit.Id && item.RemovedAtUtc == null).ToArrayAsync(ct);
        var chassis = components.SingleOrDefault(item => Same(item.ComponentRole.Code, "CHASIS"))?.Asset;
        var factory = components.SingleOrDefault(item => Same(item.ComponentRole.Code, "FABRICA"))?.Asset;
        if (chassis is null || factory is null || !Same(chassis.UsageMeasurementType, "HOROMETRO") || !Same(factory.UsageMeasurementType, "HOROMETRO")) return;
        var chassisReading = await LastValidReadingAsync(chassis.Id, ct);
        if (chassisReading is null) return;
        var target = Same(roleCode, "CHASIS") ? factory : factory.Code == Code(incomingAssetCode) ? factory : null;
        if (target is null) return;
        db.AssetReadings.Add(new AssetReadingEntity
        {
            AssetId = target.Id,
            OperationalUnitId = unit.Id,
            ReadAtUtc = occurredAt,
            Value = chassisReading.Value,
            Source = "MANUAL",
            RegisteredByUserId = user.UserId,
            IsCompositionSynchronization = true,
            Observations = $"Sincronización de referencia operacional por cambio de composición en {unit.Code}; referencia CHASIS {chassis.Code}: {chassisReading.Value}."
        });
    }
    private async Task<(OperationalUnitEntity Unit, AssetEntity Chassis, AssetEntity Factory)> RequiredComponentsAsync(string code, UserAccessContext user, CancellationToken ct, bool requireHourMeters = false)
    {
        var unit = await FindUnitAsync(code, ct) ?? throw new DomainException("Unidad operacional inexistente.");
        EnsureView(user, unit);
        var components = await db.OperationalUnitComponents
            .Include(item => item.ComponentRole)
            .Include(item => item.Asset).ThenInclude(asset => asset.OperationalState)
            .Include(item => item.Asset).ThenInclude(asset => asset.Faena)
            .Where(item => item.OperationalUnitId == unit.Id && item.RemovedAtUtc == null)
            .ToArrayAsync(ct);
        var chassis = components.SingleOrDefault(item => Same(item.ComponentRole.Code, "CHASIS"))?.Asset;
        var factory = components.SingleOrDefault(item => Same(item.ComponentRole.Code, "FABRICA"))?.Asset;
        if (chassis is null || factory is null) throw new DomainException("La unidad debe tener una composición completa con CHASIS y FABRICA vigentes para esta operación.");
        if (requireHourMeters && (!Same(chassis.UsageMeasurementType, "HOROMETRO") || !Same(factory.UsageMeasurementType, "HOROMETRO")))
        {
            var missing = new[] { chassis, factory }.Where(asset => !Same(asset.UsageMeasurementType, "HOROMETRO")).Select(asset => asset.Code).ToArray();
            throw new DomainException($"La operación de unidad requiere medidor de horas en ambos componentes. Sin medidor de horas: {string.Join(", ", missing)}.");
        }
        return (unit, chassis, factory);
    }

    private async Task<AssetReadingEntity?> LastValidReadingAsync(Guid assetId, CancellationToken ct)
    {
        var readings = await db.AssetReadings.Where(item => item.AssetId == assetId).ToListAsync(ct);
        var replaced = readings.Where(item => item.CorrectedReadingId.HasValue).Select(item => item.CorrectedReadingId!.Value).ToHashSet();
        return readings.Where(item => !replaced.Contains(item.Id)).OrderByDescending(item => item.ReadAtUtc).ThenByDescending(item => item.CreatedAtUtc).FirstOrDefault();
    }

    private static AssetReadingResponse ToReadingResponse(AssetReadingEntity reading) => new(reading.Id.ToString("D"), reading.ReadAtUtc, reading.Value, "horas", null, reading.Source, reading.IsCorrection, reading.CorrectedReadingId?.ToString("D"), reading.IsAnomalous, reading.ValidationMessage, reading.Observations);
    private static string ReadingSource(string? value) => Code(value) switch { "ORDEN_TRABAJO" => "ORDEN_TRABAJO", "IMPORTACION" => "IMPORTACION", "SAP" => "SAP", "TELEMETRIA" => "TELEMETRIA", _ => "MANUAL" };
    private static string UnitTrace(string code, string? observations) => string.IsNullOrWhiteSpace(observations) ? $"Operación iniciada desde unidad operacional {code}." : $"Operación iniciada desde unidad operacional {code}. {observations.Trim()}";
    private static void RegisterReadings(UserAccessContext user) { if (user.Permissions.Contains(AuthPermissions.RegisterAssetReadings, StringComparer.OrdinalIgnoreCase) || user.Roles.Contains(AuthRoles.Planner, StringComparer.OrdinalIgnoreCase)) return; throw new UnauthorizedAccessException("No tiene permiso para registrar lecturas de activos."); }
    private static void CorrectReadings(UserAccessContext user) { if (user.Permissions.Contains(AuthPermissions.CorrectAssetReadings, StringComparer.OrdinalIgnoreCase) || user.Roles.Contains(AuthRoles.Planner, StringComparer.OrdinalIgnoreCase)) return; throw new UnauthorizedAccessException("No tiene permiso para corregir lecturas de activos."); }
    private static void MaintainAssets(UserAccessContext user) { if (user.Permissions.Contains(AuthPermissions.ManageAssets, StringComparer.OrdinalIgnoreCase)) return; throw new UnauthorizedAccessException("No tiene permiso para administrar activos."); }
    private async Task SetAssetStateEventCorrelationAsync(IEnumerable<Guid> eventIds, CancellationToken ct)
    {
        if (!db.Database.IsNpgsql()) return;
        var value = string.Join(",", eventIds.Distinct().OrderBy(item => item).Select(item => item.ToString("D")));
        if (value.Length > 0) await db.Database.ExecuteSqlInterpolatedAsync($"SELECT set_config('cmms.asset_state_event_id', {value}, true)", ct);
    }
    private IQueryable<OperationalUnitEntity> Units() => db.OperationalUnits.Include(x => x.OperationalUnitType).Include(x => x.Faena).ThenInclude(x => x!.TechnicalLocation).Include(x => x.OperationalState).Include(x => x.DerivedFromAsset);
    private Task<OperationalUnitEntity?> FindUnitAsync(string code, CancellationToken ct) => Units().SingleOrDefaultAsync(x => x.Code == Code(code), ct);

    private async Task<OperationalUnitCompositionResponse> CompositionAsync(OperationalUnitEntity unit, CancellationToken ct)
    {
        var rows = await db.OperationalUnitComponents.AsNoTracking().Include(x => x.Asset).ThenInclude(x => x.OperationalState).Include(x => x.Asset).ThenInclude(x => x.Faena).ThenInclude(x => x!.TechnicalLocation).Include(x => x.ComponentRole).Include(x => x.InstallationWorkOrder).Include(x => x.RemovalWorkOrder).Where(x => x.OperationalUnitId == unit.Id).OrderBy(x => x.InstalledAtUtc).ToListAsync(ct);
        var rules = await db.OperationalUnitCompositionRules.AsNoTracking().Include(x => x.ComponentRole).Where(x => x.OperationalUnitTypeId == unit.OperationalUnitTypeId && x.IsActive).ToListAsync(ct);
        var missing = rules.Where(x => x.IsMandatory && rows.Count(c => c.RemovedAtUtc is null && c.ComponentRoleId == x.ComponentRoleId) < x.MinimumQuantity).Select(x => x.ComponentRole.Code).ToArray();
        OperationalUnitComponentResponse Map(OperationalUnitComponentEntity x) => new(x.Asset.Code, x.Asset.Name, x.ComponentRole.Code, x.InstalledAtUtc, x.RemovedAtUtc, x.InstallationWorkOrder?.WorkOrderNumber, x.RemovalWorkOrder?.WorkOrderNumber, x.Observations, x.Asset.OperationalState.Code, x.Asset.Faena?.Code, x.Asset.Faena?.TechnicalLocation?.Code, x.InstalledByUserId, x.InstallationReason, x.RemovedByUserId, x.RemovalReason, x.RemovedAtUtc is null, x.Asset.OperationalState.Name);
        return new(missing.Length == 0, missing, rows.Where(x => x.RemovedAtUtc is null).Select(Map).ToArray(), rows.Select(Map).ToArray());
    }

    private async Task<OperationalUnitResponse> MapAsync(OperationalUnitEntity unit, CancellationToken ct)
    {
        var role = unit.DerivedFromAssetId is null ? null : await db.OperationalUnitComponents.AsNoTracking().Include(x => x.ComponentRole).Where(x => x.OperationalUnitId == unit.Id && x.AssetId == unit.DerivedFromAssetId && x.RemovedAtUtc == null).Select(x => x.ComponentRole.Code).FirstOrDefaultAsync(ct);
        var composition = await CompositionAsync(unit, ct);
        var componentCodes = composition.Completa ? composition.Vigentes.Select(item => item.ActivoCodigo).ToArray() : [];
        var componentIds = componentCodes.Length == 0 ? new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase) : (await db.Assets.AsNoTracking().Where(item => componentCodes.Contains(item.Code)).Select(item => new { item.Id, item.Code }).ToListAsync(ct)).ToDictionary(item => item.Code, item => item.Id, StringComparer.OrdinalIgnoreCase);
        var chassisCode = composition.Completa ? composition.Vigentes.SingleOrDefault(item => Same(item.RolComponenteCodigo, "CHASIS"))?.ActivoCodigo : null;
        Guid? chassisId = chassisCode is not null && componentIds.TryGetValue(chassisCode, out var chassisAssetId) ? chassisAssetId : null;
        decimal? lastReading = chassisId is null ? null : (await LastValidReadingAsync(chassisId.Value, ct))?.Value;

        var physicalLocations = componentIds.Count == 0 ? [] : await db.AssetPhysicalLocationPeriods.AsNoTracking()
            .Where(item => item.ValidToUtc == null && componentIds.Values.Contains(item.AssetId))
            .Select(item => new { item.LocationType, LocationId = item.WorkshopId ?? item.FaenaId, LocationName = item.Workshop != null ? item.Workshop.Name : item.Faena != null ? item.Faena.Name : null })
            .ToListAsync(ct);
        var physicalLocation = physicalLocations.Count == componentIds.Count && physicalLocations
            .Select(item => new { item.LocationType, item.LocationId, item.LocationName })
            .Distinct()
            .Count() == 1
            ? physicalLocations[0]
            : null;
        var correctionReady = false;
        if (composition.Completa && chassisId is not null)
        {
            var factoryCode = composition.Vigentes.Single(item => Same(item.RolComponenteCodigo, "FABRICA")).ActivoCodigo;
            Guid? factoryId = componentIds.TryGetValue(factoryCode, out var factoryAssetId) ? factoryAssetId : null;
            if (factoryId is not null)
            {
                var componentAssetIds = new[] { chassisId.Value, factoryId.Value };
                var originals = await db.AssetReadings.Where(item => componentAssetIds.Contains(item.AssetId) && item.OperationalUnitId == unit.Id && item.OperationalUnitReadingOperationId != null && !item.IsCompositionSynchronization && !item.IsCorrection).ToListAsync(ct);
                var replaced = originals.Where(item => item.CorrectedReadingId.HasValue).Select(item => item.CorrectedReadingId!.Value).ToHashSet();
                correctionReady = originals.Where(item => item.AssetId == chassisId && !replaced.Contains(item.Id)).Any(chassisReading => originals.Any(factoryReading => factoryReading.AssetId == factoryId && !replaced.Contains(factoryReading.Id) && factoryReading.OperationalUnitReadingOperationId == chassisReading.OperationalUnitReadingOperationId));
            }
        }
        var derived = new OperationalUnitDerivedStateResponse(unit.OperationalState.Code, unit.DerivedFromAsset?.Code, role, unit.DerivedStateReason, unit.DerivedStateCalculatedAtUtc, unit.OperationalState.Name);
        return new(unit.Code, unit.Name, unit.OperationalUnitType.Code, unit.Faena?.Code, unit.Faena?.TechnicalLocation?.Code, unit.OperationalState.Code, unit.Criticality, unit.CommissioningDate, unit.DecommissioningDate, unit.Observations, composition, derived, unit.OperationalUnitType.Name, unit.Faena?.Name, unit.Faena?.TechnicalLocation?.Name, unit.OperationalState.Name, lastReading, lastReading is null ? null : "horas", physicalLocation?.LocationType, correctionReady, physicalLocation?.LocationName);
    }
    private async Task LockCompositionAsync(CancellationToken ct)
    {
        if (db.Database.IsNpgsql()) await db.Database.ExecuteSqlRawAsync("LOCK TABLE componentes_unidad_operativa IN SHARE ROW EXCLUSIVE MODE", ct);
    }

    private async Task SaveCompositionAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) { throw new DomainException($"La composicion cambio concurrentemente o viola la unicidad de componentes vigentes. {ex.GetBaseException().Message}"); }
    }

    private async Task<Guid?> WorkOrderIdAsync(string? number, CancellationToken ct) => string.IsNullOrWhiteSpace(number) ? null : (await db.WorkOrders.SingleOrDefaultAsync(x => x.WorkOrderNumber == Code(number), ct) ?? throw new DomainException("La OT indicada no existe.")).Id;
    private static void RequireCriticalReason(OperationalUnitComponentRoleEntity role, string? reason) { if (role.IsCritical && string.IsNullOrWhiteSpace(reason)) throw new DomainException($"El motivo es obligatorio para modificar el rol critico {role.Code}."); }
    private static void Require(string? value, string name) { if (string.IsNullOrWhiteSpace(value)) throw new DomainException($"{name} es obligatorio."); }
    private static string Code(string? value) => value?.Trim().ToUpperInvariant() ?? string.Empty;
    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool Same(string? a, string? b) => string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);
    private static bool CanView(UserAccessContext u, OperationalUnitEntity unit) => unit.Faena is null || u.Roles.Contains(AuthRoles.Admin, StringComparer.OrdinalIgnoreCase) || u.Roles.Contains(AuthRoles.Management, StringComparer.OrdinalIgnoreCase) || u.Faenas.Contains(unit.Faena.Code, StringComparer.OrdinalIgnoreCase);
    private static void View(UserAccessContext u) { if (u.Permissions.Contains(AuthPermissions.ViewOperationalUnits, StringComparer.OrdinalIgnoreCase)) return; throw new UnauthorizedAccessException("No tiene permiso para ver unidades operativas."); }
    private static void ManageUnits(UserAccessContext u) { if (u.Permissions.Contains(AuthPermissions.ManageOperationalUnits, StringComparer.OrdinalIgnoreCase)) return; throw new UnauthorizedAccessException("No tiene permiso para administrar unidades operativas."); }
    private static void ManageComposition(UserAccessContext u) { if (u.Permissions.Contains(AuthPermissions.ManageOperationalUnitComposition, StringComparer.OrdinalIgnoreCase)) return; throw new UnauthorizedAccessException("No tiene permiso para administrar la composicion de unidades operativas."); }
    private static void EnsureView(UserAccessContext u, OperationalUnitEntity unit) { if (!CanView(u, unit)) throw new UnauthorizedAccessException("No tiene acceso a la faena de la unidad operativa."); }
    private static void EnsureView(UserAccessContext u, string faena) { if (u.Roles.Contains(AuthRoles.Admin, StringComparer.OrdinalIgnoreCase) || u.Roles.Contains(AuthRoles.Management, StringComparer.OrdinalIgnoreCase) || u.Faenas.Contains(faena, StringComparer.OrdinalIgnoreCase)) return; throw new UnauthorizedAccessException("No tiene acceso a la faena."); }
    private Task Audit(UserAccessContext u, string action, string id, string? faena, CancellationToken ct) => audit.RecordAsync(new AuditEventRequest(u.UserId, action, "OperationalUnits", "OperationalUnit", id, null, null, faena, AuditSeverity.Medium), ct);
}
