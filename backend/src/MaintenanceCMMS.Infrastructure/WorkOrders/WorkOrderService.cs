using System.Globalization;
using System.Text.Json;
using MaintenanceCMMS.Application.Abstractions.Data;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.WorkOrders;
using MaintenanceCMMS.Domain.Common;

namespace MaintenanceCMMS.Infrastructure.WorkOrders;

public sealed class WorkOrderService : IWorkOrderService
{
    private const string WorkOrdersSchema = "ordenes_trabajo";
    private const string TasksSchema = "tareas_ot";
    private const string TaskTechniciansSchema = "ot_tecnicos_tarea";
    private const string LaborSchema = "ot_hh";
    private const string EvidencesSchema = "ot_evidencias";
    private const string SparePartsSchema = "ot_repuestos";
    private const string ChecklistSchema = "ot_checklists";
    private const string SignaturesSchema = "ot_firmas";
    private const string HistorySchema = "ot_estado_historial";
    private const string AssetsSchema = "activos";
    private const string ChecklistTemplatesSchema = "checklists";

    private readonly IDataProvider _dataProvider;
    private readonly IAuditService _auditService;

    public WorkOrderService(IDataProvider dataProvider, IAuditService auditService)
    {
        _dataProvider = dataProvider;
        _auditService = auditService;
    }

    public async Task<IReadOnlyCollection<WorkOrderSummaryResponse>> ListAsync(
        WorkOrderQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanView(user);
        var data = await ReadDataAsync(cancellationToken);

        return data.Orders
            .Select(row => BuildSummary(row, data))
            .Where(item => query.IncludeClosed || item.Estado is not (WorkOrderLifecycleStatus.ValidadaPlanificacion or WorkOrderLifecycleStatus.Anulada))
            .Where(item => !query.Status.HasValue || item.Estado == query.Status)
            .Where(item => string.IsNullOrWhiteSpace(query.FaenaCodigo) || Same(item.FaenaCodigo, query.FaenaCodigo))
            .Where(item => string.IsNullOrWhiteSpace(query.ActivoCodigo) || Same(item.ActivoCodigo, query.ActivoCodigo))
            .Where(item => string.IsNullOrWhiteSpace(query.TechnicianId) || HasAssignedTechnician(data.Technicians, item.NumeroOT, query.TechnicianId))
            .Where(item => CanViewOrder(user, item, data.Technicians))
            .OrderByDescending(item => item.FechaProgramada ?? item.FechaInicioProgramada ?? DateTimeOffset.MinValue)
            .ThenBy(item => item.NumeroOT, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<WorkOrderDetailResponse?> GetByIdAsync(
        string numeroOt,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanView(user);
        var data = await ReadDataAsync(cancellationToken);
        var row = FindOrder(data.Orders, numeroOt);
        if (row is null)
        {
            return null;
        }

        var detail = BuildDetail(row, data);
        if (!CanViewOrder(user, detail.Summary, data.Technicians))
        {
            throw new UnauthorizedAccessException("No tiene acceso a la OT solicitada.");
        }

        return detail;
    }

    public async Task<WorkOrderDetailResponse> CreateAsync(
        CreateWorkOrderRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanPlan(user);
        ValidateRequired(request.ActivoCodigo, nameof(request.ActivoCodigo));
        ValidateRequired(request.Descripcion, nameof(request.Descripcion));
        ValidateRequired(request.TipoMantenimiento, nameof(request.TipoMantenimiento));

        var asset = await FindAssetAsync(request.ActivoCodigo, cancellationToken);
        if (asset is null)
        {
            throw new DomainException($"El activo '{request.ActivoCodigo}' no existe.");
        }

        var faenaCodigo = ResolveFaena(request.FaenaCodigo, asset);
        EnsureFaenaAccess(user, faenaCodigo);

        var rows = (await _dataProvider.ReadRowsAsync(WorkOrdersSchema, cancellationToken)).ToList();
        var number = NextWorkOrderNumber(rows);
        var now = DateTimeOffset.UtcNow;
        var row = WorkOrderRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["NumeroOT"] = number,
            ["ActivoCodigo"] = NormalizeCode(request.ActivoCodigo),
            ["Estado"] = WorkOrderLifecycleStatus.OTCreada.ToString(),
            ["TipoMantenimiento"] = NormalizeText(request.TipoMantenimiento),
            ["Descripcion"] = NormalizeText(request.Descripcion),
            ["FechaProgramada"] = FormatOptionalDate(request.FechaProgramada),
            ["AvisoId"] = NormalizeCode(request.AvisoId),
            ["FaenaCodigo"] = NormalizeCode(faenaCodigo),
            ["Sistema"] = NormalizeText(request.Sistema),
            ["Subsistema"] = NormalizeText(request.Subsistema),
            ["Componente"] = NormalizeText(request.Componente),
            ["Prioridad"] = NormalizeText(request.Prioridad) ?? "Media",
            ["Criticidad"] = NormalizeText(request.Criticidad) ?? "Media",
            ["EsPreventivaAutomatica"] = "False",
            ["RequiereFirma"] = request.RequiereFirma.ToString(CultureInfo.InvariantCulture),
            ["FechaInicioProgramada"] = FormatOptionalDate(request.FechaInicioProgramada),
            ["FechaFinProgramada"] = FormatOptionalDate(request.FechaFinProgramada),
            ["CreadoPor"] = user.UserId,
            ["CreadoEnUtc"] = FormatDate(now),
            ["ActualizadoPor"] = user.UserId,
            ["ActualizadoEnUtc"] = FormatDate(now)
        });

        rows.Add(row);
        await _dataProvider.SaveRowsAsync(WorkOrdersSchema, rows, cancellationToken);
        await AddHistoryAsync(number, WorkOrderLifecycleStatus.OTCreada, WorkOrderLifecycleStatus.OTCreada, user, "OT creada", cancellationToken);
        await RecordAuditAsync(user, "work_order.created", number, null, row, faenaCodigo, request.Descripcion, cancellationToken);

        return (await GetByIdAsync(number, user, cancellationToken))!;
    }

    public Task<WorkOrderDetailResponse> CreatePreventiveAsync(
        CreatePreventiveWorkOrderRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        return CreatePreventiveInternalAsync(request, user, cancellationToken);
    }

    public async Task<WorkOrderTaskResponse?> AddTaskAsync(
        string numeroOt,
        CreateWorkOrderTaskRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanPlan(user);
        ValidateRequired(request.Descripcion, nameof(request.Descripcion));

        var order = await GetOrderForMutationAsync(numeroOt, user, cancellationToken);
        EnsureOrderOpen(order);

        var rows = (await _dataProvider.ReadRowsAsync(TasksSchema, cancellationToken)).ToList();
        var code = NormalizeCode(request.CodigoTarea) ?? NextTaskCode(rows, numeroOt);
        if (rows.Any(row => Same(row.GetValue("NumeroOT"), numeroOt) && Same(row.GetValue("CodigoTarea"), code)))
        {
            throw new DomainException($"La tarea '{code}' ya existe en la OT.");
        }

        var task = TaskRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["NumeroOT"] = NormalizeCode(numeroOt),
            ["CodigoTarea"] = code,
            ["Descripcion"] = NormalizeText(request.Descripcion),
            ["RequiereEvidencia"] = request.RequiereEvidencia.ToString(CultureInfo.InvariantCulture),
            ["RequiereHH"] = request.RequiereHH.ToString(CultureInfo.InvariantCulture),
            ["FechaInicioProgramada"] = FormatOptionalDate(request.FechaInicioProgramada),
            ["FechaFinProgramada"] = FormatOptionalDate(request.FechaFinProgramada),
            ["ChecklistObligatorio"] = request.ChecklistObligatorio.ToString(CultureInfo.InvariantCulture),
            ["Observaciones"] = NormalizeText(request.Observaciones)
        });

        rows.Add(task);
        await _dataProvider.SaveRowsAsync(TasksSchema, rows, cancellationToken);
        await RecordAuditAsync(user, "work_order.task_created", numeroOt, null, task, order.FaenaCodigo, request.Descripcion, cancellationToken);
        return ToTask(task);
    }

    public async Task<WorkOrderTaskTechnicianResponse?> AssignTechnicianAsync(
        string numeroOt,
        string codigoTarea,
        AssignTaskTechnicianRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanPlan(user);
        ValidateRequired(request.TecnicoUserId, nameof(request.TecnicoUserId));

        var order = await GetOrderForMutationAsync(numeroOt, user, cancellationToken);
        EnsureTaskExists(await _dataProvider.ReadRowsAsync(TasksSchema, cancellationToken), numeroOt, codigoTarea);

        var rows = (await _dataProvider.ReadRowsAsync(TaskTechniciansSchema, cancellationToken)).ToList();
        if (rows.Any(row => Same(row.GetValue("NumeroOT"), numeroOt) &&
                            Same(row.GetValue("CodigoTarea"), codigoTarea) &&
                            Same(row.GetValue("TecnicoUserId"), request.TecnicoUserId)))
        {
            throw new DomainException("El tecnico ya esta asignado a esta tarea.");
        }

        var now = DateTimeOffset.UtcNow;
        var rowToCreate = TaskTechnicianRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["AsignacionId"] = NewId("ASG"),
            ["NumeroOT"] = NormalizeCode(numeroOt),
            ["CodigoTarea"] = NormalizeCode(codigoTarea),
            ["TecnicoUserId"] = NormalizeText(request.TecnicoUserId),
            ["TecnicoNombre"] = NormalizeText(request.TecnicoNombre),
            ["AsignadoEnUtc"] = FormatDate(now),
            ["AsignadoPor"] = user.UserId
        });

        rows.Add(rowToCreate);
        await _dataProvider.SaveRowsAsync(TaskTechniciansSchema, rows, cancellationToken);
        await RecordAuditAsync(user, "work_order.technician_assigned", numeroOt, null, rowToCreate, order.FaenaCodigo, request.TecnicoUserId, cancellationToken);
        return ToTechnician(rowToCreate);
    }

    public async Task<WorkOrderLaborResponse?> RegisterLaborAsync(
        string numeroOt,
        string codigoTarea,
        RegisterLaborRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        ValidateRequired(request.TecnicoUserId, nameof(request.TecnicoUserId));
        ValidateRequired(request.Descripcion, nameof(request.Descripcion));
        var calculatedHours = CalculateLaborHours(request.Horas, request.HoraInicio, request.HoraTermino);
        if (calculatedHours <= 0)
        {
            throw new DomainException("Las HH deben ser mayores a cero.");
        }

        var data = await ReadDataAsync(cancellationToken);
        var order = BuildDetail(FindOrder(data.Orders, numeroOt) ?? throw new DomainException("La OT no existe."), data);
        EnsureFaenaAccess(user, order.Summary.FaenaCodigo);
        EnsureTechnicianCanWork(user, order, codigoTarea, request.TecnicoUserId);
        EnsureTaskExists(data.Tasks, numeroOt, codigoTarea);

        var rows = data.Labor.ToList();
        var rowToCreate = LaborRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["HHId"] = NewId("HH"),
            ["NumeroOT"] = NormalizeCode(numeroOt),
            ["CodigoTarea"] = NormalizeCode(codigoTarea),
            ["TecnicoUserId"] = NormalizeText(request.TecnicoUserId),
            ["Horas"] = FormatNumber(calculatedHours),
            ["Descripcion"] = NormalizeText(request.Descripcion),
            ["FechaTrabajo"] = FormatDate(request.FechaTrabajo ?? request.HoraInicio ?? DateTimeOffset.UtcNow),
            ["HoraInicio"] = FormatOptionalDate(request.HoraInicio),
            ["HoraTermino"] = FormatOptionalDate(request.HoraTermino),
            ["RegistradoPor"] = user.UserId,
            ["Comentario"] = NormalizeText(request.Comentario),
            ["ValidadoSupervisor"] = "false",
            ["ValidadoPor"] = null,
            ["ValidadoEnUtc"] = null
        });

        rows.Add(rowToCreate);
        await _dataProvider.SaveRowsAsync(LaborSchema, rows, cancellationToken);
        await RecordAuditAsync(user, "work_order.labor_registered", numeroOt, null, rowToCreate, order.Summary.FaenaCodigo, request.Descripcion, cancellationToken);
        return ToLabor(rowToCreate);
    }

    public async Task<WorkOrderLaborResponse?> ValidateLaborAsync(
        string numeroOt,
        string hhId,
        ValidateLaborRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        ValidateRequired(request.Reason, nameof(request.Reason));
        EnsureCanPlanOrSupervisor(user);

        var order = await GetOrderForMutationAsync(numeroOt, user, cancellationToken);
        var rows = (await _dataProvider.ReadRowsAsync(LaborSchema, cancellationToken)).ToList();
        var index = rows.FindIndex(row => Same(row.GetValue("NumeroOT"), numeroOt) && Same(row.GetValue("HHId"), hhId));
        if (index < 0)
        {
            return null;
        }

        var previous = rows[index];
        var values = CopyRow(previous, LaborColumns);
        values["ValidadoSupervisor"] = request.Validado.ToString(CultureInfo.InvariantCulture);
        values["ValidadoPor"] = request.Validado ? user.UserId : null;
        values["ValidadoEnUtc"] = request.Validado ? FormatDate(DateTimeOffset.UtcNow) : null;
        values["Comentario"] = Append(values.GetValueOrDefault("Comentario"), request.Reason);
        var updated = LaborRow(values);

        rows[index] = updated;
        await _dataProvider.SaveRowsAsync(LaborSchema, rows, cancellationToken);
        await RecordAuditAsync(user, "work_order.labor_validated", numeroOt, previous, updated, order.FaenaCodigo, request.Reason, cancellationToken);
        return ToLabor(updated);
    }

    public async Task<WorkOrderEvidenceResponse?> RegisterEvidenceAsync(
        string numeroOt,
        RegisterEvidenceRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        ValidateRequired(request.Nombre, nameof(request.Nombre));
        var isCommentOnly = request.TipoEvidencia == WorkOrderEvidenceType.Comentario;
        if (!isCommentOnly &&
            string.IsNullOrWhiteSpace(request.ArchivoKey) &&
            string.IsNullOrWhiteSpace(request.SharePointUrl) &&
            string.IsNullOrWhiteSpace(request.LocalPath))
        {
            throw new DomainException("Debe indicar ArchivoKey, SharePointUrl o LocalPath para la evidencia.");
        }

        var data = await ReadDataAsync(cancellationToken);
        var order = BuildDetail(FindOrder(data.Orders, numeroOt) ?? throw new DomainException("La OT no existe."), data);
        EnsureFaenaAccess(user, order.Summary.FaenaCodigo);
        if (!string.IsNullOrWhiteSpace(request.CodigoTarea))
        {
            EnsureTaskExists(data.Tasks, numeroOt, request.CodigoTarea);
            EnsureTechnicianCanWork(user, order, request.CodigoTarea, user.UserId);
        }
        else
        {
            EnsureCanPlanOrSupervisorOrAssigned(user, order);
        }

        var rows = data.Evidences.ToList();
        var rowToCreate = EvidenceRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["EvidenciaId"] = NewId("EVI"),
            ["NumeroOT"] = NormalizeCode(numeroOt),
            ["CodigoTarea"] = NormalizeCode(request.CodigoTarea),
            ["Nombre"] = NormalizeText(request.Nombre),
            ["ArchivoKey"] = NormalizeText(request.ArchivoKey),
            ["SharePointUrl"] = NormalizeText(request.SharePointUrl),
            ["CubreEvidenciaObligatoria"] = request.CubreEvidenciaObligatoria.ToString(CultureInfo.InvariantCulture),
            ["TipoEvidencia"] = request.TipoEvidencia.ToString(),
            ["EsFoto"] = (request.EsFoto || request.TipoEvidencia is WorkOrderEvidenceType.FotoAntes or WorkOrderEvidenceType.FotoDespues).ToString(CultureInfo.InvariantCulture),
            ["EsObligatoria"] = request.EsObligatoria.ToString(CultureInfo.InvariantCulture),
            ["StorageProvider"] = NormalizeText(request.StorageProvider) ?? InferStorageProvider(request),
            ["LocalPath"] = NormalizeText(request.LocalPath),
            ["OfflineId"] = NormalizeText(request.OfflineId),
            ["SyncStatus"] = NormalizeText(request.SyncStatus) ?? "Synced",
            ["CreadoEnUtc"] = FormatDate(DateTimeOffset.UtcNow),
            ["CreadoPor"] = user.UserId,
            ["Observaciones"] = NormalizeText(request.Observaciones)
        });

        rows.Add(rowToCreate);
        await _dataProvider.SaveRowsAsync(EvidencesSchema, rows, cancellationToken);
        await RecordAuditAsync(user, "work_order.evidence_registered", numeroOt, null, rowToCreate, order.Summary.FaenaCodigo, request.Nombre, cancellationToken);
        return ToEvidence(rowToCreate);
    }

    public async Task<WorkOrderSparePartResponse?> AddSparePartAsync(
        string numeroOt,
        AddWorkOrderSparePartRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanPlan(user);
        ValidateRequired(request.CodigoTarea, nameof(request.CodigoTarea));
        ValidateRequired(request.RepuestoCodigo, nameof(request.RepuestoCodigo));
        ValidateRequired(request.Unidad, nameof(request.Unidad));
        if (request.Cantidad <= 0)
        {
            throw new DomainException("La cantidad de repuesto debe ser mayor a cero.");
        }

        var order = await GetOrderForMutationAsync(numeroOt, user, cancellationToken);
        EnsureTaskExists(await _dataProvider.ReadRowsAsync(TasksSchema, cancellationToken), numeroOt, request.CodigoTarea);

        var rows = (await _dataProvider.ReadRowsAsync(SparePartsSchema, cancellationToken)).ToList();
        var rowToCreate = SparePartRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["ItemId"] = NewId("REPOT"),
            ["NumeroOT"] = NormalizeCode(numeroOt),
            ["CodigoTarea"] = NormalizeCode(request.CodigoTarea),
            ["RepuestoCodigo"] = NormalizeCode(request.RepuestoCodigo),
            ["Cantidad"] = FormatNumber(request.Cantidad),
            ["Unidad"] = NormalizeText(request.Unidad),
            ["BodegaCodigo"] = NormalizeCode(request.BodegaCodigo),
            ["Estado"] = request.Estado.ToString(),
            ["CantidadUtilizada"] = "0",
            ["CantidadDevuelta"] = "0",
            ["Observaciones"] = NormalizeText(request.Observaciones)
        });

        rows.Add(rowToCreate);
        await _dataProvider.SaveRowsAsync(SparePartsSchema, rows, cancellationToken);
        await RecordAuditAsync(user, "work_order.spare_part_added", numeroOt, null, rowToCreate, order.FaenaCodigo, request.RepuestoCodigo, cancellationToken);
        return ToSparePart(rowToCreate);
    }

    public async Task<WorkOrderSparePartResponse?> UpdateSparePartUsageAsync(
        string numeroOt,
        string itemId,
        UpdateWorkOrderSparePartUsageRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        ValidateRequired(request.Reason, nameof(request.Reason));
        var order = await GetOrderForMutationAsync(numeroOt, user, cancellationToken);
        EnsureCanPlanOrSupervisor(user);

        var rows = (await _dataProvider.ReadRowsAsync(SparePartsSchema, cancellationToken)).ToList();
        var index = rows.FindIndex(row => Same(row.GetValue("NumeroOT"), numeroOt) && Same(row.GetValue("ItemId"), itemId));
        if (index < 0)
        {
            return null;
        }

        var previous = rows[index];
        var values = CopyRow(previous, SparePartColumns);
        values["Estado"] = request.Estado.ToString();
        values["CantidadUtilizada"] = FormatNumber(request.CantidadUtilizada ?? ParseDecimal(values.GetValueOrDefault("CantidadUtilizada")));
        values["CantidadDevuelta"] = FormatNumber(request.CantidadDevuelta ?? ParseDecimal(values.GetValueOrDefault("CantidadDevuelta")));
        values["Observaciones"] = Append(values.GetValueOrDefault("Observaciones"), request.Reason);
        var updated = SparePartRow(values);
        rows[index] = updated;
        await _dataProvider.SaveRowsAsync(SparePartsSchema, rows, cancellationToken);
        await RecordAuditAsync(user, "work_order.spare_part_updated", numeroOt, previous, updated, order.FaenaCodigo, request.Reason, cancellationToken);
        return ToSparePart(updated);
    }

    public async Task<WorkOrderChecklistItemResponse?> AddChecklistItemAsync(
        string numeroOt,
        AddWorkOrderChecklistItemRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanPlan(user);
        ValidateRequired(request.CodigoTarea, nameof(request.CodigoTarea));
        ValidateRequired(request.Item, nameof(request.Item));

        var order = await GetOrderForMutationAsync(numeroOt, user, cancellationToken);
        EnsureTaskExists(await _dataProvider.ReadRowsAsync(TasksSchema, cancellationToken), numeroOt, request.CodigoTarea);

        var rows = (await _dataProvider.ReadRowsAsync(ChecklistSchema, cancellationToken)).ToList();
        var rowToCreate = ChecklistRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["ItemId"] = NewId("CHKOT"),
            ["NumeroOT"] = NormalizeCode(numeroOt),
            ["CodigoTarea"] = NormalizeCode(request.CodigoTarea),
            ["Item"] = NormalizeText(request.Item),
            ["Obligatorio"] = request.Obligatorio.ToString(CultureInfo.InvariantCulture),
            ["Completado"] = request.Completado.ToString(CultureInfo.InvariantCulture),
            ["CompletadoEnUtc"] = request.Completado ? FormatDate(DateTimeOffset.UtcNow) : null,
            ["CompletadoPor"] = request.Completado ? user.UserId : null,
            ["TemplateCode"] = NormalizeCode(request.TemplateCode),
            ["TipoRespuesta"] = request.TipoRespuesta.ToString(),
            ["Respuesta"] = request.Completado ? DefaultChecklistResponse(request.TipoRespuesta) : null,
            ["ValorNumerico"] = null,
            ["Texto"] = null,
            ["EvidenciaId"] = null,
            ["RequiereFoto"] = request.RequiereFoto.ToString(CultureInfo.InvariantCulture),
            ["RequiereArchivo"] = request.RequiereArchivo.ToString(CultureInfo.InvariantCulture),
            ["RequiereFirma"] = request.RequiereFirma.ToString(CultureInfo.InvariantCulture),
            ["FirmaId"] = null
        });

        rows.Add(rowToCreate);
        await _dataProvider.SaveRowsAsync(ChecklistSchema, rows, cancellationToken);
        await RecordAuditAsync(user, "work_order.checklist_added", numeroOt, null, rowToCreate, order.FaenaCodigo, request.Item, cancellationToken);
        return ToChecklist(rowToCreate);
    }

    public async Task<WorkOrderChecklistItemResponse?> UpdateChecklistItemAsync(
        string numeroOt,
        string itemId,
        UpdateChecklistItemRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        ValidateRequired(request.Reason, nameof(request.Reason));
        var data = await ReadDataAsync(cancellationToken);
        var order = BuildDetail(FindOrder(data.Orders, numeroOt) ?? throw new DomainException("La OT no existe."), data);
        EnsureCanPlanOrSupervisorOrAssigned(user, order);

        var rows = data.Checklist.ToList();
        var index = rows.FindIndex(row => Same(row.GetValue("NumeroOT"), numeroOt) && Same(row.GetValue("ItemId"), itemId));
        if (index < 0)
        {
            return null;
        }

        var previous = rows[index];
        var values = CopyRow(previous, ChecklistColumns);
        ValidateChecklistCompletion(values, request);
        values["Completado"] = request.Completado.ToString(CultureInfo.InvariantCulture);
        values["CompletadoEnUtc"] = request.Completado ? FormatDate(DateTimeOffset.UtcNow) : null;
        values["CompletadoPor"] = request.Completado ? user.UserId : null;
        var responseType = ParseEnum(values.GetValueOrDefault("TipoRespuesta"), WorkOrderChecklistResponseType.CumpleNoCumpleNoAplica);
        values["Respuesta"] = NormalizeText(request.Respuesta) ?? values.GetValueOrDefault("Respuesta") ?? DefaultChecklistResponse(responseType);
        values["ValorNumerico"] = request.ValorNumerico.HasValue ? FormatNumber(request.ValorNumerico.Value) : values.GetValueOrDefault("ValorNumerico");
        values["Texto"] = NormalizeText(request.Texto) ?? values.GetValueOrDefault("Texto");
        values["EvidenciaId"] = NormalizeText(request.EvidenciaId) ?? values.GetValueOrDefault("EvidenciaId");
        values["FirmaId"] = NormalizeText(request.FirmaId) ?? values.GetValueOrDefault("FirmaId");
        var updated = ChecklistRow(values);
        rows[index] = updated;
        await _dataProvider.SaveRowsAsync(ChecklistSchema, rows, cancellationToken);
        await RecordAuditAsync(user, "work_order.checklist_updated", numeroOt, previous, updated, order.Summary.FaenaCodigo, request.Reason, cancellationToken);
        return ToChecklist(updated);
    }

    public async Task<IReadOnlyCollection<WorkOrderChecklistItemResponse>> ApplyChecklistTemplateAsync(
        string numeroOt,
        ApplyChecklistTemplateRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanPlan(user);
        ValidateRequired(request.CodigoTarea, nameof(request.CodigoTarea));
        ValidateRequired(request.TemplateCode, nameof(request.TemplateCode));

        var order = await GetOrderForMutationAsync(numeroOt, user, cancellationToken);
        EnsureTaskExists(await _dataProvider.ReadRowsAsync(TasksSchema, cancellationToken), numeroOt, request.CodigoTarea);

        var templates = await _dataProvider.ReadRowsAsync(ChecklistTemplatesSchema, cancellationToken);
        var template = templates.FirstOrDefault(row => Same(row.GetValue("Codigo"), request.TemplateCode));
        if (template is null)
        {
            throw new DomainException("La plantilla de checklist no existe.");
        }

        var templateItems = ParseTemplateItems(template.GetValue("Items"));
        if (templateItems.Count == 0)
        {
            throw new DomainException("La plantilla de checklist no tiene items configurados.");
        }

        var rows = (await _dataProvider.ReadRowsAsync(ChecklistSchema, cancellationToken)).ToList();
        var created = new List<WorkOrderChecklistItemResponse>();
        foreach (var item in templateItems)
        {
            var rowToCreate = ChecklistRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["ItemId"] = NewId("CHKOT"),
                ["NumeroOT"] = NormalizeCode(numeroOt),
                ["CodigoTarea"] = NormalizeCode(request.CodigoTarea),
                ["Item"] = NormalizeText(item.Item),
                ["Obligatorio"] = item.Obligatorio.ToString(CultureInfo.InvariantCulture),
                ["Completado"] = "false",
                ["CompletadoEnUtc"] = null,
                ["CompletadoPor"] = null,
                ["TemplateCode"] = NormalizeCode(request.TemplateCode),
                ["TipoRespuesta"] = item.TipoRespuesta.ToString(),
                ["Respuesta"] = null,
                ["ValorNumerico"] = null,
                ["Texto"] = null,
                ["EvidenciaId"] = null,
                ["RequiereFoto"] = item.RequiereFoto.ToString(CultureInfo.InvariantCulture),
                ["RequiereArchivo"] = item.RequiereArchivo.ToString(CultureInfo.InvariantCulture),
                ["RequiereFirma"] = item.RequiereFirma.ToString(CultureInfo.InvariantCulture),
                ["FirmaId"] = null
            });
            rows.Add(rowToCreate);
            created.Add(ToChecklist(rowToCreate));
        }

        await _dataProvider.SaveRowsAsync(ChecklistSchema, rows, cancellationToken);
        await RecordAuditAsync(user, "work_order.checklist_template_applied", numeroOt, null, ChecklistRow(CopyRow(rows.Last(), ChecklistColumns)), order.FaenaCodigo, request.TemplateCode, cancellationToken);
        return created;
    }

    public async Task<WorkOrderSignatureResponse?> RegisterSignatureAsync(
        string numeroOt,
        RegisterWorkOrderSignatureRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SignatureFileKey) && string.IsNullOrWhiteSpace(request.SignatureImageDataUrl))
        {
            throw new DomainException("Debe registrar archivo o imagen de firma.");
        }
        var data = await ReadDataAsync(cancellationToken);
        var order = BuildDetail(FindOrder(data.Orders, numeroOt) ?? throw new DomainException("La OT no existe."), data);
        EnsureCanPlanOrSupervisorOrAssigned(user, order);
        if (!string.IsNullOrWhiteSpace(request.CodigoTarea))
        {
            EnsureTaskExists(data.Tasks, numeroOt, request.CodigoTarea);
            EnsureTechnicianCanWork(user, order, request.CodigoTarea, request.UsuarioId ?? user.UserId);
        }

        var rows = data.Signatures.ToList();
        var rowToCreate = SignatureRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["FirmaId"] = NewId("FIRMA"),
            ["NumeroOT"] = NormalizeCode(numeroOt),
            ["CodigoTarea"] = NormalizeCode(request.CodigoTarea),
            ["Scope"] = NormalizeText(request.Scope) ?? (string.IsNullOrWhiteSpace(request.CodigoTarea) ? "OT" : "Tarea"),
            ["UsuarioId"] = NormalizeText(request.UsuarioId) ?? user.UserId,
            ["SignatureFileKey"] = NormalizeText(request.SignatureFileKey),
            ["SignatureImageDataUrl"] = NormalizeText(request.SignatureImageDataUrl),
            ["FirmadoEnUtc"] = FormatDate(DateTimeOffset.UtcNow),
            ["Comentario"] = NormalizeText(request.Comentario)
        });

        rows.Add(rowToCreate);
        await _dataProvider.SaveRowsAsync(SignaturesSchema, rows, cancellationToken);
        await RecordAuditAsync(user, "work_order.signature_registered", numeroOt, null, rowToCreate, order.Summary.FaenaCodigo, request.Comentario, cancellationToken);
        return ToSignature(rowToCreate);
    }

    public Task<WorkOrderDetailResponse?> ScheduleAsync(
        string numeroOt,
        ScheduleWorkOrderRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanPlan(user);
        ValidateRequired(request.Reason, nameof(request.Reason));
        return ChangeStatusAsync(numeroOt, WorkOrderLifecycleStatus.Programada, user, request.Reason, cancellationToken, values =>
        {
            values["FechaInicioProgramada"] = FormatDate(request.FechaInicioProgramada);
            values["FechaFinProgramada"] = FormatOptionalDate(request.FechaFinProgramada);
            values["FechaProgramada"] = FormatDate(request.FechaInicioProgramada);
        }, WorkOrderLifecycleStatus.OTCreada, WorkOrderLifecycleStatus.EnPlanificacion, WorkOrderLifecycleStatus.Programada);
    }

    public Task<WorkOrderDetailResponse?> StartAsync(
        string numeroOt,
        WorkOrderActionRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        ValidateRequired(request.Reason, nameof(request.Reason));
        return ChangeStatusAsync(numeroOt, WorkOrderLifecycleStatus.EnEjecucion, user, request.Reason, cancellationToken, values =>
        {
            values["FechaInicioRealUtc"] ??= FormatDate(DateTimeOffset.UtcNow);
        }, WorkOrderLifecycleStatus.OTCreada, WorkOrderLifecycleStatus.EnPlanificacion, WorkOrderLifecycleStatus.Programada, WorkOrderLifecycleStatus.PendienteRepuestos, WorkOrderLifecycleStatus.PendienteDocumentacion, WorkOrderLifecycleStatus.Pausada);
    }

    public Task<WorkOrderDetailResponse?> PauseAsync(
        string numeroOt,
        WorkOrderActionRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        ValidateRequired(request.Reason, nameof(request.Reason));
        return ChangeStatusAsync(numeroOt, WorkOrderLifecycleStatus.Pausada, user, request.Reason, cancellationToken, null, WorkOrderLifecycleStatus.EnEjecucion);
    }

    public Task<WorkOrderDetailResponse?> FinishByTechnicianAsync(
        string numeroOt,
        WorkOrderActionRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        ValidateRequired(request.Reason, nameof(request.Reason));
        return ChangeStatusAsync(numeroOt, WorkOrderLifecycleStatus.FinalizadaTecnico, user, request.Reason, cancellationToken, values =>
        {
            values["FechaFinalizacionTecnicoUtc"] = FormatDate(DateTimeOffset.UtcNow);
            values["FinalizadoPor"] = user.UserId;
        }, WorkOrderLifecycleStatus.EnEjecucion, WorkOrderLifecycleStatus.Pausada, WorkOrderLifecycleStatus.Programada);
    }

    public async Task<WorkOrderDetailResponse?> CloseTechnicallyAsync(
        string numeroOt,
        WorkOrderActionRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanClose(user);
        ValidateRequired(request.Reason, nameof(request.Reason));

        var data = await ReadDataAsync(cancellationToken);
        var row = FindOrder(data.Orders, numeroOt);
        if (row is null)
        {
            return null;
        }

        var detail = BuildDetail(row, data);
        EnsureFaenaAccess(user, detail.Summary.FaenaCodigo);
        EnsureStatus(detail.Summary.Estado, WorkOrderLifecycleStatus.FinalizadaTecnico, WorkOrderLifecycleStatus.EnRevisionSupervisor);
        if (detail.ClosureBlockers.Count > 0)
        {
            throw new DomainException($"No se puede cerrar la OT: {string.Join(" | ", detail.ClosureBlockers.Select(item => item.Message))}");
        }

        return await ChangeStatusAsync(numeroOt, WorkOrderLifecycleStatus.CerradaTecnicamente, user, request.Reason, cancellationToken, values =>
        {
            values["FechaCierreSupervisorUtc"] = FormatDate(DateTimeOffset.UtcNow);
            values["CerradoPor"] = user.UserId;
        }, WorkOrderLifecycleStatus.FinalizadaTecnico, WorkOrderLifecycleStatus.EnRevisionSupervisor);
    }

    public Task<WorkOrderDetailResponse?> ValidatePlanningAsync(
        string numeroOt,
        WorkOrderActionRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanValidatePlanning(user);
        ValidateRequired(request.Reason, nameof(request.Reason));
        return ChangeStatusAsync(numeroOt, WorkOrderLifecycleStatus.ValidadaPlanificacion, user, request.Reason, cancellationToken, values =>
        {
            values["FechaValidacionPlanificacionUtc"] = FormatDate(DateTimeOffset.UtcNow);
            values["ValidadoPor"] = user.UserId;
        }, WorkOrderLifecycleStatus.CerradaTecnicamente);
    }

    public Task<WorkOrderDetailResponse?> AnnulAsync(
        string numeroOt,
        WorkOrderActionRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanPlanOrSupervisor(user);
        ValidateRequired(request.Reason, nameof(request.Reason));
        return ChangeStatusAsync(numeroOt, WorkOrderLifecycleStatus.Anulada, user, request.Reason, cancellationToken, values =>
        {
            values["AnuladoPor"] = user.UserId;
            values["FechaAnulacionUtc"] = FormatDate(DateTimeOffset.UtcNow);
            values["MotivoAnulacion"] = request.Reason;
        });
    }

    private async Task<WorkOrderDetailResponse> CreatePreventiveInternalAsync(
        CreatePreventiveWorkOrderRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        var created = await CreateAsync(new CreateWorkOrderRequest(
            request.ActivoCodigo,
            request.Descripcion,
            "Preventive",
            request.FaenaCodigo,
            Sistema: request.Sistema,
            Subsistema: request.Subsistema,
            Componente: request.Componente,
            Prioridad: "Media",
            Criticidad: "Media",
            FechaProgramada: request.FechaProgramada,
            FechaInicioProgramada: request.FechaInicioProgramada,
            FechaFinProgramada: request.FechaFinProgramada,
            RequiereFirma: request.RequiereFirma),
            user,
            cancellationToken);

        var rows = (await _dataProvider.ReadRowsAsync(WorkOrdersSchema, cancellationToken)).ToList();
        var index = rows.FindIndex(row => Same(row.GetValue("NumeroOT"), created.Summary.NumeroOT));
        var values = CopyRow(rows[index], WorkOrderColumns);
        values["EsPreventivaAutomatica"] = "True";
        values["PlanPreventivoCodigo"] = NormalizeCode(request.PlanPreventivoCodigo);
        var updated = WorkOrderRow(values);
        rows[index] = updated;
        await _dataProvider.SaveRowsAsync(WorkOrdersSchema, rows, cancellationToken);
        return (await GetByIdAsync(created.Summary.NumeroOT, user, cancellationToken))!;
    }

    private async Task<WorkOrderDetailResponse?> ChangeStatusAsync(
        string numeroOt,
        WorkOrderLifecycleStatus newStatus,
        UserAccessContext user,
        string reason,
        CancellationToken cancellationToken,
        Action<Dictionary<string, string?>>? mutate = null,
        params WorkOrderLifecycleStatus[] allowedCurrentStatuses)
    {
        var rows = (await _dataProvider.ReadRowsAsync(WorkOrdersSchema, cancellationToken)).ToList();
        var index = rows.FindIndex(row => Same(row.GetValue("NumeroOT"), numeroOt));
        if (index < 0)
        {
            return null;
        }

        var previous = rows[index];
        var currentStatus = ParseStatus(previous.GetValue("Estado"));
        if (allowedCurrentStatuses.Length > 0)
        {
            EnsureStatus(currentStatus, allowedCurrentStatuses);
        }

        var faenaCodigo = previous.GetValue("FaenaCodigo");
        EnsureFaenaAccess(user, faenaCodigo);

        if (newStatus is WorkOrderLifecycleStatus.EnEjecucion or WorkOrderLifecycleStatus.Pausada or WorkOrderLifecycleStatus.FinalizadaTecnico)
        {
            var detail = await GetByIdAsync(numeroOt, user, cancellationToken) ?? throw new DomainException("La OT no existe.");
            EnsureCanPlanOrSupervisorOrAssigned(user, detail);
        }

        var values = CopyRow(previous, WorkOrderColumns);
        values["Estado"] = newStatus.ToString();
        values["ActualizadoPor"] = user.UserId;
        values["ActualizadoEnUtc"] = FormatDate(DateTimeOffset.UtcNow);
        mutate?.Invoke(values);
        var updated = WorkOrderRow(values);
        rows[index] = updated;

        await _dataProvider.SaveRowsAsync(WorkOrdersSchema, rows, cancellationToken);
        await AddHistoryAsync(numeroOt, currentStatus, newStatus, user, reason, cancellationToken);
        await RecordAuditAsync(user, "work_order.status_changed", numeroOt, previous, updated, faenaCodigo, reason, cancellationToken, AuditSeverity.High);
        return await GetByIdAsync(numeroOt, user, cancellationToken);
    }

    private async Task<WorkOrderSummaryResponse> GetOrderForMutationAsync(
        string numeroOt,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        var detail = await GetByIdAsync(numeroOt, user, cancellationToken);
        return detail?.Summary ?? throw new DomainException("La OT no existe.");
    }

    private async Task<WorkOrderData> ReadDataAsync(CancellationToken cancellationToken)
    {
        return new WorkOrderData(
            await _dataProvider.ReadRowsAsync(WorkOrdersSchema, cancellationToken),
            await _dataProvider.ReadRowsAsync(TasksSchema, cancellationToken),
            await _dataProvider.ReadRowsAsync(TaskTechniciansSchema, cancellationToken),
            await _dataProvider.ReadRowsAsync(LaborSchema, cancellationToken),
            await _dataProvider.ReadRowsAsync(EvidencesSchema, cancellationToken),
            await _dataProvider.ReadRowsAsync(SparePartsSchema, cancellationToken),
            await _dataProvider.ReadRowsAsync(ChecklistSchema, cancellationToken),
            await _dataProvider.ReadRowsAsync(SignaturesSchema, cancellationToken),
            await _dataProvider.ReadRowsAsync(HistorySchema, cancellationToken),
            await _dataProvider.ReadRowsAsync(AssetsSchema, cancellationToken));
    }

    private WorkOrderDetailResponse BuildDetail(DataRow order, WorkOrderData data)
    {
        var summary = BuildSummary(order, data);
        var tasks = data.Tasks.Where(row => Same(row.GetValue("NumeroOT"), summary.NumeroOT)).Select(ToTask).OrderBy(item => item.CodigoTarea, StringComparer.OrdinalIgnoreCase).ToArray();
        var technicians = data.Technicians.Where(row => Same(row.GetValue("NumeroOT"), summary.NumeroOT)).Select(ToTechnician).ToArray();
        var labor = data.Labor.Where(row => Same(row.GetValue("NumeroOT"), summary.NumeroOT)).Select(ToLabor).ToArray();
        var evidences = data.Evidences.Where(row => Same(row.GetValue("NumeroOT"), summary.NumeroOT)).Select(ToEvidence).ToArray();
        var spareParts = data.SpareParts.Where(row => Same(row.GetValue("NumeroOT"), summary.NumeroOT)).Select(ToSparePart).ToArray();
        var checklist = data.Checklist.Where(row => Same(row.GetValue("NumeroOT"), summary.NumeroOT)).Select(ToChecklist).ToArray();
        var signatures = data.Signatures.Where(row => Same(row.GetValue("NumeroOT"), summary.NumeroOT)).Select(ToSignature).ToArray();
        var history = data.History.Where(row => Same(row.GetValue("NumeroOT"), summary.NumeroOT)).Select(ToHistory).OrderBy(item => item.FechaUtc).ToArray();
        var blockers = BuildClosureBlockers(summary, tasks, labor, evidences, spareParts, checklist, signatures);

        return new WorkOrderDetailResponse(summary with { BloqueosCierre = blockers.Count }, tasks, technicians, labor, evidences, spareParts, checklist, signatures, history, blockers);
    }

    private WorkOrderSummaryResponse BuildSummary(DataRow row, WorkOrderData data)
    {
        var numeroOt = row.GetValue("NumeroOT") ?? string.Empty;
        var tasks = data.Tasks.Where(item => Same(item.GetValue("NumeroOT"), numeroOt)).ToArray();
        var technicians = data.Technicians.Where(item => Same(item.GetValue("NumeroOT"), numeroOt)).ToArray();
        var labor = data.Labor.Where(item => Same(item.GetValue("NumeroOT"), numeroOt)).ToArray();
        var detailTasks = tasks.Select(ToTask).ToArray();
        var blockers = BuildClosureBlockers(
            ToSummaryWithoutBlockers(row, data),
            detailTasks,
            labor.Select(ToLabor).ToArray(),
            data.Evidences.Where(item => Same(item.GetValue("NumeroOT"), numeroOt)).Select(ToEvidence).ToArray(),
            data.SpareParts.Where(item => Same(item.GetValue("NumeroOT"), numeroOt)).Select(ToSparePart).ToArray(),
            data.Checklist.Where(item => Same(item.GetValue("NumeroOT"), numeroOt)).Select(ToChecklist).ToArray(),
            data.Signatures.Where(item => Same(item.GetValue("NumeroOT"), numeroOt)).Select(ToSignature).ToArray());

        var summary = ToSummaryWithoutBlockers(row, data);
        return summary with
        {
            TareasTotal = tasks.Length,
            TecnicosTotal = technicians.Select(item => item.GetValue("TecnicoUserId")).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            HorasRegistradas = labor.Sum(item => ParseDecimal(item.GetValue("Horas"))),
            BloqueosCierre = blockers.Count
        };
    }

    private WorkOrderSummaryResponse ToSummaryWithoutBlockers(DataRow row, WorkOrderData data)
    {
        var assetCode = row.GetValue("ActivoCodigo") ?? string.Empty;
        var asset = data.Assets.FirstOrDefault(item => Same(item.GetValue("Codigo"), assetCode));
        return new WorkOrderSummaryResponse(
            row.GetValue("NumeroOT") ?? string.Empty,
            ParseStatus(row.GetValue("Estado")),
            assetCode,
            EmptyToNull(asset?.GetValue("Nombre")),
            row.GetValue("FaenaCodigo") ?? asset?.GetValue("FaenaCodigo") ?? string.Empty,
            row.GetValue("TipoMantenimiento") ?? string.Empty,
            row.GetValue("Descripcion") ?? string.Empty,
            EmptyToNull(row.GetValue("AvisoId")),
            EmptyToNull(row.GetValue("Sistema")),
            EmptyToNull(row.GetValue("Subsistema")),
            EmptyToNull(row.GetValue("Componente")),
            row.GetValue("Prioridad") ?? "Media",
            row.GetValue("Criticidad") ?? "Media",
            ParseDate(row.GetValue("FechaProgramada")),
            ParseDate(row.GetValue("FechaInicioProgramada")),
            ParseDate(row.GetValue("FechaFinProgramada")),
            ParseBool(row.GetValue("EsPreventivaAutomatica")),
            ParseBool(row.GetValue("RequiereFirma")),
            0,
            0,
            0,
            0);
    }

    private static IReadOnlyCollection<WorkOrderClosureBlocker> BuildClosureBlockers(
        WorkOrderSummaryResponse summary,
        IReadOnlyCollection<WorkOrderTaskResponse> tasks,
        IReadOnlyCollection<WorkOrderLaborResponse> labor,
        IReadOnlyCollection<WorkOrderEvidenceResponse> evidences,
        IReadOnlyCollection<WorkOrderSparePartResponse> spareParts,
        IReadOnlyCollection<WorkOrderChecklistItemResponse> checklist,
        IReadOnlyCollection<WorkOrderSignatureResponse> signatures)
    {
        var blockers = new List<WorkOrderClosureBlocker>();

        foreach (var task in tasks.Where(item => item.RequiereEvidencia))
        {
            if (!evidences.Any(item => Same(item.CodigoTarea, task.CodigoTarea) && item.CubreEvidenciaObligatoria))
            {
                blockers.Add(new WorkOrderClosureBlocker("missing_evidence", $"La tarea {task.CodigoTarea} requiere evidencia."));
            }
        }

        foreach (var task in tasks.Where(item => item.RequiereHH))
        {
            var taskLabor = labor.Where(item => Same(item.CodigoTarea, task.CodigoTarea)).ToArray();
            if (taskLabor.Sum(item => item.Horas) <= 0)
            {
                blockers.Add(new WorkOrderClosureBlocker("missing_labor", $"La tarea {task.CodigoTarea} requiere HH."));
            }
            else if (!taskLabor.Any(item => item.ValidadoSupervisor))
            {
                blockers.Add(new WorkOrderClosureBlocker("labor_not_validated", $"La tarea {task.CodigoTarea} tiene HH sin validacion de supervisor."));
            }
        }

        foreach (var item in checklist.Where(item => item.Obligatorio && !item.Completado))
        {
            blockers.Add(new WorkOrderClosureBlocker("incomplete_checklist", $"Checklist obligatorio pendiente: {item.Item}."));
        }

        foreach (var item in checklist.Where(item => item.Completado))
        {
            if ((item.RequiereFoto || item.RequiereArchivo) && string.IsNullOrWhiteSpace(item.EvidenciaId))
            {
                blockers.Add(new WorkOrderClosureBlocker("missing_checklist_evidence", $"Checklist {item.Item} requiere evidencia asociada."));
            }

            if (item.RequiereFirma && string.IsNullOrWhiteSpace(item.FirmaId))
            {
                blockers.Add(new WorkOrderClosureBlocker("missing_checklist_signature", $"Checklist {item.Item} requiere firma asociada."));
            }
        }

        foreach (var item in spareParts.Where(item => item.Estado == WorkOrderSparePartStatus.Entregado))
        {
            blockers.Add(new WorkOrderClosureBlocker("unresolved_spare_part", $"Repuesto entregado pendiente de uso/devolucion: {item.RepuestoCodigo}."));
        }

        if (summary.Estado is not WorkOrderLifecycleStatus.ValidadaPlanificacion &&
            summary.Estado is not WorkOrderLifecycleStatus.Anulada &&
            summary.Estado is not WorkOrderLifecycleStatus.CerradaTecnicamente &&
            summary.RequiereFirma &&
            signatures.Count == 0)
        {
            blockers.Add(new WorkOrderClosureBlocker("missing_signature", "La OT requiere firma antes del cierre tecnico."));
        }

        return blockers;
    }

    private async Task AddHistoryAsync(
        string numeroOt,
        WorkOrderLifecycleStatus previousStatus,
        WorkOrderLifecycleStatus nextStatus,
        UserAccessContext user,
        string reason,
        CancellationToken cancellationToken)
    {
        var rows = (await _dataProvider.ReadRowsAsync(HistorySchema, cancellationToken)).ToList();
        rows.Add(HistoryRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["HistorialId"] = NewId("HISTOT"),
            ["NumeroOT"] = NormalizeCode(numeroOt),
            ["EstadoAnterior"] = previousStatus.ToString(),
            ["EstadoNuevo"] = nextStatus.ToString(),
            ["FechaUtc"] = FormatDate(DateTimeOffset.UtcNow),
            ["UsuarioId"] = user.UserId,
            ["Motivo"] = reason
        }));
        await _dataProvider.SaveRowsAsync(HistorySchema, rows, cancellationToken);
    }

    private async Task<DataRow?> FindAssetAsync(string assetCode, CancellationToken cancellationToken)
    {
        return (await _dataProvider.ReadRowsAsync(AssetsSchema, cancellationToken))
            .FirstOrDefault(row => Same(row.GetValue("Codigo"), assetCode));
    }

    private static DataRow? FindOrder(IReadOnlyCollection<DataRow> rows, string numeroOt)
    {
        return rows.FirstOrDefault(row => Same(row.GetValue("NumeroOT"), numeroOt));
    }

    private static void EnsureTaskExists(IReadOnlyCollection<DataRow> tasks, string numeroOt, string codigoTarea)
    {
        if (!tasks.Any(row => Same(row.GetValue("NumeroOT"), numeroOt) && Same(row.GetValue("CodigoTarea"), codigoTarea)))
        {
            throw new DomainException($"La tarea '{codigoTarea}' no existe en la OT.");
        }
    }

    private static string ResolveFaena(string? requestedFaena, DataRow asset)
    {
        var assetFaena = asset.GetValue("FaenaCodigo");
        if (!string.IsNullOrWhiteSpace(requestedFaena) && !Same(requestedFaena, assetFaena))
        {
            throw new DomainException("El activo seleccionado no pertenece a la faena indicada.");
        }

        return NormalizeCode(requestedFaena) ?? NormalizeCode(assetFaena) ?? string.Empty;
    }

    private static bool CanViewOrder(UserAccessContext user, WorkOrderSummaryResponse order, IReadOnlyCollection<DataRow> assignments)
    {
        if (HasAnyRole(user, AuthRoles.Admin, AuthRoles.Management))
        {
            return true;
        }

        if (HasAnyRole(user, AuthRoles.Technician) && !HasAnyRole(user, AuthRoles.Planner, AuthRoles.MaintenanceSupervisor))
        {
            return HasAssignedTechnician(assignments, order.NumeroOT, user.UserId);
        }

        return CanAccessFaena(user, order.FaenaCodigo);
    }

    private static void EnsureCanView(UserAccessContext user)
    {
        if (HasAnyRole(user, AuthRoles.Admin, AuthRoles.Planner, AuthRoles.MaintenanceSupervisor, AuthRoles.Technician, AuthRoles.Management, AuthRoles.FaenaViewer))
        {
            return;
        }

        throw new UnauthorizedAccessException("No tiene permisos para ver OT.");
    }

    private static void EnsureCanPlan(UserAccessContext user)
    {
        if (HasAnyRole(user, AuthRoles.Admin, AuthRoles.Planner, AuthRoles.MaintenanceSupervisor))
        {
            return;
        }

        throw new UnauthorizedAccessException("La planificacion de OT requiere planificador o supervisor.");
    }

    private static void EnsureCanPlanOrSupervisor(UserAccessContext user)
    {
        if (HasAnyRole(user, AuthRoles.Admin, AuthRoles.Planner, AuthRoles.MaintenanceSupervisor))
        {
            return;
        }

        throw new UnauthorizedAccessException("La accion requiere planificador o supervisor.");
    }

    private static void EnsureCanClose(UserAccessContext user)
    {
        if (HasAnyRole(user, AuthRoles.Admin, AuthRoles.MaintenanceSupervisor) ||
            user.Permissions.Contains(AuthPermissions.CloseWorkOrders, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        throw new UnauthorizedAccessException("El cierre tecnico requiere supervisor de mantenimiento.");
    }

    private static void EnsureCanValidatePlanning(UserAccessContext user)
    {
        if (HasAnyRole(user, AuthRoles.Admin, AuthRoles.Planner) ||
            user.Permissions.Contains(AuthPermissions.FinalValidateWorkOrders, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        throw new UnauthorizedAccessException("La validacion final requiere planificacion.");
    }

    private static void EnsureCanPlanOrSupervisorOrAssigned(UserAccessContext user, WorkOrderDetailResponse order)
    {
        if (HasAnyRole(user, AuthRoles.Admin, AuthRoles.Planner, AuthRoles.MaintenanceSupervisor))
        {
            return;
        }

        if (HasAnyRole(user, AuthRoles.Technician) &&
            order.Technicians.Any(item => Same(item.TecnicoUserId, user.UserId)))
        {
            return;
        }

        throw new UnauthorizedAccessException("La accion requiere estar asignado a la OT.");
    }

    private static void EnsureTechnicianCanWork(UserAccessContext user, WorkOrderDetailResponse order, string taskCode, string technicianId)
    {
        if (HasAnyRole(user, AuthRoles.Admin, AuthRoles.Planner, AuthRoles.MaintenanceSupervisor))
        {
            return;
        }

        if (!Same(user.UserId, technicianId))
        {
            throw new UnauthorizedAccessException("El tecnico solo puede registrar su propio trabajo.");
        }

        if (!order.Technicians.Any(item => Same(item.CodigoTarea, taskCode) && Same(item.TecnicoUserId, user.UserId)))
        {
            throw new UnauthorizedAccessException("El tecnico no esta asignado a esta tarea.");
        }
    }

    private static void EnsureFaenaAccess(UserAccessContext user, string? faenaCodigo)
    {
        if (!CanAccessFaena(user, faenaCodigo))
        {
            throw new UnauthorizedAccessException("No tiene acceso a la faena de la OT.");
        }
    }

    private static bool CanAccessFaena(UserAccessContext user, string? faenaCodigo)
    {
        return string.IsNullOrWhiteSpace(faenaCodigo)
            || HasAnyRole(user, AuthRoles.Admin, AuthRoles.Management)
            || (HasAnyRole(user, AuthRoles.Planner) && user.Faenas.Count == 0)
            || user.Faenas.Contains(faenaCodigo, StringComparer.OrdinalIgnoreCase);
    }

    private static bool HasAssignedTechnician(IReadOnlyCollection<DataRow> assignments, string numeroOt, string? technicianId)
    {
        return !string.IsNullOrWhiteSpace(technicianId) &&
               assignments.Any(row => Same(row.GetValue("NumeroOT"), numeroOt) && Same(row.GetValue("TecnicoUserId"), technicianId));
    }

    private static bool HasAnyRole(UserAccessContext user, params string[] roles)
    {
        return roles.Any(role => user.Roles.Contains(role, StringComparer.OrdinalIgnoreCase));
    }

    private static void EnsureOrderOpen(WorkOrderSummaryResponse order)
    {
        if (order.Estado is WorkOrderLifecycleStatus.ValidadaPlanificacion or WorkOrderLifecycleStatus.Anulada)
        {
            throw new DomainException("La OT ya no admite modificaciones.");
        }
    }

    private static void EnsureStatus(WorkOrderLifecycleStatus current, params WorkOrderLifecycleStatus[] expected)
    {
        if (!expected.Contains(current))
        {
            throw new DomainException($"La OT esta en estado {current} y no admite esta accion.");
        }
    }

    private async Task RecordAuditAsync(
        UserAccessContext user,
        string action,
        string entityId,
        DataRow? previous,
        DataRow? updated,
        string? faenaCodigo,
        string? reason,
        CancellationToken cancellationToken,
        AuditSeverity severity = AuditSeverity.Medium)
    {
        await _auditService.RecordAsync(new AuditEventRequest(
            user.UserId,
            action,
            AuditModules.WorkOrders,
            "WorkOrder",
            entityId,
            previous is null ? null : Serialize(previous),
            updated is null ? null : Serialize(updated),
            faenaCodigo,
            severity,
            reason),
            cancellationToken);
    }

    private static WorkOrderTaskResponse ToTask(DataRow row)
    {
        return new WorkOrderTaskResponse(
            row.GetValue("NumeroOT") ?? string.Empty,
            row.GetValue("CodigoTarea") ?? string.Empty,
            row.GetValue("Descripcion") ?? string.Empty,
            ParseDate(row.GetValue("FechaInicioProgramada")),
            ParseDate(row.GetValue("FechaFinProgramada")),
            ParseBool(row.GetValue("RequiereEvidencia")),
            ParseBool(row.GetValue("RequiereHH"), true),
            ParseBool(row.GetValue("ChecklistObligatorio")),
            EmptyToNull(row.GetValue("Observaciones")));
    }

    private static WorkOrderTaskTechnicianResponse ToTechnician(DataRow row)
    {
        return new WorkOrderTaskTechnicianResponse(
            row.GetValue("AsignacionId") ?? string.Empty,
            row.GetValue("NumeroOT") ?? string.Empty,
            row.GetValue("CodigoTarea") ?? string.Empty,
            row.GetValue("TecnicoUserId") ?? string.Empty,
            EmptyToNull(row.GetValue("TecnicoNombre")),
            ParseDate(row.GetValue("AsignadoEnUtc")) ?? DateTimeOffset.MinValue,
            row.GetValue("AsignadoPor") ?? string.Empty);
    }

    private static WorkOrderLaborResponse ToLabor(DataRow row)
    {
        return new WorkOrderLaborResponse(
            row.GetValue("HHId") ?? string.Empty,
            row.GetValue("NumeroOT") ?? string.Empty,
            row.GetValue("CodigoTarea") ?? string.Empty,
            row.GetValue("TecnicoUserId") ?? string.Empty,
            ParseDecimal(row.GetValue("Horas")),
            row.GetValue("Descripcion") ?? string.Empty,
            ParseDate(row.GetValue("FechaTrabajo")) ?? DateTimeOffset.MinValue,
            row.GetValue("RegistradoPor") ?? string.Empty,
            ParseDate(row.GetValue("HoraInicio")),
            ParseDate(row.GetValue("HoraTermino")),
            EmptyToNull(row.GetValue("Comentario")),
            ParseBool(row.GetValue("ValidadoSupervisor")),
            EmptyToNull(row.GetValue("ValidadoPor")),
            ParseDate(row.GetValue("ValidadoEnUtc")));
    }

    private static WorkOrderEvidenceResponse ToEvidence(DataRow row)
    {
        return new WorkOrderEvidenceResponse(
            row.GetValue("EvidenciaId") ?? string.Empty,
            row.GetValue("NumeroOT") ?? string.Empty,
            EmptyToNull(row.GetValue("CodigoTarea")),
            row.GetValue("Nombre") ?? string.Empty,
            EmptyToNull(row.GetValue("ArchivoKey")),
            EmptyToNull(row.GetValue("SharePointUrl")),
            ParseBool(row.GetValue("CubreEvidenciaObligatoria"), true),
            ParseDate(row.GetValue("CreadoEnUtc")) ?? DateTimeOffset.MinValue,
            row.GetValue("CreadoPor") ?? string.Empty,
            EmptyToNull(row.GetValue("Observaciones")),
            ParseEnum(row.GetValue("TipoEvidencia"), WorkOrderEvidenceType.Archivo),
            ParseBool(row.GetValue("EsFoto")),
            ParseBool(row.GetValue("EsObligatoria")),
            EmptyToNull(row.GetValue("StorageProvider")),
            EmptyToNull(row.GetValue("LocalPath")),
            EmptyToNull(row.GetValue("OfflineId")),
            EmptyToNull(row.GetValue("SyncStatus")));
    }

    private static WorkOrderSparePartResponse ToSparePart(DataRow row)
    {
        return new WorkOrderSparePartResponse(
            row.GetValue("ItemId") ?? string.Empty,
            row.GetValue("NumeroOT") ?? string.Empty,
            row.GetValue("CodigoTarea") ?? string.Empty,
            row.GetValue("RepuestoCodigo") ?? string.Empty,
            ParseDecimal(row.GetValue("Cantidad")),
            row.GetValue("Unidad") ?? string.Empty,
            EmptyToNull(row.GetValue("BodegaCodigo")),
            ParseEnum(row.GetValue("Estado"), WorkOrderSparePartStatus.Solicitado),
            ParseDecimal(row.GetValue("CantidadUtilizada")),
            ParseDecimal(row.GetValue("CantidadDevuelta")),
            EmptyToNull(row.GetValue("Observaciones")));
    }

    private static WorkOrderChecklistItemResponse ToChecklist(DataRow row)
    {
        return new WorkOrderChecklistItemResponse(
            row.GetValue("ItemId") ?? string.Empty,
            row.GetValue("NumeroOT") ?? string.Empty,
            row.GetValue("CodigoTarea") ?? string.Empty,
            row.GetValue("Item") ?? string.Empty,
            ParseBool(row.GetValue("Obligatorio"), true),
            ParseBool(row.GetValue("Completado")),
            ParseDate(row.GetValue("CompletadoEnUtc")),
            EmptyToNull(row.GetValue("CompletadoPor")),
            EmptyToNull(row.GetValue("TemplateCode")),
            ParseEnum(row.GetValue("TipoRespuesta"), WorkOrderChecklistResponseType.CumpleNoCumpleNoAplica),
            EmptyToNull(row.GetValue("Respuesta")),
            ParseNullableDecimal(row.GetValue("ValorNumerico")),
            EmptyToNull(row.GetValue("Texto")),
            EmptyToNull(row.GetValue("EvidenciaId")),
            ParseBool(row.GetValue("RequiereFoto")),
            ParseBool(row.GetValue("RequiereArchivo")),
            ParseBool(row.GetValue("RequiereFirma")),
            EmptyToNull(row.GetValue("FirmaId")));
    }

    private static WorkOrderSignatureResponse ToSignature(DataRow row)
    {
        return new WorkOrderSignatureResponse(
            row.GetValue("FirmaId") ?? string.Empty,
            row.GetValue("NumeroOT") ?? string.Empty,
            row.GetValue("UsuarioId") ?? string.Empty,
            EmptyToNull(row.GetValue("SignatureFileKey")),
            ParseDate(row.GetValue("FirmadoEnUtc")) ?? DateTimeOffset.MinValue,
            EmptyToNull(row.GetValue("Comentario")),
            EmptyToNull(row.GetValue("CodigoTarea")),
            EmptyToNull(row.GetValue("Scope")) ?? "OT",
            EmptyToNull(row.GetValue("SignatureImageDataUrl")));
    }

    private static WorkOrderStatusHistoryResponse ToHistory(DataRow row)
    {
        return new WorkOrderStatusHistoryResponse(
            row.GetValue("HistorialId") ?? string.Empty,
            row.GetValue("NumeroOT") ?? string.Empty,
            ParseStatus(row.GetValue("EstadoAnterior")),
            ParseStatus(row.GetValue("EstadoNuevo")),
            ParseDate(row.GetValue("FechaUtc")) ?? DateTimeOffset.MinValue,
            row.GetValue("UsuarioId") ?? string.Empty,
            row.GetValue("Motivo") ?? string.Empty);
    }

    private static WorkOrderLifecycleStatus ParseStatus(string? value)
    {
        if (Enum.TryParse<WorkOrderLifecycleStatus>(value, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return value?.Trim().ToLowerInvariant() switch
        {
            "draft" => WorkOrderLifecycleStatus.OTCreada,
            "planned" or "assigned" => WorkOrderLifecycleStatus.Programada,
            "inprogress" => WorkOrderLifecycleStatus.EnEjecucion,
            "pendingevidence" or "pendinglabor" => WorkOrderLifecycleStatus.EnRevisionSupervisor,
            "closed" => WorkOrderLifecycleStatus.CerradaTecnicamente,
            "cancelled" => WorkOrderLifecycleStatus.Anulada,
            _ => WorkOrderLifecycleStatus.OTCreada
        };
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback)
        where TEnum : struct
    {
        return Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : fallback;
    }

    private static DataRow WorkOrderRow(IReadOnlyDictionary<string, string?> values) => Row(WorkOrderColumns, values);
    private static DataRow TaskRow(IReadOnlyDictionary<string, string?> values) => Row(TaskColumns, values);
    private static DataRow TaskTechnicianRow(IReadOnlyDictionary<string, string?> values) => Row(TaskTechnicianColumns, values);
    private static DataRow LaborRow(IReadOnlyDictionary<string, string?> values) => Row(LaborColumns, values);
    private static DataRow EvidenceRow(IReadOnlyDictionary<string, string?> values) => Row(EvidenceColumns, values);
    private static DataRow SparePartRow(IReadOnlyDictionary<string, string?> values) => Row(SparePartColumns, values);
    private static DataRow ChecklistRow(IReadOnlyDictionary<string, string?> values) => Row(ChecklistColumns, values);
    private static DataRow SignatureRow(IReadOnlyDictionary<string, string?> values) => Row(SignatureColumns, values);
    private static DataRow HistoryRow(IReadOnlyDictionary<string, string?> values) => Row(HistoryColumns, values);

    private static DataRow Row(IEnumerable<string> columns, IReadOnlyDictionary<string, string?> values)
    {
        return new DataRow(columns.ToDictionary(column => column, column => values.TryGetValue(column, out var value) ? value : null, StringComparer.OrdinalIgnoreCase));
    }

    private static Dictionary<string, string?> CopyRow(DataRow row, IEnumerable<string> columns)
    {
        return columns.ToDictionary(column => column, column => row.GetValue(column), StringComparer.OrdinalIgnoreCase);
    }

    private static string NextWorkOrderNumber(IReadOnlyCollection<DataRow> rows)
    {
        var next = rows
            .Select(row => row.GetValue("NumeroOT"))
            .Select(value => ParseNumberSuffix(value, "OT-"))
            .DefaultIfEmpty(0)
            .Max() + 1;
        return $"OT-{next:000000}";
    }

    private static string NextTaskCode(IReadOnlyCollection<DataRow> rows, string numeroOt)
    {
        var next = rows
            .Where(row => Same(row.GetValue("NumeroOT"), numeroOt))
            .Select(row => row.GetValue("CodigoTarea"))
            .Select(value => ParseNumberSuffix(value, "T-"))
            .DefaultIfEmpty(0)
            .Max() + 1;
        return $"T-{next:000}";
    }

    private static int ParseNumberSuffix(string? value, string prefix)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        var normalized = value.Replace(prefix, string.Empty, StringComparison.OrdinalIgnoreCase);
        return int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
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

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string FormatDate(DateTimeOffset value) => value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    private static string? FormatOptionalDate(DateTimeOffset? value) => value.HasValue ? FormatDate(value.Value) : null;

    private static string FormatNumber(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    private static DateTimeOffset? ParseDate(string? value)
    {
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed) ? parsed : null;
    }

    private static decimal ParseDecimal(string? value)
    {
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }

    private static decimal? ParseNullableDecimal(string? value)
    {
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static bool ParseBool(string? value, bool fallback = false)
    {
        return bool.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static bool Same(string? left, string? right) => string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string? Append(string? existing, string next)
    {
        return string.IsNullOrWhiteSpace(existing) ? next : $"{existing} | {next}";
    }

    private static string Serialize(DataRow row) => JsonSerializer.Serialize(row.Values);

    private static decimal CalculateLaborHours(decimal? requestedHours, DateTimeOffset? start, DateTimeOffset? end)
    {
        if (start.HasValue || end.HasValue)
        {
            if (!start.HasValue || !end.HasValue)
            {
                throw new DomainException("Debe indicar hora inicio y hora termino para calcular HH.");
            }

            if (end.Value <= start.Value)
            {
                throw new DomainException("La hora termino debe ser posterior a la hora inicio.");
            }

            return Math.Round((decimal)(end.Value - start.Value).TotalHours, 2);
        }

        return requestedHours ?? 0;
    }

    private static string InferStorageProvider(RegisterEvidenceRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.SharePointUrl))
        {
            return "SharePoint";
        }

        if (!string.IsNullOrWhiteSpace(request.LocalPath))
        {
            return "LocalSimulation";
        }

        return "ManualLink";
    }

    private static string? DefaultChecklistResponse(WorkOrderChecklistResponseType responseType)
    {
        return responseType switch
        {
            WorkOrderChecklistResponseType.CumpleNoCumpleNoAplica => "Cumple",
            WorkOrderChecklistResponseType.BuenoRegularMalo => "Bueno",
            WorkOrderChecklistResponseType.SiNo => "Si",
            _ => null
        };
    }

    private static void ValidateChecklistCompletion(Dictionary<string, string?> values, UpdateChecklistItemRequest request)
    {
        if (!request.Completado)
        {
            return;
        }

        var responseType = ParseEnum(values.GetValueOrDefault("TipoRespuesta"), WorkOrderChecklistResponseType.CumpleNoCumpleNoAplica);
        var response = NormalizeText(request.Respuesta) ?? NormalizeText(values.GetValueOrDefault("Respuesta")) ?? DefaultChecklistResponse(responseType);
        var text = NormalizeText(request.Texto) ?? NormalizeText(values.GetValueOrDefault("Texto"));
        var evidenceId = NormalizeText(request.EvidenciaId) ?? NormalizeText(values.GetValueOrDefault("EvidenciaId"));
        var signatureId = NormalizeText(request.FirmaId) ?? NormalizeText(values.GetValueOrDefault("FirmaId"));

        if (responseType is WorkOrderChecklistResponseType.CumpleNoCumpleNoAplica or WorkOrderChecklistResponseType.BuenoRegularMalo or WorkOrderChecklistResponseType.SiNo &&
            string.IsNullOrWhiteSpace(response))
        {
            throw new DomainException("Debe registrar una respuesta de checklist.");
        }

        if (responseType == WorkOrderChecklistResponseType.Numerico &&
            !request.ValorNumerico.HasValue &&
            string.IsNullOrWhiteSpace(values.GetValueOrDefault("ValorNumerico")))
        {
            throw new DomainException("Debe registrar un valor numerico en el checklist.");
        }

        if (responseType == WorkOrderChecklistResponseType.Texto && string.IsNullOrWhiteSpace(text))
        {
            throw new DomainException("Debe registrar texto en el checklist.");
        }

        if ((responseType == WorkOrderChecklistResponseType.FotoObligatoria || ParseBool(values.GetValueOrDefault("RequiereFoto"))) &&
            string.IsNullOrWhiteSpace(evidenceId))
        {
            throw new DomainException("Debe asociar evidencia fotografica al checklist.");
        }

        if ((responseType == WorkOrderChecklistResponseType.Archivo || ParseBool(values.GetValueOrDefault("RequiereArchivo"))) &&
            string.IsNullOrWhiteSpace(evidenceId))
        {
            throw new DomainException("Debe asociar archivo al checklist.");
        }

        if ((responseType == WorkOrderChecklistResponseType.Firma || ParseBool(values.GetValueOrDefault("RequiereFirma"))) &&
            string.IsNullOrWhiteSpace(signatureId))
        {
            throw new DomainException("Debe asociar firma al checklist.");
        }
    }

    private static IReadOnlyCollection<ChecklistTemplateItem> ParseTemplateItems(string? rawItems)
    {
        if (string.IsNullOrWhiteSpace(rawItems))
        {
            return [];
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<List<ChecklistTemplateItem>>(rawItems, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (parsed is not null && parsed.Count > 0)
            {
                return parsed.Where(item => !string.IsNullOrWhiteSpace(item.Item)).ToArray();
            }
        }
        catch (JsonException)
        {
            // Non-JSON templates are supported as one item per line or semicolon.
        }

        return rawItems
            .Split(["\r\n", "\n", ";"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => new ChecklistTemplateItem { Item = item })
            .ToArray();
    }

    private sealed record WorkOrderData(
        IReadOnlyList<DataRow> Orders,
        IReadOnlyList<DataRow> Tasks,
        IReadOnlyList<DataRow> Technicians,
        IReadOnlyList<DataRow> Labor,
        IReadOnlyList<DataRow> Evidences,
        IReadOnlyList<DataRow> SpareParts,
        IReadOnlyList<DataRow> Checklist,
        IReadOnlyList<DataRow> Signatures,
        IReadOnlyList<DataRow> History,
        IReadOnlyList<DataRow> Assets);

    private sealed class ChecklistTemplateItem
    {
        public string Item { get; init; } = string.Empty;

        public WorkOrderChecklistResponseType TipoRespuesta { get; init; } = WorkOrderChecklistResponseType.CumpleNoCumpleNoAplica;

        public bool Obligatorio { get; init; } = true;

        public bool RequiereFoto { get; init; }

        public bool RequiereArchivo { get; init; }

        public bool RequiereFirma { get; init; }
    }

    private static readonly string[] WorkOrderColumns =
    [
        "NumeroOT",
        "ActivoCodigo",
        "Estado",
        "TipoMantenimiento",
        "Descripcion",
        "FechaProgramada",
        "AvisoId",
        "FaenaCodigo",
        "Sistema",
        "Subsistema",
        "Componente",
        "Prioridad",
        "Criticidad",
        "ClasificacionFalla",
        "PlanPreventivoCodigo",
        "EsPreventivaAutomatica",
        "RequiereFirma",
        "FechaInicioProgramada",
        "FechaFinProgramada",
        "CreadoPor",
        "CreadoEnUtc",
        "FechaInicioRealUtc",
        "FechaFinalizacionTecnicoUtc",
        "FinalizadoPor",
        "FechaCierreSupervisorUtc",
        "CerradoPor",
        "FechaValidacionPlanificacionUtc",
        "ValidadoPor",
        "AnuladoPor",
        "FechaAnulacionUtc",
        "MotivoAnulacion",
        "ActualizadoPor",
        "ActualizadoEnUtc"
    ];

    private static readonly string[] TaskColumns =
    [
        "NumeroOT",
        "CodigoTarea",
        "Descripcion",
        "RequiereEvidencia",
        "RequiereHH",
        "FechaInicioProgramada",
        "FechaFinProgramada",
        "ChecklistObligatorio",
        "Observaciones"
    ];

    private static readonly string[] TaskTechnicianColumns =
    [
        "AsignacionId",
        "NumeroOT",
        "CodigoTarea",
        "TecnicoUserId",
        "TecnicoNombre",
        "AsignadoEnUtc",
        "AsignadoPor"
    ];

    private static readonly string[] LaborColumns =
    [
        "HHId",
        "NumeroOT",
        "CodigoTarea",
        "TecnicoUserId",
        "Horas",
        "Descripcion",
        "FechaTrabajo",
        "HoraInicio",
        "HoraTermino",
        "RegistradoPor",
        "Comentario",
        "ValidadoSupervisor",
        "ValidadoPor",
        "ValidadoEnUtc"
    ];

    private static readonly string[] EvidenceColumns =
    [
        "EvidenciaId",
        "NumeroOT",
        "CodigoTarea",
        "Nombre",
        "ArchivoKey",
        "SharePointUrl",
        "CubreEvidenciaObligatoria",
        "TipoEvidencia",
        "EsFoto",
        "EsObligatoria",
        "StorageProvider",
        "LocalPath",
        "OfflineId",
        "SyncStatus",
        "CreadoEnUtc",
        "CreadoPor",
        "Observaciones"
    ];

    private static readonly string[] SparePartColumns =
    [
        "ItemId",
        "NumeroOT",
        "CodigoTarea",
        "RepuestoCodigo",
        "Cantidad",
        "Unidad",
        "BodegaCodigo",
        "Estado",
        "CantidadUtilizada",
        "CantidadDevuelta",
        "Observaciones"
    ];

    private static readonly string[] ChecklistColumns =
    [
        "ItemId",
        "NumeroOT",
        "CodigoTarea",
        "Item",
        "Obligatorio",
        "Completado",
        "CompletadoEnUtc",
        "CompletadoPor",
        "TemplateCode",
        "TipoRespuesta",
        "Respuesta",
        "ValorNumerico",
        "Texto",
        "EvidenciaId",
        "RequiereFoto",
        "RequiereArchivo",
        "RequiereFirma",
        "FirmaId"
    ];

    private static readonly string[] SignatureColumns =
    [
        "FirmaId",
        "NumeroOT",
        "CodigoTarea",
        "Scope",
        "UsuarioId",
        "SignatureFileKey",
        "SignatureImageDataUrl",
        "FirmadoEnUtc",
        "Comentario"
    ];

    private static readonly string[] HistoryColumns =
    [
        "HistorialId",
        "NumeroOT",
        "EstadoAnterior",
        "EstadoNuevo",
        "FechaUtc",
        "UsuarioId",
        "Motivo"
    ];
}
