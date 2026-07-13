using System.Globalization;
using MaintenanceCMMS.Application.Abstractions.Data;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Application.Alerts;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.PreventiveMaintenance;
using MaintenanceCMMS.Application.WorkOrders;
using MaintenanceCMMS.Domain.Common;

namespace MaintenanceCMMS.Infrastructure.PreventiveMaintenance;

public sealed class PreventiveMaintenanceService : IPreventiveMaintenanceService
{
    private const string PlansSchema = "planes_preventivos";
    private const string ReadingsSchema = "preventivo_lecturas";
    private const string EvaluationsSchema = "preventivo_evaluaciones";
    private const string HistorySchema = "preventivo_historial";
    private const string AssetsSchema = "activos";
    private const string WorkOrdersSchema = "ordenes_trabajo";
    private const decimal AnomalousHourJump = 500;
    private const decimal AnomalousKmJump = 5000;

    private readonly CmmsDbContext _dbContext;
    private readonly IWorkOrderService _workOrderService;
    private readonly IAlertService _alertService;
    private readonly IAuditService _auditService;

    public PreventiveMaintenanceService(
        CmmsDbContext dbContext,
        IWorkOrderService workOrderService,
        IAlertService alertService,
        IAuditService auditService)
    {
        _dbContext = dbContext;
        _workOrderService = workOrderService;
        _alertService = alertService;
        _auditService = auditService;
    }

    public async Task<IReadOnlyCollection<PreventivePlanResponse>> ListPlansAsync(
        PreventivePlanQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanView(user);
        var data = await ReadDataAsync(cancellationToken);

        return data.Plans
            .Select(row => ToPlanResponse(row, FindAsset(data.Assets, row.GetValue("ActivoCodigo"))))
            .Where(item => query.IncludeInactive || item.Activo)
            .Where(item => string.IsNullOrWhiteSpace(query.FaenaCodigo) || Same(item.FaenaCodigo, query.FaenaCodigo))
            .Where(item => string.IsNullOrWhiteSpace(query.ActivoCodigo) || Same(item.ActivoCodigo, query.ActivoCodigo))
            .Where(item => string.IsNullOrWhiteSpace(query.FamiliaEquipo) || Same(item.FamiliaEquipo, query.FamiliaEquipo))
            .Where(item => !query.Estado.HasValue || item.Estado == query.Estado)
            .Where(item => string.IsNullOrWhiteSpace(item.FaenaCodigo) || CanAccessFaena(user, item.FaenaCodigo))
            .OrderBy(item => item.Nombre, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<PreventivePlanResponse> UpsertPlanAsync(
        UpsertPreventivePlanRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanManage(user);
        ValidateRequired(request.Codigo, nameof(request.Codigo));
        ValidateRequired(request.Nombre, nameof(request.Nombre));
        if (string.IsNullOrWhiteSpace(request.ActivoCodigo) && string.IsNullOrWhiteSpace(request.FamiliaEquipo))
        {
            throw new DomainException("Debe asociar el plan a un activo o a una familia de equipo.");
        }

        if (!request.FrecuenciaHoras.HasValue && !request.FrecuenciaKm.HasValue && !request.FrecuenciaDias.HasValue)
        {
            throw new DomainException("Debe configurar al menos una frecuencia por horas, km o calendario.");
        }

        var data = await ReadDataAsync(cancellationToken);
        var assetRow = FindAsset(data.Assets, request.ActivoCodigo);
        var asset = assetRow is null ? null : ToAssetInfo(assetRow);
        if (!string.IsNullOrWhiteSpace(request.ActivoCodigo) && asset is null)
        {
            throw new DomainException($"El activo '{request.ActivoCodigo}' no existe.");
        }

        if (asset is not null)
        {
            EnsureFaenaAccess(user, asset.FaenaCodigo);
        }

        var rows = data.Plans.ToList();
        var index = rows.FindIndex(row => Same(row.GetValue("Codigo"), request.Codigo));
        var previous = index >= 0 ? rows[index] : null;
        var previousValues = previous is null ? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase) : CopyRow(previous, PlanColumns);
        var now = DateTimeOffset.UtcNow;
        var row = PlanRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Codigo"] = NormalizeCode(request.Codigo),
            ["Nombre"] = NormalizeText(request.Nombre),
            ["ActivoCodigo"] = NormalizeCode(request.ActivoCodigo),
            ["FamiliaEquipo"] = NormalizeText(request.FamiliaEquipo),
            ["Marca"] = NormalizeText(request.Marca),
            ["Modelo"] = NormalizeText(request.Modelo),
            ["Frecuencia"] = ResolveFrequencyType(request).ToString(),
            ["FrecuenciaHoras"] = FormatOptionalNumber(request.FrecuenciaHoras),
            ["FrecuenciaKm"] = FormatOptionalNumber(request.FrecuenciaKm),
            ["FrecuenciaDias"] = FormatOptionalNumber(request.FrecuenciaDias),
            ["ToleranciaHoras"] = FormatNumber(Math.Max(0, request.ToleranciaHoras)),
            ["ToleranciaKm"] = FormatNumber(Math.Max(0, request.ToleranciaKm)),
            ["ToleranciaDias"] = Math.Max(0, request.ToleranciaDias).ToString(CultureInfo.InvariantCulture),
            ["ChecklistCodigo"] = NormalizeCode(request.ChecklistCodigo),
            ["RepuestosSugeridos"] = NormalizeText(request.RepuestosSugeridos),
            ["HHEstimadas"] = FormatNumber(Math.Max(0.1m, request.HHEstimadas)),
            ["FechaInicio"] = FormatOptionalDate(request.FechaInicio ?? ParseDate(previousValues.GetValueOrDefault("FechaInicio")) ?? now),
            ["UltimaEjecucionFecha"] = previousValues.GetValueOrDefault("UltimaEjecucionFecha"),
            ["UltimaEjecucionHoras"] = previousValues.GetValueOrDefault("UltimaEjecucionHoras"),
            ["UltimaEjecucionKm"] = previousValues.GetValueOrDefault("UltimaEjecucionKm"),
            ["ProximaFecha"] = previousValues.GetValueOrDefault("ProximaFecha"),
            ["ProximaHora"] = previousValues.GetValueOrDefault("ProximaHora"),
            ["ProximoKm"] = previousValues.GetValueOrDefault("ProximoKm"),
            ["Estado"] = previousValues.GetValueOrDefault("Estado") ?? PreventiveStatus.Vigente.ToString(),
            ["Activo"] = request.Activo ? "true" : "false",
            ["ActualizadoEnUtc"] = FormatDate(now),
            ["ActualizadoPor"] = user.UserId
        });

        if (index >= 0)
        {
            rows[index] = row;
        }
        else
        {
            rows.Add(row);
        }

        await _dbContext.SaveOperationalRowsAsync(PlansSchema, rows, cancellationToken);
        await RecordAuditAsync(user, previous is null ? "preventive.plan_created" : "preventive.plan_updated", request.Codigo, previous, row, request.Reason, cancellationToken);
        return ToPlanResponse(row, assetRow);
    }

    public async Task<IReadOnlyCollection<PreventiveReadingResponse>> ListReadingsAsync(
        PreventiveReadingQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanView(user);
        var data = await ReadDataAsync(cancellationToken);
        return data.Readings
            .Select(row =>
            {
                var assetRow = FindAsset(data.Assets, row.GetValue("ActivoCodigo"));
                return ToReadingResponse(row, assetRow is null ? null : ToAssetInfo(assetRow));
            })
            .Where(item => string.IsNullOrWhiteSpace(query.ActivoCodigo) || Same(item.ActivoCodigo, query.ActivoCodigo))
            .Where(item => string.IsNullOrWhiteSpace(query.FaenaCodigo) || Same(item.FaenaCodigo, query.FaenaCodigo))
            .Where(item => !query.From.HasValue || item.FechaLectura >= query.From.Value)
            .Where(item => !query.To.HasValue || item.FechaLectura <= query.To.Value)
            .Where(item => CanAccessFaena(user, item.FaenaCodigo))
            .OrderByDescending(item => item.FechaLectura)
            .ToArray();
    }

    public async Task<PreventiveReadingResponse> RegisterReadingAsync(
        RegisterPreventiveReadingRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanRegisterReading(user);
        ValidateRequired(request.ActivoCodigo, nameof(request.ActivoCodigo));
        if (!request.Horometro.HasValue && !request.Kilometraje.HasValue)
        {
            throw new DomainException("Debe indicar horometro, kilometraje o ambos.");
        }

        var data = await ReadDataAsync(cancellationToken);
        var assetRow = FindAsset(data.Assets, request.ActivoCodigo) ??
                    throw new DomainException($"El activo '{request.ActivoCodigo}' no existe.");
        var asset = ToAssetInfo(assetRow);
        EnsureFaenaAccess(user, asset.FaenaCodigo);

        var latest = LatestReading(data.Readings, asset.Codigo);
        var latestResponse = latest is null ? null : ToReadingResponse(latest, asset);
        var isCorrection = IsLowerReading(request.Horometro, latestResponse?.Horometro) ||
                           IsLowerReading(request.Kilometraje, latestResponse?.Kilometraje);
        if (isCorrection)
        {
            if (!request.AutorizarCorreccion || !CanManage(user))
            {
                throw new DomainException("No se permite registrar una lectura menor sin correccion autorizada.");
            }

            ValidateRequired(request.MotivoCorreccion, nameof(request.MotivoCorreccion));
        }

        var anomalyMessages = new List<string>();
        AddAnomalyMessage(anomalyMessages, "horometro", request.Horometro, latestResponse?.Horometro, AnomalousHourJump);
        AddAnomalyMessage(anomalyMessages, "kilometraje", request.Kilometraje, latestResponse?.Kilometraje, AnomalousKmJump);
        var isAnomalous = anomalyMessages.Count > 0;
        var row = ReadingRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["ReadingId"] = NewId("LEC"),
            ["ActivoCodigo"] = NormalizeCode(request.ActivoCodigo),
            ["Horometro"] = FormatOptionalNumber(request.Horometro),
            ["Kilometraje"] = FormatOptionalNumber(request.Kilometraje),
            ["FechaLectura"] = FormatDate(request.FechaLectura),
            ["UsuarioId"] = user.UserId,
            ["Evidencia"] = NormalizeText(request.Evidencia),
            ["EsCorreccion"] = isCorrection ? "true" : "false",
            ["EsAnomala"] = isAnomalous ? "true" : "false",
            ["MensajeValidacion"] = anomalyMessages.Count == 0 ? null : string.Join(" | ", anomalyMessages),
            ["MotivoCorreccion"] = NormalizeText(request.MotivoCorreccion),
            ["AutorizadoPor"] = isCorrection ? user.UserId : null,
            ["CreadoEnUtc"] = FormatDate(DateTimeOffset.UtcNow)
        });

        var rows = data.Readings.ToList();
        rows.Add(row);
        await _dbContext.SaveOperationalRowsAsync(ReadingsSchema, rows, cancellationToken);
        await RecordAuditAsync(user, "preventive.reading_registered", row.GetValue("ReadingId")!, null, row, request.MotivoCorreccion, cancellationToken);
        return ToReadingResponse(row, asset);
    }

    public async Task<PreventiveDashboardResponse> EvaluateAsync(
        PreventiveEvaluationQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanView(user);
        return await EvaluateInternalAsync(query, user, query.GenerateWorkOrders, cancellationToken);
    }

    public async Task<PreventiveWorkOrderGenerationResponse> GenerateWorkOrderAsync(
        string planCode,
        GeneratePreventiveWorkOrderRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanManage(user);
        ValidateRequired(planCode, nameof(planCode));
        var data = await ReadDataAsync(cancellationToken);
        var planRow = data.Plans.FirstOrDefault(row => Same(row.GetValue("Codigo"), planCode)) ??
                      throw new DomainException("El plan preventivo no existe.");
        var targets = MatchAssets(planRow, data.Assets).ToArray();
        if (!string.IsNullOrWhiteSpace(request.ActivoCodigo))
        {
            targets = targets.Where(asset => Same(asset.Codigo, request.ActivoCodigo)).ToArray();
        }

        if (targets.Length == 0)
        {
            throw new DomainException("El plan preventivo no tiene activos aplicables.");
        }

        if (targets.Length > 1)
        {
            throw new DomainException("Debe indicar ActivoCodigo para generar OT desde un plan por familia.");
        }

        var asset = targets[0];
        EnsureFaenaAccess(user, asset.FaenaCodigo);
        var evaluation = EvaluatePlanForAsset(planRow, asset, data, request.Force ? null : FindOpenPreventiveWorkOrder(data.WorkOrders, planCode, asset.Codigo), request.Force);
        if (!request.Force && evaluation.Estado is PreventiveStatus.Vigente or PreventiveStatus.ProximoAVencer)
        {
            throw new DomainException("El preventivo aun no esta en ventana de generacion.");
        }

        if (!request.Force && !string.IsNullOrWhiteSpace(evaluation.NumeroOT))
        {
            return new PreventiveWorkOrderGenerationResponse(planCode, asset.Codigo, evaluation.NumeroOT, PreventiveStatus.OTGenerada, ["Ya existe una OT preventiva abierta para este plan y activo."]);
        }

        var warnings = new List<string>();
        var plan = ToPlanResponse(planRow, asset.ToDataRow());
        var workOrder = await _workOrderService.CreatePreventiveAsync(new CreatePreventiveWorkOrderRequest(
            asset.Codigo,
            $"Preventivo {plan.Nombre}",
            plan.Codigo,
            asset.FaenaCodigo,
            FechaProgramada: evaluation.FechaVencimientoEstimada,
            FechaInicioProgramada: evaluation.FechaVencimientoEstimada,
            FechaFinProgramada: evaluation.FechaVencimientoEstimada?.AddHours((double)Math.Max(1, plan.HHEstimadas))),
            user,
            cancellationToken);

        var task = await _workOrderService.AddTaskAsync(workOrder.Summary.NumeroOT, new CreateWorkOrderTaskRequest(
            $"Ejecutar {plan.Nombre}",
            CodigoTarea: "T-001",
            FechaInicioProgramada: evaluation.FechaVencimientoEstimada,
            FechaFinProgramada: evaluation.FechaVencimientoEstimada?.AddHours((double)Math.Max(1, plan.HHEstimadas)),
            RequiereEvidencia: true,
            RequiereHH: true,
            ChecklistObligatorio: !string.IsNullOrWhiteSpace(plan.ChecklistCodigo),
            Observaciones: $"Plan preventivo {plan.Codigo}"),
            user,
            cancellationToken);

        if (task is not null && !string.IsNullOrWhiteSpace(plan.ChecklistCodigo))
        {
            try
            {
                await _workOrderService.ApplyChecklistTemplateAsync(workOrder.Summary.NumeroOT, new ApplyChecklistTemplateRequest(task.CodigoTarea, plan.ChecklistCodigo), user, cancellationToken);
            }
            catch (DomainException ex)
            {
                warnings.Add(ex.Message);
            }
        }

        if (task is not null)
        {
            foreach (var sparePart in ParseSuggestedSpareParts(plan.RepuestosSugeridos))
            {
                try
                {
                    await _workOrderService.AddSparePartAsync(workOrder.Summary.NumeroOT, new AddWorkOrderSparePartRequest(
                        task.CodigoTarea,
                        sparePart.Code,
                        sparePart.Quantity,
                        sparePart.Unit,
                        Observaciones: $"Sugerido por plan {plan.Codigo}"),
                        user,
                        cancellationToken);
                }
                catch (DomainException ex)
                {
                    warnings.Add(ex.Message);
                }
            }
        }

        await UpdatePlanStateAsync(plan.Codigo, asset.Codigo, PreventiveStatus.OTGenerada, user, $"OT preventiva {workOrder.Summary.NumeroOT} generada", workOrder.Summary.NumeroOT, cancellationToken);
        await SaveEvaluationAsync(evaluation with { Estado = PreventiveStatus.OTGenerada, NumeroOT = workOrder.Summary.NumeroOT }, cancellationToken);
        await GenerateAlertSafeAsync(
            "preventive-created",
            "Preventivo creado automaticamente",
            $"Se genero la OT {workOrder.Summary.NumeroOT} desde el plan {plan.Codigo}.",
            asset.FaenaCodigo,
            "PreventivePlan",
            plan.Codigo,
            $"{plan.Codigo}:{asset.Codigo}:{workOrder.Summary.NumeroOT}",
            cancellationToken);

        return new PreventiveWorkOrderGenerationResponse(plan.Codigo, asset.Codigo, workOrder.Summary.NumeroOT, PreventiveStatus.OTGenerada, warnings);
    }

    public async Task<PreventivePlanResponse?> ReprogramAsync(
        string planCode,
        ReprogramPreventivePlanRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanManage(user);
        ValidateRequired(request.Reason, nameof(request.Reason));
        var data = await ReadDataAsync(cancellationToken);
        var rows = data.Plans.ToList();
        var index = rows.FindIndex(row => Same(row.GetValue("Codigo"), planCode));
        if (index < 0)
        {
            return null;
        }

        var previous = rows[index];
        var assetRow = FindAsset(data.Assets, previous.GetValue("ActivoCodigo"));
        var asset = assetRow is null ? null : ToAssetInfo(assetRow);
        if (asset is not null)
        {
            EnsureFaenaAccess(user, asset.FaenaCodigo);
        }

        var values = CopyRow(previous, PlanColumns);
        values["ProximaFecha"] = FormatOptionalDate(request.ProximaFecha) ?? values.GetValueOrDefault("ProximaFecha");
        values["ProximaHora"] = FormatOptionalNumber(request.ProximaHora) ?? values.GetValueOrDefault("ProximaHora");
        values["ProximoKm"] = FormatOptionalNumber(request.ProximoKm) ?? values.GetValueOrDefault("ProximoKm");
        values["Estado"] = PreventiveStatus.Reprogramado.ToString();
        values["ActualizadoPor"] = user.UserId;
        values["ActualizadoEnUtc"] = FormatDate(DateTimeOffset.UtcNow);
        var updated = PlanRow(values);
        rows[index] = updated;

        await _dbContext.SaveOperationalRowsAsync(PlansSchema, rows, cancellationToken);
        await AddHistoryAsync(planCode, previous.GetValue("ActivoCodigo") ?? string.Empty, ParseStatus(previous.GetValue("Estado")), PreventiveStatus.Reprogramado, user, request.Reason!, null, cancellationToken);
        await RecordAuditAsync(user, "preventive.plan_reprogrammed", planCode, previous, updated, request.Reason, cancellationToken);
        return ToPlanResponse(updated, assetRow);
    }

    public async Task<PreventiveEngineRunResponse> RunAutomaticEvaluationAsync(
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanManage(user);
        var dashboard = await EvaluateInternalAsync(new PreventiveEvaluationQuery(EvaluationDate: DateTimeOffset.UtcNow), user, false, cancellationToken);
        var generated = 0;
        var alerts = 0;
        var warnings = new List<string>();

        foreach (var item in dashboard.DueItems.Where(item => item.Estado is PreventiveStatus.EnVentana or PreventiveStatus.Vencido && string.IsNullOrWhiteSpace(item.NumeroOT)))
        {
            try
            {
                var result = await GenerateWorkOrderAsync(item.PlanCodigo, new GeneratePreventiveWorkOrderRequest(item.ActivoCodigo), user, cancellationToken);
                generated++;
                warnings.AddRange(result.Warnings);
            }
            catch (DomainException ex)
            {
                warnings.Add($"{item.PlanCodigo}/{item.ActivoCodigo}: {ex.Message}");
            }
        }

        foreach (var item in dashboard.DueItems.Where(item => item.Estado == PreventiveStatus.Vencido))
        {
            var generatedAlert = await GenerateAlertSafeAsync(
                "preventive-overdue",
                "Preventivo vencido",
                item.Mensaje,
                item.FaenaCodigo,
                "PreventivePlan",
                item.PlanCodigo,
                $"{item.PlanCodigo}:{item.ActivoCodigo}:vencido",
                cancellationToken);
            alerts += generatedAlert ? 1 : 0;
        }

        return new PreventiveEngineRunResponse(dashboard.DueItems.Count, generated, alerts, warnings);
    }

    private async Task<PreventiveDashboardResponse> EvaluateInternalAsync(
        PreventiveEvaluationQuery query,
        UserAccessContext user,
        bool generateWorkOrders,
        CancellationToken cancellationToken)
    {
        var data = await ReadDataAsync(cancellationToken);
        var planResponses = new List<PreventivePlanResponse>();
        var dueItems = new List<PreventiveDueResponse>();
        var evaluationDate = query.EvaluationDate ?? DateTimeOffset.UtcNow;

        foreach (var planRow in data.Plans.Where(row => ParseBool(row.GetValue("Activo"), true)))
        {
            var targetAssets = MatchAssets(planRow, data.Assets)
                .Where(asset => string.IsNullOrWhiteSpace(query.ActivoCodigo) || Same(asset.Codigo, query.ActivoCodigo))
                .Where(asset => string.IsNullOrWhiteSpace(query.FaenaCodigo) || Same(asset.FaenaCodigo, query.FaenaCodigo))
                .Where(asset => CanAccessFaena(user, asset.FaenaCodigo))
                .ToArray();

            foreach (var asset in targetAssets)
            {
                var openOt = FindOpenPreventiveWorkOrder(data.WorkOrders, planRow.GetValue("Codigo"), asset.Codigo);
                var evaluation = EvaluatePlanForAsset(planRow, asset, data, openOt, false, evaluationDate);
                dueItems.Add(evaluation);
                await SaveEvaluationAsync(evaluation, cancellationToken);
                await UpdatePlanStateAsync(evaluation.PlanCodigo, asset.Codigo, evaluation.Estado, user, "Evaluacion preventiva", evaluation.NumeroOT, cancellationToken, updatePlanOnlyWhenSpecificAsset: true);

                if (generateWorkOrders && evaluation.Estado is PreventiveStatus.EnVentana or PreventiveStatus.Vencido && string.IsNullOrWhiteSpace(evaluation.NumeroOT))
                {
                    await GenerateWorkOrderAsync(evaluation.PlanCodigo, new GeneratePreventiveWorkOrderRequest(asset.Codigo), user, cancellationToken);
                }
            }

            planResponses.Add(ToPlanResponse(planRow, FindAsset(data.Assets, planRow.GetValue("ActivoCodigo"))));
        }

        var history = (await _dbContext.ReadOperationalRowsAsync(HistorySchema, cancellationToken))
            .Select(ToHistoryResponse)
            .OrderByDescending(item => item.FechaUtc)
            .Take(200)
            .ToArray();
        var calendar = dueItems
            .Where(item => item.FechaVencimientoEstimada.HasValue)
            .Select(item => new PreventiveCalendarItemResponse(item.PlanCodigo, item.Nombre, item.ActivoCodigo, item.ActivoNombre, item.FaenaCodigo, item.FechaVencimientoEstimada!.Value, item.Estado, item.NumeroOT))
            .OrderBy(item => item.Fecha)
            .ToArray();

        return new PreventiveDashboardResponse(
            planResponses
                .Where(item => string.IsNullOrWhiteSpace(query.FaenaCodigo) || Same(item.FaenaCodigo, query.FaenaCodigo))
                .Where(item => string.IsNullOrWhiteSpace(query.ActivoCodigo) || Same(item.ActivoCodigo, query.ActivoCodigo))
                .ToArray(),
            dueItems.OrderByDescending(item => item.Estado).ThenBy(item => item.FechaVencimientoEstimada).ToArray(),
            calendar,
            history);
    }

    private PreventiveDueResponse EvaluatePlanForAsset(
        DataRow planRow,
        AssetInfo asset,
        PreventiveData data,
        DataRow? openWorkOrder,
        bool forced,
        DateTimeOffset? evaluationDate = null)
    {
        var plan = ToPlanResponse(planRow, asset.ToDataRow());
        var now = evaluationDate ?? DateTimeOffset.UtcNow;
        var latest = LatestReading(data.Readings, asset.Codigo);
        var latestReading = latest is null ? null : ToReadingResponse(latest, asset);
        var states = new List<TriggerEvaluation>();

        if (plan.FrecuenciaHoras.HasValue)
        {
            var baseValue = plan.UltimaEjecucionHoras ?? 0;
            var next = plan.ProximaHora ?? baseValue + plan.FrecuenciaHoras.Value;
            var current = latestReading?.Horometro ?? baseValue;
            states.Add(EvaluateMeter("horas", next - current, plan.ToleranciaHoras, plan.FrecuenciaHoras.Value));
        }

        if (plan.FrecuenciaKm.HasValue)
        {
            var baseValue = plan.UltimaEjecucionKm ?? 0;
            var next = plan.ProximoKm ?? baseValue + plan.FrecuenciaKm.Value;
            var current = latestReading?.Kilometraje ?? baseValue;
            states.Add(EvaluateMeter("km", next - current, plan.ToleranciaKm, plan.FrecuenciaKm.Value));
        }

        DateTimeOffset? dueDate = null;
        if (plan.FrecuenciaDias.HasValue)
        {
            var baseDate = plan.UltimaEjecucionFecha ?? plan.FechaInicio ?? now;
            dueDate = plan.ProximaFecha ?? baseDate.AddDays(plan.FrecuenciaDias.Value);
            var days = (int)Math.Floor((dueDate.Value.Date - now.Date).TotalDays);
            states.Add(EvaluateCalendar(days, plan.ToleranciaDias, plan.FrecuenciaDias.Value));
        }

        if (states.Count == 0)
        {
            states.Add(new TriggerEvaluation(PreventiveStatus.Vigente, null, null, null, "Sin frecuencia configurada."));
        }

        var worst = states.OrderByDescending(item => StatusRank(item.Status)).First();
        var status = openWorkOrder is not null && !forced ? PreventiveStatus.OTGenerada : worst.Status;
        var message = BuildEvaluationMessage(plan, asset, status, worst, openWorkOrder);
        return new PreventiveDueResponse(
            plan.Codigo,
            plan.Nombre,
            asset.Codigo,
            asset.Nombre,
            asset.FaenaCodigo,
            status,
            states.FirstOrDefault(item => item.Kind == "horas")?.Remaining,
            states.FirstOrDefault(item => item.Kind == "km")?.Remaining,
            states.FirstOrDefault(item => item.Kind == "calendario")?.DaysRemaining,
            dueDate,
            openWorkOrder?.GetValue("NumeroOT"),
            message);
    }

    private static TriggerEvaluation EvaluateMeter(string kind, decimal remaining, decimal tolerance, decimal frequency)
    {
        var closeWindow = Math.Max(tolerance, Math.Round(frequency * 0.1m, 2));
        var status = remaining < -tolerance
            ? PreventiveStatus.Vencido
            : remaining <= tolerance
                ? PreventiveStatus.EnVentana
                : remaining <= closeWindow
                    ? PreventiveStatus.ProximoAVencer
                    : PreventiveStatus.Vigente;
        return new TriggerEvaluation(status, kind, remaining, null, $"{kind}: quedan {remaining.ToString(CultureInfo.InvariantCulture)}.");
    }

    private static TriggerEvaluation EvaluateCalendar(int daysRemaining, int toleranceDays, int frequencyDays)
    {
        var closeWindow = Math.Max(toleranceDays, Math.Max(7, (int)Math.Ceiling(frequencyDays * 0.1d)));
        var status = daysRemaining < -toleranceDays
            ? PreventiveStatus.Vencido
            : daysRemaining <= toleranceDays
                ? PreventiveStatus.EnVentana
                : daysRemaining <= closeWindow
                    ? PreventiveStatus.ProximoAVencer
                    : PreventiveStatus.Vigente;
        return new TriggerEvaluation(status, "calendario", null, daysRemaining, $"calendario: quedan {daysRemaining} dias.");
    }

    private async Task SaveEvaluationAsync(PreventiveDueResponse item, CancellationToken cancellationToken)
    {
        var rows = (await _dbContext.ReadOperationalRowsAsync(EvaluationsSchema, cancellationToken)).ToList();
        rows.Add(new DataRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["EvaluacionId"] = NewId("EVAL"),
            ["PlanCodigo"] = NormalizeCode(item.PlanCodigo),
            ["ActivoCodigo"] = NormalizeCode(item.ActivoCodigo),
            ["FaenaCodigo"] = NormalizeCode(item.FaenaCodigo),
            ["Estado"] = item.Estado.ToString(),
            ["HorasRestantes"] = FormatOptionalNumber(item.HorasRestantes),
            ["KmRestantes"] = FormatOptionalNumber(item.KmRestantes),
            ["DiasRestantes"] = item.DiasRestantes?.ToString(CultureInfo.InvariantCulture),
            ["FechaVencimientoEstimada"] = FormatOptionalDate(item.FechaVencimientoEstimada),
            ["NumeroOT"] = NormalizeCode(item.NumeroOT),
            ["Mensaje"] = item.Mensaje,
            ["FechaEvaluacionUtc"] = FormatDate(DateTimeOffset.UtcNow)
        }));
        await _dbContext.SaveOperationalRowsAsync(EvaluationsSchema, rows, cancellationToken);
    }

    private async Task UpdatePlanStateAsync(
        string planCode,
        string assetCode,
        PreventiveStatus nextStatus,
        UserAccessContext user,
        string reason,
        string? numeroOt,
        CancellationToken cancellationToken,
        bool updatePlanOnlyWhenSpecificAsset = false)
    {
        var rows = (await _dbContext.ReadOperationalRowsAsync(PlansSchema, cancellationToken)).ToList();
        var index = rows.FindIndex(row => Same(row.GetValue("Codigo"), planCode));
        if (index < 0)
        {
            return;
        }

        var previous = rows[index];
        if (updatePlanOnlyWhenSpecificAsset && !Same(previous.GetValue("ActivoCodigo"), assetCode))
        {
            await AddHistoryIfChangedAsync(planCode, assetCode, nextStatus, user, reason, numeroOt, cancellationToken);
            return;
        }

        var previousStatus = ParseStatus(previous.GetValue("Estado"));
        var values = CopyRow(previous, PlanColumns);
        values["Estado"] = nextStatus.ToString();
        values["ActualizadoPor"] = user.UserId;
        values["ActualizadoEnUtc"] = FormatDate(DateTimeOffset.UtcNow);
        var updated = PlanRow(values);
        rows[index] = updated;
        await _dbContext.SaveOperationalRowsAsync(PlansSchema, rows, cancellationToken);
        if (previousStatus != nextStatus)
        {
            await AddHistoryAsync(planCode, assetCode, previousStatus, nextStatus, user, reason, numeroOt, cancellationToken);
        }
    }

    private async Task AddHistoryIfChangedAsync(
        string planCode,
        string assetCode,
        PreventiveStatus nextStatus,
        UserAccessContext user,
        string reason,
        string? numeroOt,
        CancellationToken cancellationToken)
    {
        var evaluations = await _dbContext.ReadOperationalRowsAsync(EvaluationsSchema, cancellationToken);
        var previous = evaluations
            .Where(row => Same(row.GetValue("PlanCodigo"), planCode) && Same(row.GetValue("ActivoCodigo"), assetCode))
            .OrderByDescending(row => ParseDate(row.GetValue("FechaEvaluacionUtc")) ?? DateTimeOffset.MinValue)
            .Skip(1)
            .FirstOrDefault();
        var previousStatus = previous is null ? PreventiveStatus.Vigente : ParseStatus(previous.GetValue("Estado"));
        if (previousStatus != nextStatus)
        {
            await AddHistoryAsync(planCode, assetCode, previousStatus, nextStatus, user, reason, numeroOt, cancellationToken);
        }
    }

    private async Task AddHistoryAsync(
        string planCode,
        string assetCode,
        PreventiveStatus previous,
        PreventiveStatus next,
        UserAccessContext user,
        string reason,
        string? numeroOt,
        CancellationToken cancellationToken)
    {
        var rows = (await _dbContext.ReadOperationalRowsAsync(HistorySchema, cancellationToken)).ToList();
        rows.Add(new DataRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["HistoryId"] = NewId("HIS"),
            ["PlanCodigo"] = NormalizeCode(planCode),
            ["ActivoCodigo"] = NormalizeCode(assetCode),
            ["EstadoAnterior"] = previous.ToString(),
            ["EstadoNuevo"] = next.ToString(),
            ["FechaUtc"] = FormatDate(DateTimeOffset.UtcNow),
            ["UsuarioId"] = user.UserId,
            ["Motivo"] = reason,
            ["NumeroOT"] = NormalizeCode(numeroOt)
        }));
        await _dbContext.SaveOperationalRowsAsync(HistorySchema, rows, cancellationToken);
    }

    private async Task<bool> GenerateAlertSafeAsync(
        string ruleCode,
        string title,
        string message,
        string faenaCodigo,
        string entityType,
        string entityId,
        string causeKey,
        CancellationToken cancellationToken)
    {
        try
        {
            await _alertService.GenerateAsync(new GenerateAlertRequest(
                ruleCode,
                title,
                message,
                "PreventiveMaintenance",
                causeKey,
                faenaCodigo,
                entityType,
                entityId,
                new Dictionary<string, string?>
                {
                    ["FaenaCodigo"] = faenaCodigo,
                    ["EntityId"] = entityId
                }),
                SystemUser,
                cancellationToken);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task<PreventiveData> ReadDataAsync(CancellationToken cancellationToken)
    {
        return new PreventiveData(
            await _dbContext.ReadOperationalRowsAsync(PlansSchema, cancellationToken),
            await _dbContext.ReadOperationalRowsAsync(ReadingsSchema, cancellationToken),
            await _dbContext.ReadOperationalRowsAsync(EvaluationsSchema, cancellationToken),
            await _dbContext.ReadOperationalRowsAsync(AssetsSchema, cancellationToken),
            await _dbContext.ReadOperationalRowsAsync(WorkOrdersSchema, cancellationToken));
    }

    private IEnumerable<AssetInfo> MatchAssets(DataRow planRow, IReadOnlyCollection<DataRow> assets)
    {
        var activoCodigo = NormalizeCode(planRow.GetValue("ActivoCodigo"));
        var family = NormalizeText(planRow.GetValue("FamiliaEquipo"));
        var brand = NormalizeText(planRow.GetValue("Marca"));
        var model = NormalizeText(planRow.GetValue("Modelo"));

        return assets
            .Select(ToAssetInfo)
            .Where(asset => string.IsNullOrWhiteSpace(activoCodigo) || Same(asset.Codigo, activoCodigo))
            .Where(asset => string.IsNullOrWhiteSpace(family) || Same(asset.Familia, family))
            .Where(asset => string.IsNullOrWhiteSpace(brand) || Same(asset.Marca, brand))
            .Where(asset => string.IsNullOrWhiteSpace(model) || Same(asset.Modelo, model));
    }

    private static DataRow? FindOpenPreventiveWorkOrder(IReadOnlyCollection<DataRow> rows, string? planCode, string assetCode)
    {
        return rows.FirstOrDefault(row =>
            Same(row.GetValue("PlanPreventivoCodigo"), planCode) &&
            Same(row.GetValue("ActivoCodigo"), assetCode) &&
            ParseBool(row.GetValue("EsPreventivaAutomatica")) &&
            !IsClosedWorkOrder(row.GetValue("Estado")));
    }

    private static bool IsClosedWorkOrder(string? status)
    {
        return status is not null &&
               (status.Equals(WorkOrderLifecycleStatus.ValidadaPlanificacion.ToString(), StringComparison.OrdinalIgnoreCase) ||
                status.Equals(WorkOrderLifecycleStatus.CerradaTecnicamente.ToString(), StringComparison.OrdinalIgnoreCase) ||
                status.Equals(WorkOrderLifecycleStatus.Anulada.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    private static PreventivePlanResponse ToPlanResponse(DataRow row, DataRow? asset)
    {
        var assetInfo = asset is null ? null : ToAssetInfo(asset);
        var frequency = ParseFrequency(row);
        return new PreventivePlanResponse(
            row.GetValue("Codigo") ?? string.Empty,
            row.GetValue("Nombre") ?? string.Empty,
            EmptyToNull(row.GetValue("ActivoCodigo")),
            assetInfo?.Nombre,
            assetInfo?.FaenaCodigo,
            EmptyToNull(row.GetValue("FamiliaEquipo")),
            EmptyToNull(row.GetValue("Marca")),
            EmptyToNull(row.GetValue("Modelo")),
            frequency,
            ParseNullableDecimal(row.GetValue("FrecuenciaHoras")),
            ParseNullableDecimal(row.GetValue("FrecuenciaKm")),
            ParseNullableInt(row.GetValue("FrecuenciaDias")),
            ParseDecimal(row.GetValue("ToleranciaHoras")),
            ParseDecimal(row.GetValue("ToleranciaKm")),
            ParseInt(row.GetValue("ToleranciaDias")),
            EmptyToNull(row.GetValue("ChecklistCodigo")),
            EmptyToNull(row.GetValue("RepuestosSugeridos")),
            ParseDecimal(row.GetValue("HHEstimadas"), 1),
            ParseDate(row.GetValue("FechaInicio")),
            ParseDate(row.GetValue("UltimaEjecucionFecha")),
            ParseNullableDecimal(row.GetValue("UltimaEjecucionHoras")),
            ParseNullableDecimal(row.GetValue("UltimaEjecucionKm")),
            ParseDate(row.GetValue("ProximaFecha")),
            ParseNullableDecimal(row.GetValue("ProximaHora")),
            ParseNullableDecimal(row.GetValue("ProximoKm")),
            ParseStatus(row.GetValue("Estado")),
            ParseBool(row.GetValue("Activo"), true));
    }

    private static PreventiveReadingResponse ToReadingResponse(DataRow row, AssetInfo? asset)
    {
        var faenaCodigo = asset?.FaenaCodigo ?? string.Empty;
        return new PreventiveReadingResponse(
            row.GetValue("ReadingId") ?? string.Empty,
            row.GetValue("ActivoCodigo") ?? string.Empty,
            asset?.Nombre,
            faenaCodigo,
            ParseNullableDecimal(row.GetValue("Horometro")),
            ParseNullableDecimal(row.GetValue("Kilometraje")),
            ParseDate(row.GetValue("FechaLectura")) ?? DateTimeOffset.MinValue,
            row.GetValue("UsuarioId") ?? string.Empty,
            EmptyToNull(row.GetValue("Evidencia")),
            ParseBool(row.GetValue("EsCorreccion")),
            ParseBool(row.GetValue("EsAnomala")),
            EmptyToNull(row.GetValue("MensajeValidacion")),
            EmptyToNull(row.GetValue("AutorizadoPor")));
    }

    private static PreventiveHistoryResponse ToHistoryResponse(DataRow row)
    {
        return new PreventiveHistoryResponse(
            row.GetValue("HistoryId") ?? string.Empty,
            row.GetValue("PlanCodigo") ?? string.Empty,
            row.GetValue("ActivoCodigo") ?? string.Empty,
            ParseStatus(row.GetValue("EstadoAnterior")),
            ParseStatus(row.GetValue("EstadoNuevo")),
            ParseDate(row.GetValue("FechaUtc")) ?? DateTimeOffset.MinValue,
            row.GetValue("UsuarioId") ?? string.Empty,
            row.GetValue("Motivo") ?? string.Empty,
            EmptyToNull(row.GetValue("NumeroOT")));
    }

    private static AssetInfo ToAssetInfo(DataRow row)
    {
        return new AssetInfo(
            row.GetValue("Codigo") ?? string.Empty,
            row.GetValue("Nombre"),
            row.GetValue("FaenaCodigo") ?? string.Empty,
            EmptyToNull(row.GetValue("Familia")),
            EmptyToNull(row.GetValue("Marca")),
            EmptyToNull(row.GetValue("Modelo")));
    }

    private static DataRow? FindAsset(IEnumerable<DataRow> rows, string? assetCode)
    {
        if (string.IsNullOrWhiteSpace(assetCode))
        {
            return null;
        }

        return rows.FirstOrDefault(row => Same(row.GetValue("Codigo"), assetCode));
    }

    private static DataRow? LatestReading(IEnumerable<DataRow> rows, string assetCode)
    {
        return rows
            .Where(row => Same(row.GetValue("ActivoCodigo"), assetCode))
            .OrderByDescending(row => ParseDate(row.GetValue("FechaLectura")) ?? DateTimeOffset.MinValue)
            .FirstOrDefault();
    }

    private async Task RecordAuditAsync(
        UserAccessContext user,
        string action,
        string entityId,
        DataRow? previous,
        DataRow? next,
        string? reason,
        CancellationToken cancellationToken)
    {
        await _auditService.RecordAsync(new AuditEventRequest(
            user.UserId,
            action,
            AuditModules.WorkOrders,
            "PreventiveMaintenance",
            entityId,
            previous is null ? null : Serialize(previous),
            next is null ? null : Serialize(next),
            Reason: reason,
            Severity: action.Contains("reprogram", StringComparison.OrdinalIgnoreCase) ? AuditSeverity.High : AuditSeverity.Medium),
            cancellationToken);
    }

    private void EnsureCanView(UserAccessContext user)
    {
        if (!(CanManage(user) ||
              HasRole(user, AuthRoles.Technician) ||
              HasRole(user, AuthRoles.Management) ||
              HasRole(user, AuthRoles.FaenaViewer)))
        {
            throw new UnauthorizedAccessException("El usuario no tiene permiso para ver preventivos.");
        }
    }

    private void EnsureCanRegisterReading(UserAccessContext user)
    {
        if (!(CanManage(user) || HasRole(user, AuthRoles.Technician)))
        {
            throw new UnauthorizedAccessException("El usuario no tiene permiso para registrar lecturas.");
        }
    }

    private void EnsureCanManage(UserAccessContext user)
    {
        if (!CanManage(user))
        {
            throw new UnauthorizedAccessException("El usuario no tiene permiso para gestionar preventivos.");
        }
    }

    private static bool CanManage(UserAccessContext user)
    {
        return HasRole(user, AuthRoles.Admin) ||
               HasRole(user, AuthRoles.Planner) ||
               HasRole(user, AuthRoles.MaintenanceSupervisor) ||
               HasPermission(user, AuthPermissions.Administration) ||
               HasPermission(user, AuthPermissions.FinalValidateWorkOrders);
    }

    private static bool CanAccessFaena(UserAccessContext user, string? faenaCodigo)
    {
        return string.IsNullOrWhiteSpace(faenaCodigo) ||
               HasRole(user, AuthRoles.Admin) ||
               (HasRole(user, AuthRoles.Planner) && user.Faenas.Count == 0) ||
               HasPermission(user, AuthPermissions.Administration) ||
               user.Faenas.Contains(faenaCodigo, StringComparer.OrdinalIgnoreCase);
    }

    private static void EnsureFaenaAccess(UserAccessContext user, string? faenaCodigo)
    {
        if (!CanAccessFaena(user, faenaCodigo))
        {
            throw new UnauthorizedAccessException("El usuario no tiene acceso a la faena solicitada.");
        }
    }

    private static IReadOnlyCollection<SuggestedSparePart> ParseSuggestedSpareParts(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        return raw
            .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item =>
            {
                var parts = item.Split(':', StringSplitOptions.TrimEntries);
                var quantity = parts.Length > 1 && decimal.TryParse(parts[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedQuantity)
                    ? parsedQuantity
                    : 1;
                var unit = parts.Length > 2 && !string.IsNullOrWhiteSpace(parts[2]) ? parts[2] : "UN";
                return new SuggestedSparePart(parts[0], quantity, unit);
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Code))
            .ToArray();
    }

    private static PreventiveFrequencyType ResolveFrequencyType(UpsertPreventivePlanRequest request)
    {
        var count = new[] { request.FrecuenciaHoras.HasValue, request.FrecuenciaKm.HasValue, request.FrecuenciaDias.HasValue }.Count(BooleanTrue);
        if (count > 1)
        {
            return PreventiveFrequencyType.Mixta;
        }

        if (request.FrecuenciaHoras.HasValue)
        {
            return PreventiveFrequencyType.Horas;
        }

        if (request.FrecuenciaKm.HasValue)
        {
            return PreventiveFrequencyType.Kilometros;
        }

        return PreventiveFrequencyType.Calendario;
    }

    private static PreventiveFrequencyType ParseFrequency(DataRow row)
    {
        if (Enum.TryParse<PreventiveFrequencyType>(row.GetValue("Frecuencia"), ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        var hasHours = ParseNullableDecimal(row.GetValue("FrecuenciaHoras")).HasValue;
        var hasKm = ParseNullableDecimal(row.GetValue("FrecuenciaKm")).HasValue;
        var hasCalendar = ParseNullableInt(row.GetValue("FrecuenciaDias")).HasValue || ParseDate(row.GetValue("ProximaFecha")).HasValue;
        var count = new[] { hasHours, hasKm, hasCalendar }.Count(BooleanTrue);
        if (count > 1)
        {
            return PreventiveFrequencyType.Mixta;
        }

        if (hasHours)
        {
            return PreventiveFrequencyType.Horas;
        }

        if (hasKm)
        {
            return PreventiveFrequencyType.Kilometros;
        }

        return PreventiveFrequencyType.Calendario;
    }

    private static string BuildEvaluationMessage(
        PreventivePlanResponse plan,
        AssetInfo asset,
        PreventiveStatus status,
        TriggerEvaluation worst,
        DataRow? openWorkOrder)
    {
        if (openWorkOrder is not null)
        {
            return $"Plan {plan.Codigo} para {asset.Codigo} tiene OT abierta {openWorkOrder.GetValue("NumeroOT")}.";
        }

        return status switch
        {
            PreventiveStatus.Vencido => $"Plan {plan.Codigo} vencido para {asset.Codigo}; {worst.Message}",
            PreventiveStatus.EnVentana => $"Plan {plan.Codigo} esta en ventana para {asset.Codigo}; {worst.Message}",
            PreventiveStatus.ProximoAVencer => $"Plan {plan.Codigo} proximo a vencer para {asset.Codigo}; {worst.Message}",
            _ => $"Plan {plan.Codigo} vigente para {asset.Codigo}; {worst.Message}"
        };
    }

    private static int StatusRank(PreventiveStatus status)
    {
        return status switch
        {
            PreventiveStatus.OTGenerada => 5,
            PreventiveStatus.Vencido => 4,
            PreventiveStatus.EnVentana => 3,
            PreventiveStatus.ProximoAVencer => 2,
            PreventiveStatus.Reprogramado => 1,
            PreventiveStatus.Ejecutado => 1,
            _ => 0
        };
    }

    private static bool IsLowerReading(decimal? current, decimal? previous)
    {
        return current.HasValue && previous.HasValue && current.Value < previous.Value;
    }

    private static void AddAnomalyMessage(List<string> messages, string label, decimal? current, decimal? previous, decimal threshold)
    {
        if (current.HasValue && previous.HasValue && current.Value - previous.Value > threshold)
        {
            messages.Add($"Salto anomalo de {label}: {previous.Value.ToString(CultureInfo.InvariantCulture)} a {current.Value.ToString(CultureInfo.InvariantCulture)}.");
        }
    }

    private static DataRow PlanRow(IReadOnlyDictionary<string, string?> values) => Row(PlanColumns, values);

    private static DataRow ReadingRow(IReadOnlyDictionary<string, string?> values) => Row(ReadingColumns, values);

    private static DataRow Row(IEnumerable<string> columns, IReadOnlyDictionary<string, string?> values)
    {
        return new DataRow(columns.ToDictionary(column => column, column => values.TryGetValue(column, out var value) ? value : null, StringComparer.OrdinalIgnoreCase));
    }

    private static Dictionary<string, string?> CopyRow(DataRow row, IEnumerable<string> columns)
    {
        return columns.ToDictionary(column => column, column => row.GetValue(column), StringComparer.OrdinalIgnoreCase);
    }

    private static string Serialize(DataRow row)
    {
        return System.Text.Json.JsonSerializer.Serialize(row.Values);
    }

    private static string NewId(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 13, prefix.Length + 33)].ToUpperInvariant();

    private static void ValidateRequired(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"El campo {fieldName} es obligatorio.");
        }
    }

    private static string? NormalizeText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeCode(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string FormatDate(DateTimeOffset value) => value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    private static string? FormatOptionalDate(DateTimeOffset? value) => value.HasValue ? FormatDate(value.Value) : null;

    private static string FormatNumber(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    private static string? FormatOptionalNumber(decimal? value) => value.HasValue ? FormatNumber(value.Value) : null;

    private static string? FormatOptionalNumber(int? value) => value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : null;

    private static DateTimeOffset? ParseDate(string? value)
    {
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed) ? parsed : null;
    }

    private static decimal ParseDecimal(string? value, decimal fallback = 0)
    {
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    private static decimal? ParseNullableDecimal(string? value)
    {
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static int ParseInt(string? value, int fallback = 0)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    private static int? ParseNullableInt(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static PreventiveStatus ParseStatus(string? value)
    {
        return Enum.TryParse<PreventiveStatus>(value, ignoreCase: true, out var parsed) ? parsed : PreventiveStatus.Vigente;
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

    private static bool Same(string? left, string? right) => string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool HasRole(UserAccessContext user, string role) => user.Roles.Contains(role, StringComparer.OrdinalIgnoreCase);

    private static bool HasPermission(UserAccessContext user, string permission) => user.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

    private static bool BooleanTrue(bool value) => value;

    private static readonly UserAccessContext SystemUser = new(
        "preventive-engine",
        [AuthRoles.Admin],
        [AuthPermissions.Administration, AuthPermissions.ManageAlerts, AuthPermissions.FinalValidateWorkOrders],
        []);

    private static readonly string[] PlanColumns =
    [
        "Codigo",
        "Nombre",
        "ActivoCodigo",
        "FamiliaEquipo",
        "Marca",
        "Modelo",
        "Frecuencia",
        "FrecuenciaHoras",
        "FrecuenciaKm",
        "FrecuenciaDias",
        "ToleranciaHoras",
        "ToleranciaKm",
        "ToleranciaDias",
        "ChecklistCodigo",
        "RepuestosSugeridos",
        "HHEstimadas",
        "FechaInicio",
        "UltimaEjecucionFecha",
        "UltimaEjecucionHoras",
        "UltimaEjecucionKm",
        "ProximaFecha",
        "ProximaHora",
        "ProximoKm",
        "Estado",
        "Activo",
        "ActualizadoEnUtc",
        "ActualizadoPor"
    ];

    private static readonly string[] ReadingColumns =
    [
        "ReadingId",
        "ActivoCodigo",
        "Horometro",
        "Kilometraje",
        "FechaLectura",
        "UsuarioId",
        "Evidencia",
        "EsCorreccion",
        "EsAnomala",
        "MensajeValidacion",
        "MotivoCorreccion",
        "AutorizadoPor",
        "CreadoEnUtc"
    ];

    private sealed record PreventiveData(
        IReadOnlyCollection<DataRow> Plans,
        IReadOnlyCollection<DataRow> Readings,
        IReadOnlyCollection<DataRow> Evaluations,
        IReadOnlyCollection<DataRow> Assets,
        IReadOnlyCollection<DataRow> WorkOrders);

    private sealed record AssetInfo(
        string Codigo,
        string? Nombre,
        string FaenaCodigo,
        string? Familia,
        string? Marca,
        string? Modelo)
    {
        public DataRow ToDataRow()
        {
            return new DataRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Codigo"] = Codigo,
                ["Nombre"] = Nombre,
                ["FaenaCodigo"] = FaenaCodigo,
                ["Familia"] = Familia,
                ["Marca"] = Marca,
                ["Modelo"] = Modelo
            });
        }
    }

    private sealed record TriggerEvaluation(
        PreventiveStatus Status,
        string? Kind,
        decimal? Remaining,
        int? DaysRemaining,
        string Message);

    private sealed record SuggestedSparePart(
        string Code,
        decimal Quantity,
        string Unit);
}
