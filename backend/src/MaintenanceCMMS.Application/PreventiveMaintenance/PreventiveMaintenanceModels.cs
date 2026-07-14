namespace MaintenanceCMMS.Application.PreventiveMaintenance;

public enum PreventiveFrequencyType
{
    Horas = 0,
    Kilometros = 1,
    Calendario = 2,
    Mixta = 3
}

public enum PreventiveStatus
{
    Vigente = 0,
    ProximoAVencer = 1,
    EnVentana = 2,
    Vencido = 3,
    OTGenerada = 4,
    Ejecutado = 5,
    Reprogramado = 6
}

public sealed record PreventivePlanQuery(
    string? FaenaCodigo = null,
    string? ActivoCodigo = null,
    string? FamiliaEquipo = null,
    PreventiveStatus? Estado = null,
    bool IncludeInactive = false);

public sealed record PreventiveReadingQuery(
    string? FaenaCodigo = null,
    string? ActivoCodigo = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null);

public sealed record PreventiveEvaluationQuery(
    string? FaenaCodigo = null,
    string? ActivoCodigo = null,
    DateTimeOffset? EvaluationDate = null,
    bool GenerateWorkOrders = false);

public sealed record UpsertPreventivePlanRequest(
    string Codigo,
    string Nombre,
    string? ActivoCodigo = null,
    string? FamiliaEquipo = null,
    string? Marca = null,
    string? Modelo = null,
    decimal? FrecuenciaHoras = null,
    decimal? FrecuenciaKm = null,
    int? FrecuenciaDias = null,
    decimal ToleranciaHoras = 0,
    decimal ToleranciaKm = 0,
    int ToleranciaDias = 0,
    string? ChecklistCodigo = null,
    string? RepuestosSugeridos = null,
    decimal HHEstimadas = 1,
    DateTimeOffset? FechaInicio = null,
    bool Activo = true,
    string? Reason = null);

public sealed record RegisterPreventiveReadingRequest(
    string ActivoCodigo,
    decimal Valor,
    DateTimeOffset FechaLectura,
    string? Evidencia = null,
    string Origen = "MANUAL",
    string? Observaciones = null);

public sealed record GeneratePreventiveWorkOrderRequest(
    string? ActivoCodigo = null,
    string? Reason = null,
    bool Force = false);

public sealed record ReprogramPreventivePlanRequest(
    DateTimeOffset? ProximaFecha = null,
    decimal? ProximaHora = null,
    decimal? ProximoKm = null,
    string? Reason = null);

public sealed record PreventivePlanResponse(
    string Codigo,
    string Nombre,
    string? ActivoCodigo,
    string? ActivoNombre,
    string? FaenaCodigo,
    string? FamiliaEquipo,
    string? Marca,
    string? Modelo,
    PreventiveFrequencyType TipoFrecuencia,
    decimal? FrecuenciaHoras,
    decimal? FrecuenciaKm,
    int? FrecuenciaDias,
    decimal ToleranciaHoras,
    decimal ToleranciaKm,
    int ToleranciaDias,
    string? ChecklistCodigo,
    string? RepuestosSugeridos,
    decimal HHEstimadas,
    DateTimeOffset? FechaInicio,
    DateTimeOffset? UltimaEjecucionFecha,
    decimal? UltimaEjecucionHoras,
    decimal? UltimaEjecucionKm,
    DateTimeOffset? ProximaFecha,
    decimal? ProximaHora,
    decimal? ProximoKm,
    PreventiveStatus Estado,
    bool Activo);

public sealed record PreventiveReadingResponse(
    string ReadingId,
    string ActivoCodigo,
    string? ActivoNombre,
    string FaenaCodigo,
    decimal Valor,
    string Unidad,
    DateTimeOffset FechaLectura,
    string UsuarioId,
    string? Evidencia,
    bool EsCorreccion,
    bool EsAnomala,
    string? MensajeValidacion,
    string? AutorizadoPor);

public sealed record PreventiveDueResponse(
    string PlanCodigo,
    string Nombre,
    string ActivoCodigo,
    string? ActivoNombre,
    string FaenaCodigo,
    PreventiveStatus Estado,
    decimal? HorasRestantes,
    decimal? KmRestantes,
    int? DiasRestantes,
    DateTimeOffset? FechaVencimientoEstimada,
    string? NumeroOT,
    string Mensaje);

public sealed record PreventiveCalendarItemResponse(
    string PlanCodigo,
    string Nombre,
    string ActivoCodigo,
    string? ActivoNombre,
    string FaenaCodigo,
    DateTimeOffset Fecha,
    PreventiveStatus Estado,
    string? NumeroOT);

public sealed record PreventiveHistoryResponse(
    string HistoryId,
    string PlanCodigo,
    string ActivoCodigo,
    PreventiveStatus EstadoAnterior,
    PreventiveStatus EstadoNuevo,
    DateTimeOffset FechaUtc,
    string UsuarioId,
    string Motivo,
    string? NumeroOT);

public sealed record PreventiveDashboardResponse(
    IReadOnlyCollection<PreventivePlanResponse> Plans,
    IReadOnlyCollection<PreventiveDueResponse> DueItems,
    IReadOnlyCollection<PreventiveCalendarItemResponse> Calendar,
    IReadOnlyCollection<PreventiveHistoryResponse> History);

public sealed record PreventiveWorkOrderGenerationResponse(
    string PlanCodigo,
    string ActivoCodigo,
    string NumeroOT,
    PreventiveStatus Estado,
    IReadOnlyCollection<string> Warnings);

public sealed record PreventiveEngineRunResponse(
    int Evaluated,
    int GeneratedWorkOrders,
    int AlertsGenerated,
    IReadOnlyCollection<string> Warnings);
