namespace MaintenanceCMMS.Application.Scheduling;

public enum ScheduleViewMode
{
    Diario = 0,
    Semanal = 1,
    Mensual = 2
}

public enum ScheduleItemStatus
{
    Programado = 0,
    EnProceso = 1,
    Atrasado = 2,
    EsperandoCupo = 3,
    Completado = 4
}

public enum ScheduleAlertType
{
    TallerSobrecargado = 0,
    OTVencida = 1,
    ProgramacionExcedeCapacidad = 2,
    EquipoEsperandoCupo = 3,
    TrabajoCriticoAtrasado = 4
}

public sealed record ScheduleBoardQuery(
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    string? FaenaCodigo = null,
    string? TallerCodigo = null,
    ScheduleViewMode View = ScheduleViewMode.Semanal,
    bool IncludeClosed = false);

public sealed record UpsertWorkshopRequest(
    string TallerCodigo,
    string Nombre,
    string FaenaCodigo,
    decimal CapacidadDiariaHH,
    int CapacidadEquipos,
    string Horario,
    string Especialidad,
    bool Activo = true,
    string? Reason = null);

public sealed record ScheduleWorkOrderPlanningRequest(
    string TallerCodigo,
    DateTimeOffset FechaInicio,
    DateTimeOffset FechaFin,
    decimal HHEstimadas,
    string Reason,
    string? TecnicoUserId = null,
    bool OverrideCapacity = false);

public sealed record AddScheduleDependencyRequest(
    string PredecessorNumeroOT,
    string SuccessorNumeroOT,
    string Tipo = "FinishToStart",
    string? Motivo = null);

public sealed record ScheduleBoardResponse(
    IReadOnlyCollection<WorkshopResponse> Workshops,
    IReadOnlyCollection<ScheduleItemResponse> Items,
    IReadOnlyCollection<WorkshopLoadResponse> Loads,
    IReadOnlyCollection<KanbanColumnResponse> Kanban,
    IReadOnlyCollection<GanttDependencyResponse> Dependencies,
    IReadOnlyCollection<ScheduleAlertResponse> Alerts);

public sealed record WorkshopResponse(
    string TallerCodigo,
    string Nombre,
    string FaenaCodigo,
    decimal CapacidadDiariaHH,
    int CapacidadEquipos,
    string Horario,
    string Especialidad,
    bool Activo);

public sealed record ScheduleItemResponse(
    string ProgramacionId,
    string NumeroOT,
    string TallerCodigo,
    string TallerNombre,
    string FaenaCodigo,
    string ActivoCodigo,
    string? ActivoNombre,
    string? TecnicoUserId,
    DateTimeOffset FechaInicio,
    DateTimeOffset FechaFin,
    decimal HHEstimadas,
    ScheduleItemStatus Estado,
    string Prioridad,
    string Criticidad,
    string Descripcion);

public sealed record WorkshopLoadResponse(
    string TallerCodigo,
    string TallerNombre,
    DateOnly Fecha,
    decimal CapacidadHH,
    decimal HHProgramadas,
    int CapacidadEquipos,
    int EquiposProgramados,
    bool Sobrecargado);

public sealed record KanbanColumnResponse(
    ScheduleItemStatus Estado,
    IReadOnlyCollection<ScheduleItemResponse> Items);

public sealed record GanttDependencyResponse(
    string DependenciaId,
    string PredecessorNumeroOT,
    string SuccessorNumeroOT,
    string Tipo,
    string? Motivo);

public sealed record ScheduleAlertResponse(
    string AlertId,
    ScheduleAlertType Tipo,
    string Severity,
    string Message,
    string? TallerCodigo,
    string? NumeroOT,
    string? FaenaCodigo,
    DateTimeOffset CreatedAtUtc,
    bool Resolved);

public sealed record ScheduleWorkOrderResponse(
    ScheduleItemResponse Item,
    IReadOnlyCollection<string> Warnings,
    IReadOnlyCollection<ScheduleAlertResponse> Alerts);

public sealed record ScheduleDependencyResponse(
    string DependenciaId,
    string PredecessorNumeroOT,
    string SuccessorNumeroOT,
    string Tipo,
    string? Motivo);
