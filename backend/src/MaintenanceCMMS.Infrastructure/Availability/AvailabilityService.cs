using System.Globalization;
using System.Text;
using System.Text.Json;
using MaintenanceCMMS.Application.Abstractions.Data;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.Availability;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Domain.Enums;

namespace MaintenanceCMMS.Infrastructure.Availability;

public sealed class AvailabilityService : IAvailabilityService
{
    private const string ContractsSchema = "disponibilidad_contratos";
    private const string AssignmentsSchema = "disponibilidad_activos_contrato";
    private const string EventsSchema = "disponibilidad_eventos";
    private const string AssetsSchema = "activos";
    private const string FaenasSchema = "faenas";
    private const string DocumentsSchema = "documentos";
    private const string WorkOrdersSchema = "ordenes_trabajo";

    private readonly CmmsDbContext _dbContext;
    private readonly IAuditService _auditService;

    public AvailabilityService(CmmsDbContext dbContext, IAuditService auditService)
    {
        _dbContext = dbContext;
        _auditService = auditService;
    }

    public async Task<AvailabilityDashboardResponse> GetDashboardAsync(
        AvailabilityQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanView(user);
        var data = await ReadDataAsync(cancellationToken);
        var range = ResolveRange(query.From, query.To);
        var contractResults = new List<ContractCalculation>();

        foreach (var contract in data.Contracts.Select(ToContract).Where(item => ContractMatches(item, query, user)))
        {
            var calculation = CalculateContract(contract, data, range);
            contractResults.Add(calculation);
        }

        var byContract = contractResults.Select(item => item.Summary).ToArray();
        var unavailable = contractResults.SelectMany(item => item.Unavailable).ToArray();
        var byCause = unavailable
            .GroupBy(item => new { item.Causa, item.PenalizaDisponibilidad })
            .Select(group => new AvailabilityCauseSummary(
                group.Key.Causa,
                Round(group.Sum(item => item.HorasNoDisponibles)),
                group.Count(),
                group.Key.PenalizaDisponibilidad))
            .OrderByDescending(item => item.HorasNoDisponibles)
            .ToArray();
        var byFaena = byContract
            .GroupBy(item => item.FaenaCodigo, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var committed = group.Sum(item => item.EquiposComprometidos);
                var covered = group.Sum(item => item.EquiposCubiertos);
                var committedHours = group.Sum(item => item.HorasComprometidas);
                var availableHours = group.Sum(item => item.HorasDisponibles);
                return new AvailabilityFaenaSummary(
                    group.Key,
                    committed,
                    covered,
                    Ratio(covered, committed),
                    Ratio(availableHours, committedHours));
            })
            .OrderBy(item => item.FaenaCodigo, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var totalCommitted = byContract.Sum(item => item.EquiposComprometidos);
        var totalCovered = byContract.Sum(item => item.EquiposCubiertos);
        var totalCommittedHours = byContract.Sum(item => item.HorasComprometidas);
        var totalAvailableHours = byContract.Sum(item => item.HorasDisponibles);
        var penalizedHours = totalCommittedHours - totalAvailableHours;
        var target = WeightedTarget(byContract);
        var kpi = new AvailabilityKpiResponse(
            totalCommitted,
            totalCovered,
            Math.Max(0, totalCommitted - totalCovered),
            Round(totalCommittedHours),
            Round(totalAvailableHours),
            Round(penalizedHours),
            Ratio(totalCovered, totalCommitted),
            Ratio(totalAvailableHours, totalCommittedHours),
            target,
            Ratio(totalAvailableHours, totalCommittedHours) >= target && Ratio(totalCovered, totalCommitted) >= target);

        var trends = BuildTrends(query, range, data, user);
        var events = data.Events
            .Select(row => ToEventResponse(row, FindAsset(data.Assets, row.GetValue("ActivoCodigo"))))
            .Where(item => EventMatches(item, new AvailabilityEventQuery(range.From, range.To, query.FaenaCodigo, query.ContractCode), user))
            .OrderByDescending(item => item.InicioUtc)
            .ToArray();

        return new AvailabilityDashboardResponse(kpi, byContract, byFaena, byCause, unavailable, trends, events);
    }

    public async Task<IReadOnlyCollection<AvailabilityContractResponse>> ListContractsAsync(
        AvailabilityContractQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanView(user);
        var data = await ReadDataAsync(cancellationToken);
        return data.Contracts
            .Select(ToContract)
            .Where(item => query.IncludeInactive || item.Activo)
            .Where(item => string.IsNullOrWhiteSpace(query.FaenaCodigo) || Same(item.FaenaCodigo, query.FaenaCodigo))
            .Where(item => string.IsNullOrWhiteSpace(query.Cliente) || item.Cliente.Contains(query.Cliente, StringComparison.OrdinalIgnoreCase))
            .Where(item => CanViewFaena(user, item.FaenaCodigo))
            .Select(item => item with { Assets = BuildAssignmentResponses(data, item.ContractCode) })
            .OrderBy(item => item.Cliente, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Nombre, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<AvailabilityContractResponse> UpsertContractAsync(
        UpsertAvailabilityContractRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanManage(user);
        ValidateRequired(request.ContractCode, nameof(request.ContractCode));
        ValidateRequired(request.Nombre, nameof(request.Nombre));
        ValidateRequired(request.Cliente, nameof(request.Cliente));
        ValidateRequired(request.FaenaCodigo, nameof(request.FaenaCodigo));
        EnsureCanUseFaena(user, request.FaenaCodigo);

        var data = await ReadDataAsync(cancellationToken);
        if (!data.Faenas.Any(row => Same(row.GetValue("Codigo"), request.FaenaCodigo)))
        {
            throw new DomainException("La faena indicada no existe.");
        }

        var rows = data.Contracts.ToList();
        var index = rows.FindIndex(row => Same(row.GetValue("ContractCode"), request.ContractCode));
        var previous = index >= 0 ? rows[index] : null;
        var rowToSave = ContractRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["ContractCode"] = NormalizeCode(request.ContractCode),
            ["Nombre"] = NormalizeText(request.Nombre),
            ["Cliente"] = NormalizeText(request.Cliente),
            ["FaenaCodigo"] = NormalizeCode(request.FaenaCodigo),
            ["HorasComprometidasDia"] = FormatNumber(Math.Max(0.1m, request.HorasComprometidasDia)),
            ["DisponibilidadObjetivo"] = FormatNumber(NormalizeTarget(request.DisponibilidadObjetivo)),
            ["FechaInicio"] = FormatOptionalDate(request.FechaInicio),
            ["FechaFin"] = FormatOptionalDate(request.FechaFin),
            ["ReglasCliente"] = NormalizeText(request.ReglasCliente),
            ["Activo"] = request.Activo ? "true" : "false",
            ["ActualizadoEnUtc"] = FormatDate(DateTimeOffset.UtcNow),
            ["ActualizadoPor"] = user.UserId
        });

        if (index >= 0)
        {
            rows[index] = rowToSave;
        }
        else
        {
            rows.Add(rowToSave);
        }

        await _dbContext.SaveOperationalRowsAsync(ContractsSchema, rows, cancellationToken);
        await RecordAuditAsync(user, previous is null ? "availability.contract_created" : "availability.contract_updated", request.ContractCode, previous, rowToSave, request.Reason, request.FaenaCodigo, cancellationToken);

        return ToContract(rowToSave) with { Assets = BuildAssignmentResponses(data with { Contracts = rows }, request.ContractCode) };
    }

    public async Task<AvailabilityContractAssetResponse> AssignAssetAsync(
        AssignContractAssetRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanManage(user);
        ValidateRequired(request.ContractCode, nameof(request.ContractCode));
        ValidateRequired(request.ActivoCodigo, nameof(request.ActivoCodigo));

        var data = await ReadDataAsync(cancellationToken);
        var contract = data.Contracts.Select(ToContract).FirstOrDefault(item => Same(item.ContractCode, request.ContractCode)) ??
                       throw new DomainException("El contrato no existe.");
        var asset = FindAsset(data.Assets, request.ActivoCodigo) ??
                    throw new DomainException("El activo indicado no existe.");
        EnsureCanUseFaena(user, contract.FaenaCodigo);
        if (!Same(asset.FaenaCodigo, contract.FaenaCodigo))
        {
            throw new DomainException("El activo debe pertenecer a la misma faena del contrato.");
        }

        var rows = data.Assignments.ToList();
        var index = rows.FindIndex(row => Same(row.GetValue("ContractCode"), request.ContractCode) && Same(row.GetValue("ActivoCodigo"), request.ActivoCodigo) && Same(row.GetValue("Rol"), request.Rol.ToString()));
        var previous = index >= 0 ? rows[index] : null;
        var rowToSave = AssignmentRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["AssignmentId"] = previous?.GetValue("AssignmentId") ?? NewId("ASG"),
            ["ContractCode"] = NormalizeCode(request.ContractCode),
            ["ActivoCodigo"] = NormalizeCode(request.ActivoCodigo),
            ["Rol"] = request.Rol.ToString(),
            ["FechaInicio"] = FormatOptionalDate(request.FechaInicio),
            ["FechaFin"] = FormatOptionalDate(request.FechaFin),
            ["Activo"] = request.Activo ? "true" : "false",
            ["ActualizadoEnUtc"] = FormatDate(DateTimeOffset.UtcNow),
            ["ActualizadoPor"] = user.UserId
        });

        if (index >= 0)
        {
            rows[index] = rowToSave;
        }
        else
        {
            rows.Add(rowToSave);
        }

        await _dbContext.SaveOperationalRowsAsync(AssignmentsSchema, rows, cancellationToken);
        await RecordAuditAsync(user, previous is null ? "availability.asset_assigned" : "availability.asset_assignment_updated", request.ActivoCodigo, previous, rowToSave, request.Reason, contract.FaenaCodigo, cancellationToken);
        return ToAssignmentResponse(rowToSave, asset);
    }

    public async Task<IReadOnlyCollection<AvailabilityEventResponse>> ListEventsAsync(
        AvailabilityEventQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanView(user);
        var data = await ReadDataAsync(cancellationToken);
        return data.Events
            .Select(row => ToEventResponse(row, FindAsset(data.Assets, row.GetValue("ActivoCodigo"))))
            .Where(item => EventMatches(item, query, user))
            .OrderByDescending(item => item.InicioUtc)
            .ToArray();
    }

    public async Task<AvailabilityEventResponse> RegisterEventAsync(
        RegisterAvailabilityEventRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanManage(user);
        ValidateRequired(request.ContractCode, nameof(request.ContractCode));
        ValidateRequired(request.ActivoCodigo, nameof(request.ActivoCodigo));
        if (request.FinUtc.HasValue && request.FinUtc.Value <= request.InicioUtc)
        {
            throw new DomainException("La fecha de termino debe ser posterior al inicio.");
        }

        var data = await ReadDataAsync(cancellationToken);
        var contract = data.Contracts.Select(ToContract).FirstOrDefault(item => Same(item.ContractCode, request.ContractCode)) ??
                       throw new DomainException("El contrato no existe.");
        var asset = FindAsset(data.Assets, request.ActivoCodigo) ??
                    throw new DomainException("El activo indicado no existe.");
        EnsureCanUseFaena(user, contract.FaenaCodigo);
        if (!Same(asset.FaenaCodigo, contract.FaenaCodigo))
        {
            throw new DomainException("El activo debe pertenecer a la misma faena del contrato.");
        }

        var penalizes = PenalizesAvailability(request.Causa, request.PuedeUtilizarse, request.AtribuibleMantenimiento);
        var row = EventRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["EventId"] = NewId("AVL"),
            ["ContractCode"] = NormalizeCode(request.ContractCode),
            ["ActivoCodigo"] = NormalizeCode(request.ActivoCodigo),
            ["FaenaCodigo"] = NormalizeCode(contract.FaenaCodigo),
            ["Causa"] = request.Causa.ToString(),
            ["InicioUtc"] = FormatDate(request.InicioUtc),
            ["FinUtc"] = FormatOptionalDate(request.FinUtc),
            ["PuedeUtilizarse"] = request.PuedeUtilizarse ? "true" : "false",
            ["AtribuibleMantenimiento"] = request.AtribuibleMantenimiento ? "true" : "false",
            ["PenalizaDisponibilidad"] = penalizes ? "true" : "false",
            ["NumeroOT"] = NormalizeCode(request.NumeroOT),
            ["Comentario"] = NormalizeText(request.Comentario),
            ["UsuarioId"] = user.UserId,
            ["CreatedAtUtc"] = FormatDate(DateTimeOffset.UtcNow)
        });

        var rows = data.Events.ToList();
        rows.Add(row);
        await _dbContext.SaveOperationalRowsAsync(EventsSchema, rows, cancellationToken);
        await RecordAuditAsync(user, "availability.event_registered", row.GetValue("EventId")!, null, row, request.Comentario, contract.FaenaCodigo, cancellationToken);
        return ToEventResponse(row, asset);
    }

    private ContractCalculation CalculateContract(
        AvailabilityContractResponse contract,
        AvailabilityData data,
        AvailabilityRange range)
    {
        var assignments = data.Assignments
            .Select(row => ToAssignmentResponse(row, FindAsset(data.Assets, row.GetValue("ActivoCodigo"))))
            .Where(item => Same(item.ContractCode, contract.ContractCode) && item.Activo && AssignmentOverlaps(item, range))
            .ToArray();
        var committed = assignments.Where(item => item.Rol == ContractAssetRole.Comprometido).ToArray();
        if (committed.Length == 0)
        {
            committed = assignments.Where(item => item.Rol == ContractAssetRole.Asignado).ToArray();
        }

        var support = assignments
            .Where(item => item.Rol is ContractAssetRole.Backup or ContractAssetRole.Arriendo ||
                           (committed.All(committedAsset => !Same(committedAsset.ActivoCodigo, item.ActivoCodigo)) && item.Rol == ContractAssetRole.Asignado))
            .ToArray();
        var unavailable = new List<UnavailableAssetResponse>();
        var committedUnavailable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rawPenalizedHours = 0m;

        foreach (var asset in committed)
        {
            var assetUnavailable = BuildUnavailable(asset, contract, data, range).ToArray();
            unavailable.AddRange(assetUnavailable);
            if (assetUnavailable.Any(item => item.PenalizaDisponibilidad))
            {
                committedUnavailable.Add(asset.ActivoCodigo);
                rawPenalizedHours += assetUnavailable.Where(item => item.PenalizaDisponibilidad).Sum(item => item.HorasNoDisponibles);
            }
        }

        var supportAvailable = support.Count(item => !BuildUnavailable(item, contract, data, range).Any(unavailableItem => unavailableItem.PenalizaDisponibilidad));
        var requiredCount = committed.Length;
        var coveredCount = Math.Min(requiredCount, Math.Max(0, requiredCount - committedUnavailable.Count) + supportAvailable);
        var rangeDays = Math.Max(0m, (decimal)(range.To - range.From).TotalDays);
        var committedHours = requiredCount * contract.HorasComprometidasDia * rangeDays;
        var backupCoverageHours = supportAvailable * contract.HorasComprometidasDia * rangeDays;
        var penalizedHours = Math.Max(0, rawPenalizedHours - backupCoverageHours);
        var availableHours = Math.Max(0, committedHours - penalizedHours);

        if (supportAvailable > 0)
        {
            unavailable = unavailable
                .Select(item => item.PenalizaDisponibilidad ? item with { CubiertoPorBackup = true } : item)
                .ToList();
        }

        var summary = new AvailabilityContractSummary(
            contract.ContractCode,
            contract.Nombre,
            contract.Cliente,
            contract.FaenaCodigo,
            requiredCount,
            coveredCount,
            Round(committedHours),
            Round(availableHours),
            Ratio(coveredCount, requiredCount),
            Ratio(availableHours, committedHours),
            contract.DisponibilidadObjetivo,
            Ratio(availableHours, committedHours) >= contract.DisponibilidadObjetivo && Ratio(coveredCount, requiredCount) >= contract.DisponibilidadObjetivo);

        return new ContractCalculation(summary, unavailable);
    }

    private IEnumerable<UnavailableAssetResponse> BuildUnavailable(
        AvailabilityContractAssetResponse assignment,
        AvailabilityContractResponse contract,
        AvailabilityData data,
        AvailabilityRange range)
    {
        var asset = FindAsset(data.Assets, assignment.ActivoCodigo);
        if (asset is not null && !AssetCanBeUsed(asset))
        {
            yield return new UnavailableAssetResponse(
                contract.ContractCode,
                assignment.ActivoCodigo,
                assignment.ActivoNombre,
                assignment.FaenaCodigo,
                AvailabilityCause.PendienteDiagnostico,
                range.From,
                range.To,
                Round((decimal)(range.To - range.From).TotalHours),
                true,
                false,
                null);
        }

        foreach (var document in data.Documents.Where(row => IsBlockingExpiredDocument(row, assignment.ActivoCodigo, range)))
        {
            var startsAt = DocumentPenaltyStart(document, range);
            yield return new UnavailableAssetResponse(
                contract.ContractCode,
                assignment.ActivoCodigo,
                assignment.ActivoNombre,
                assignment.FaenaCodigo,
                AvailabilityCause.DocumentacionVencida,
                startsAt,
                range.To,
                Round((decimal)(range.To - startsAt).TotalHours),
                true,
                false,
                null);
        }

        foreach (var workOrder in data.WorkOrders.Where(row => WorkOrderCreatesUnavailability(row, assignment.ActivoCodigo, range)))
        {
            var cause = WorkOrderCause(workOrder);
            var interval = WorkOrderInterval(workOrder, range);
            yield return new UnavailableAssetResponse(
                contract.ContractCode,
                assignment.ActivoCodigo,
                assignment.ActivoNombre,
                assignment.FaenaCodigo,
                cause,
                interval.From,
                interval.To,
                Round((decimal)(interval.To - interval.From).TotalHours),
                cause != AvailabilityCause.OperacionalExternaNoAtribuible,
                false,
                workOrder.GetValue("NumeroOT"));
        }

        foreach (var item in data.Events
            .Where(row => Same(row.GetValue("ContractCode"), contract.ContractCode) && Same(row.GetValue("ActivoCodigo"), assignment.ActivoCodigo))
            .Select(row => ToEventResponse(row, FindAsset(data.Assets, row.GetValue("ActivoCodigo"))))
            .Where(item => EventOverlaps(item, range)))
        {
            if (item.PuedeUtilizarse)
            {
                continue;
            }

            var startsAt = item.InicioUtc > range.From ? item.InicioUtc : range.From;
            var endsAt = (item.FinUtc ?? range.To) < range.To ? item.FinUtc ?? range.To : range.To;
            yield return new UnavailableAssetResponse(
                item.ContractCode,
                item.ActivoCodigo,
                item.ActivoNombre,
                item.FaenaCodigo,
                item.Causa,
                startsAt,
                item.FinUtc,
                Round((decimal)(endsAt - startsAt).TotalHours),
                item.PenalizaDisponibilidad,
                false,
                item.NumeroOT);
        }
    }

    private IReadOnlyCollection<AvailabilityTrendPoint> BuildTrends(
        AvailabilityQuery query,
        AvailabilityRange range,
        AvailabilityData data,
        UserAccessContext user)
    {
        var ranges = SplitRange(range, query.Period);
        return ranges.Select(item =>
        {
            var partialQuery = query with { From = item.From, To = item.To };
            var calculations = data.Contracts
                .Select(ToContract)
                .Where(contract => ContractMatches(contract, partialQuery, user))
                .Select(contract => CalculateContract(contract, data, item).Summary)
                .ToArray();
            var committed = calculations.Sum(contract => contract.EquiposComprometidos);
            var covered = calculations.Sum(contract => contract.EquiposCubiertos);
            var committedHours = calculations.Sum(contract => contract.HorasComprometidas);
            var availableHours = calculations.Sum(contract => contract.HorasDisponibles);
            return new AvailabilityTrendPoint(
                PeriodKey(item.From, query.Period),
                item.From,
                item.To,
                Ratio(covered, committed),
                Ratio(availableHours, committedHours),
                committed,
                covered,
                Round(committedHours),
                Round(availableHours));
        }).ToArray();
    }

    private async Task<AvailabilityData> ReadDataAsync(CancellationToken cancellationToken)
    {
        return new AvailabilityData(
            await _dbContext.ReadOperationalRowsAsync(ContractsSchema, cancellationToken),
            await _dbContext.ReadOperationalRowsAsync(AssignmentsSchema, cancellationToken),
            await _dbContext.ReadOperationalRowsAsync(EventsSchema, cancellationToken),
            await _dbContext.ReadOperationalRowsAsync(AssetsSchema, cancellationToken),
            await _dbContext.ReadOperationalRowsAsync(FaenasSchema, cancellationToken),
            await _dbContext.ReadOperationalRowsAsync(DocumentsSchema, cancellationToken),
            await _dbContext.ReadOperationalRowsAsync(WorkOrdersSchema, cancellationToken));
    }

    private IReadOnlyCollection<AvailabilityContractAssetResponse> BuildAssignmentResponses(
        AvailabilityData data,
        string contractCode)
    {
        return data.Assignments
            .Where(row => Same(row.GetValue("ContractCode"), contractCode))
            .Select(row => ToAssignmentResponse(row, FindAsset(data.Assets, row.GetValue("ActivoCodigo"))))
            .OrderBy(item => item.Rol)
            .ThenBy(item => item.ActivoCodigo, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static AvailabilityContractResponse ToContract(DataRow row)
    {
        return new AvailabilityContractResponse(
            row.GetValue("ContractCode") ?? string.Empty,
            row.GetValue("Nombre") ?? string.Empty,
            row.GetValue("Cliente") ?? string.Empty,
            row.GetValue("FaenaCodigo") ?? string.Empty,
            ParseDecimal(row.GetValue("HorasComprometidasDia"), 24),
            NormalizeTarget(ParseDecimal(row.GetValue("DisponibilidadObjetivo"), 0.9m)),
            ParseDate(row.GetValue("FechaInicio")),
            ParseDate(row.GetValue("FechaFin")),
            EmptyToNull(row.GetValue("ReglasCliente")),
            ParseBool(row.GetValue("Activo"), true),
            []);
    }

    private static AvailabilityContractAssetResponse ToAssignmentResponse(DataRow row, AssetInfo? asset)
    {
        return new AvailabilityContractAssetResponse(
            row.GetValue("AssignmentId") ?? string.Empty,
            row.GetValue("ContractCode") ?? string.Empty,
            row.GetValue("ActivoCodigo") ?? string.Empty,
            asset?.Nombre,
            asset?.FaenaCodigo ?? string.Empty,
            ParseEnum(row.GetValue("Rol"), ContractAssetRole.Comprometido),
            ParseDate(row.GetValue("FechaInicio")),
            ParseDate(row.GetValue("FechaFin")),
            ParseBool(row.GetValue("Activo"), true));
    }

    private static AvailabilityEventResponse ToEventResponse(DataRow row, AssetInfo? asset)
    {
        return ToEventResponse(
            row,
            row.GetValue("ActivoCodigo") ?? string.Empty,
            asset?.Nombre,
            asset?.FaenaCodigo ?? row.GetValue("FaenaCodigo") ?? string.Empty);
    }

    private static AvailabilityEventResponse ToEventResponse(
        DataRow row,
        string assetCode,
        string? assetName,
        string faenaCodigo)
    {
        var cause = ParseEnum(row.GetValue("Causa"), AvailabilityCause.PendienteDiagnostico);
        var canUse = ParseBool(row.GetValue("PuedeUtilizarse"));
        var attributable = ParseBool(row.GetValue("AtribuibleMantenimiento"), true);
        return new AvailabilityEventResponse(
            row.GetValue("EventId") ?? string.Empty,
            row.GetValue("ContractCode") ?? string.Empty,
            assetCode,
            assetName,
            faenaCodigo,
            cause,
            ParseDate(row.GetValue("InicioUtc")) ?? DateTimeOffset.MinValue,
            ParseDate(row.GetValue("FinUtc")),
            canUse,
            attributable,
            ParseBool(row.GetValue("PenalizaDisponibilidad"), PenalizesAvailability(cause, canUse, attributable)),
            EmptyToNull(row.GetValue("NumeroOT")),
            EmptyToNull(row.GetValue("Comentario")),
            row.GetValue("UsuarioId") ?? string.Empty,
            ParseDate(row.GetValue("CreatedAtUtc")) ?? DateTimeOffset.MinValue);
    }

    private static AssetInfo? FindAsset(IReadOnlyCollection<DataRow> rows, string? assetCode)
    {
        if (string.IsNullOrWhiteSpace(assetCode))
        {
            return null;
        }

        var row = rows.FirstOrDefault(item => Same(item.GetValue("Codigo"), assetCode));
        return row is null
            ? null
            : new AssetInfo(
                row.GetValue("Codigo") ?? string.Empty,
                EmptyToNull(row.GetValue("Nombre")),
                row.GetValue("FaenaCodigo") ?? string.Empty,
                ParseEnum(row.GetValue("Estado"), AssetStatus.Active),
                EmptyToNull(row.GetValue("EstadoOperacional")));
    }

    private static bool ContractMatches(AvailabilityContractResponse contract, AvailabilityQuery query, UserAccessContext user)
    {
        return contract.Activo &&
               CanViewFaena(user, contract.FaenaCodigo) &&
               (string.IsNullOrWhiteSpace(query.FaenaCodigo) || Same(contract.FaenaCodigo, query.FaenaCodigo)) &&
               (string.IsNullOrWhiteSpace(query.ContractCode) || Same(contract.ContractCode, query.ContractCode)) &&
               (string.IsNullOrWhiteSpace(query.Cliente) || contract.Cliente.Contains(query.Cliente, StringComparison.OrdinalIgnoreCase));
    }

    private static bool EventMatches(AvailabilityEventResponse item, AvailabilityEventQuery query, UserAccessContext user)
    {
        return CanViewFaena(user, item.FaenaCodigo) &&
               (string.IsNullOrWhiteSpace(query.FaenaCodigo) || Same(item.FaenaCodigo, query.FaenaCodigo)) &&
               (string.IsNullOrWhiteSpace(query.ContractCode) || Same(item.ContractCode, query.ContractCode)) &&
               (string.IsNullOrWhiteSpace(query.ActivoCodigo) || Same(item.ActivoCodigo, query.ActivoCodigo)) &&
               (!query.Cause.HasValue || item.Causa == query.Cause) &&
               (!query.From.HasValue || (item.FinUtc ?? DateTimeOffset.UtcNow) >= query.From.Value) &&
               (!query.To.HasValue || item.InicioUtc <= query.To.Value);
    }

    private static bool AssignmentOverlaps(AvailabilityContractAssetResponse assignment, AvailabilityRange range)
    {
        var starts = assignment.FechaInicio ?? DateTimeOffset.MinValue;
        var ends = assignment.FechaFin ?? DateTimeOffset.MaxValue;
        return starts < range.To && ends > range.From;
    }

    private static bool EventOverlaps(AvailabilityEventResponse item, AvailabilityRange range)
    {
        return item.InicioUtc < range.To && (item.FinUtc ?? range.To) > range.From;
    }

    private static bool AssetCanBeUsed(AssetInfo asset)
    {
        if (asset.Status is AssetStatus.Draft or AssetStatus.Unavailable or AssetStatus.Retired)
        {
            return false;
        }

        var state = asset.EstadoOperacional ?? string.Empty;
        return !state.Contains("no disponible", StringComparison.OrdinalIgnoreCase) &&
               !state.Contains("mantenimiento", StringComparison.OrdinalIgnoreCase) &&
               !state.Contains("retirado", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBlockingExpiredDocument(DataRow row, string assetCode, AvailabilityRange range)
    {
        if (!Same(row.GetValue("EntidadTipo"), "Activo") || !Same(row.GetValue("EntidadCodigo"), assetCode))
        {
            return false;
        }

        var status = row.GetValue("Estado") ?? string.Empty;
        if (status.Equals("Anulado", StringComparison.OrdinalIgnoreCase) ||
            status.Equals("Reemplazado", StringComparison.OrdinalIgnoreCase) ||
            ParseBool(row.GetValue("EsHistorico")))
        {
            return false;
        }

        var expiry = ParseDate(row.GetValue("FechaVencimiento"));
        if (!expiry.HasValue || expiry.Value >= range.To)
        {
            return false;
        }

        return ParseBool(row.GetValue("Critico")) || ParseBool(row.GetValue("BloqueaDisponibilidad"));
    }

    private static DateTimeOffset DocumentPenaltyStart(DataRow row, AvailabilityRange range)
    {
        var expiry = ParseDate(row.GetValue("FechaVencimiento")) ?? range.From;
        return expiry > range.From ? expiry : range.From;
    }

    private static bool WorkOrderCreatesUnavailability(DataRow row, string assetCode, AvailabilityRange range)
    {
        if (!Same(row.GetValue("ActivoCodigo"), assetCode))
        {
            return false;
        }

        var status = row.GetValue("Estado") ?? string.Empty;
        if (status.Equals("Anulada", StringComparison.OrdinalIgnoreCase) ||
            status.Equals("ValidadaPlanificacion", StringComparison.OrdinalIgnoreCase) ||
            status.Equals("CerradaTecnicamente", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var interval = WorkOrderInterval(row, range);
        return interval.From < range.To && interval.To > range.From &&
               (status.Equals("EnEjecucion", StringComparison.OrdinalIgnoreCase) ||
                status.Equals("PendienteRepuestos", StringComparison.OrdinalIgnoreCase) ||
                status.Equals("PendienteDocumentacion", StringComparison.OrdinalIgnoreCase) ||
                status.Equals("Pausada", StringComparison.OrdinalIgnoreCase));
    }

    private static AvailabilityRange WorkOrderInterval(DataRow row, AvailabilityRange range)
    {
        var from = ParseDate(row.GetValue("FechaInicioRealUtc")) ??
                   ParseDate(row.GetValue("FechaInicioProgramada")) ??
                   ParseDate(row.GetValue("FechaProgramada")) ??
                   range.From;
        var to = ParseDate(row.GetValue("FechaCierreSupervisorUtc")) ??
                 ParseDate(row.GetValue("FechaFinalizacionTecnicoUtc")) ??
                 ParseDate(row.GetValue("FechaFinProgramada")) ??
                 range.To;
        if (to <= from)
        {
            to = range.To;
        }

        return new AvailabilityRange(from > range.From ? from : range.From, to < range.To ? to : range.To);
    }

    private static AvailabilityCause WorkOrderCause(DataRow row)
    {
        var status = row.GetValue("Estado") ?? string.Empty;
        if (status.Equals("PendienteRepuestos", StringComparison.OrdinalIgnoreCase))
        {
            return AvailabilityCause.Repuestos;
        }

        if (status.Equals("PendienteDocumentacion", StringComparison.OrdinalIgnoreCase))
        {
            return AvailabilityCause.DocumentacionVencida;
        }

        var type = row.GetValue("TipoMantenimiento") ?? string.Empty;
        return type.Contains("Prevent", StringComparison.OrdinalIgnoreCase)
            ? AvailabilityCause.MantenimientoPreventivo
            : AvailabilityCause.MantenimientoCorrectivo;
    }

    private static bool PenalizesAvailability(AvailabilityCause cause, bool canUse, bool attributable)
    {
        return !canUse && attributable && cause != AvailabilityCause.OperacionalExternaNoAtribuible;
    }

    private static AvailabilityRange ResolveRange(DateTimeOffset? from, DateTimeOffset? to)
    {
        var now = DateTimeOffset.UtcNow;
        var start = from ?? new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var end = to ?? now;
        if (end <= start)
        {
            throw new DomainException("El rango de disponibilidad es invalido.");
        }

        return new AvailabilityRange(start, end);
    }

    private static IReadOnlyCollection<AvailabilityRange> SplitRange(AvailabilityRange range, AvailabilityPeriod period)
    {
        if (period == AvailabilityPeriod.Acumulado)
        {
            return [range];
        }

        var items = new List<AvailabilityRange>();
        var cursor = range.From;
        while (cursor < range.To)
        {
            var next = period switch
            {
                AvailabilityPeriod.Dia => cursor.Date.AddDays(1),
                AvailabilityPeriod.Semana => cursor.Date.AddDays(7),
                AvailabilityPeriod.Mes => new DateTimeOffset(cursor.Year, cursor.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(1),
                _ => range.To
            };
            if (next <= cursor)
            {
                next = cursor.AddDays(1);
            }

            if (next > range.To)
            {
                next = range.To;
            }

            items.Add(new AvailabilityRange(cursor, next));
            cursor = next;
        }

        return items;
    }

    private static string PeriodKey(DateTimeOffset from, AvailabilityPeriod period)
    {
        return period switch
        {
            AvailabilityPeriod.Dia => from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            AvailabilityPeriod.Semana => $"{from:yyyy}-W{ISOWeek.GetWeekOfYear(from.UtcDateTime):00}",
            AvailabilityPeriod.Mes => from.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            _ => "Acumulado"
        };
    }

    private async Task RecordAuditAsync(
        UserAccessContext user,
        string action,
        string entityId,
        DataRow? previous,
        DataRow next,
        string? reason,
        string? faenaCodigo,
        CancellationToken cancellationToken)
    {
        await _auditService.RecordAsync(new AuditEventRequest(
            user.UserId,
            action,
            "Disponibilidad",
            "Availability",
            entityId,
            previous is null ? null : Serialize(previous),
            Serialize(next),
            faenaCodigo,
            AuditSeverity.Medium,
            reason,
            Detail: reason), cancellationToken);
    }

    private static void EnsureCanView(UserAccessContext user)
    {
        if (!(CanManage(user) ||
              HasRole(user, AuthRoles.Management) ||
              HasRole(user, AuthRoles.FaenaViewer) ||
              HasRole(user, AuthRoles.Technician)))
        {
            throw new UnauthorizedAccessException("El usuario no tiene permiso para ver disponibilidad.");
        }
    }

    private static void EnsureCanManage(UserAccessContext user)
    {
        if (!CanManage(user))
        {
            throw new UnauthorizedAccessException("El usuario no tiene permiso para gestionar disponibilidad.");
        }
    }

    private static bool CanManage(UserAccessContext user)
    {
        return HasRole(user, AuthRoles.Admin) ||
               HasRole(user, AuthRoles.Planner) ||
               HasRole(user, AuthRoles.MaintenanceSupervisor) ||
               HasPermission(user, AuthPermissions.Administration);
    }

    private static bool CanViewFaena(UserAccessContext user, string? faenaCodigo)
    {
        return string.IsNullOrWhiteSpace(faenaCodigo) ||
               HasRole(user, AuthRoles.Admin) ||
               HasPermission(user, AuthPermissions.Administration) ||
               (HasRole(user, AuthRoles.Planner) && user.Faenas.Count == 0) ||
               user.Faenas.Contains(faenaCodigo, StringComparer.OrdinalIgnoreCase);
    }

    private static void EnsureCanUseFaena(UserAccessContext user, string faenaCodigo)
    {
        if (!CanViewFaena(user, faenaCodigo))
        {
            throw new UnauthorizedAccessException("El usuario no tiene acceso a la faena indicada.");
        }
    }

    private static DataRow ContractRow(IReadOnlyDictionary<string, string?> values) => Row(ContractColumns, values);

    private static DataRow AssignmentRow(IReadOnlyDictionary<string, string?> values) => Row(AssignmentColumns, values);

    private static DataRow EventRow(IReadOnlyDictionary<string, string?> values) => Row(EventColumns, values);

    private static DataRow Row(IEnumerable<string> columns, IReadOnlyDictionary<string, string?> values)
    {
        return new DataRow(columns.ToDictionary(column => column, column => values.TryGetValue(column, out var value) ? value : null, StringComparer.OrdinalIgnoreCase));
    }

    private static void ValidateRequired(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"El campo {fieldName} es obligatorio.");
        }
    }

    private static string Serialize(DataRow row)
    {
        return JsonSerializer.Serialize(row.Values);
    }

    private static string NewId(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 13, prefix.Length + 33)].ToUpperInvariant();

    private static string? NormalizeText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeCode(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string FormatDate(DateTimeOffset value) => value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    private static string? FormatOptionalDate(DateTimeOffset? value) => value.HasValue ? FormatDate(value.Value) : null;

    private static string FormatNumber(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    private static DateTimeOffset? ParseDate(string? value)
    {
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;
    }

    private static decimal ParseDecimal(string? value, decimal fallback = 0)
    {
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback)
        where TEnum : struct
    {
        return Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : fallback;
    }

    private static bool ParseBool(string? value, bool fallback = false)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("si", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("1", StringComparison.OrdinalIgnoreCase);
    }

    private static decimal NormalizeTarget(decimal value)
    {
        if (value > 1)
        {
            value /= 100;
        }

        return Math.Clamp(value, 0, 1);
    }

    private static decimal Ratio(decimal numerator, decimal denominator)
    {
        return denominator <= 0 ? 1 : Round(numerator / denominator);
    }

    private static decimal Ratio(int numerator, int denominator)
    {
        return denominator <= 0 ? 1 : Round((decimal)numerator / denominator);
    }

    private static decimal WeightedTarget(IReadOnlyCollection<AvailabilityContractSummary> contracts)
    {
        var totalHours = contracts.Sum(item => item.HorasComprometidas);
        if (totalHours <= 0)
        {
            return contracts.Count == 0 ? 1 : Round(contracts.Average(item => item.DisponibilidadObjetivo));
        }

        return Round(contracts.Sum(item => item.DisponibilidadObjetivo * item.HorasComprometidas) / totalHours);
    }

    private static decimal Round(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);

    private static bool Same(string? left, string? right) => string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool HasRole(UserAccessContext user, string role) => user.Roles.Contains(role, StringComparer.OrdinalIgnoreCase);

    private static bool HasPermission(UserAccessContext user, string permission) => user.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

    private static readonly string[] ContractColumns =
    [
        "ContractCode",
        "Nombre",
        "Cliente",
        "FaenaCodigo",
        "HorasComprometidasDia",
        "DisponibilidadObjetivo",
        "FechaInicio",
        "FechaFin",
        "ReglasCliente",
        "Activo",
        "ActualizadoEnUtc",
        "ActualizadoPor"
    ];

    private static readonly string[] AssignmentColumns =
    [
        "AssignmentId",
        "ContractCode",
        "ActivoCodigo",
        "Rol",
        "FechaInicio",
        "FechaFin",
        "Activo",
        "ActualizadoEnUtc",
        "ActualizadoPor"
    ];

    private static readonly string[] EventColumns =
    [
        "EventId",
        "ContractCode",
        "ActivoCodigo",
        "FaenaCodigo",
        "Causa",
        "InicioUtc",
        "FinUtc",
        "PuedeUtilizarse",
        "AtribuibleMantenimiento",
        "PenalizaDisponibilidad",
        "NumeroOT",
        "Comentario",
        "UsuarioId",
        "CreatedAtUtc"
    ];

    private sealed record AvailabilityData(
        IReadOnlyCollection<DataRow> Contracts,
        IReadOnlyCollection<DataRow> Assignments,
        IReadOnlyCollection<DataRow> Events,
        IReadOnlyCollection<DataRow> Assets,
        IReadOnlyCollection<DataRow> Faenas,
        IReadOnlyCollection<DataRow> Documents,
        IReadOnlyCollection<DataRow> WorkOrders);

    private sealed record AvailabilityRange(DateTimeOffset From, DateTimeOffset To);

    private sealed record ContractCalculation(
        AvailabilityContractSummary Summary,
        IReadOnlyCollection<UnavailableAssetResponse> Unavailable);

    private sealed record AssetInfo(
        string Codigo,
        string? Nombre,
        string FaenaCodigo,
        AssetStatus Status,
        string? EstadoOperacional);
}
