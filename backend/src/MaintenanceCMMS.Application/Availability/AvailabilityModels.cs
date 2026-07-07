namespace MaintenanceCMMS.Application.Availability;

public enum AvailabilityPeriod
{
    Dia = 0,
    Semana = 1,
    Mes = 2,
    Acumulado = 3
}

public enum ContractAssetRole
{
    Comprometido = 0,
    Backup = 1,
    Arriendo = 2,
    Asignado = 3
}

public enum AvailabilityCause
{
    MantenimientoCorrectivo = 0,
    MantenimientoPreventivo = 1,
    Repuestos = 2,
    DocumentacionVencida = 3,
    TrasladoMantenimiento = 4,
    ServicioExterno = 5,
    PruebaLiberacionTecnica = 6,
    PendienteDiagnostico = 7,
    FallaRepetitiva = 8,
    OperacionalExternaNoAtribuible = 9
}

public sealed record AvailabilityQuery(
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    string? FaenaCodigo = null,
    string? ContractCode = null,
    string? Cliente = null,
    AvailabilityPeriod Period = AvailabilityPeriod.Mes);

public sealed record AvailabilityContractQuery(
    string? FaenaCodigo = null,
    string? Cliente = null,
    bool IncludeInactive = false);

public sealed record AvailabilityEventQuery(
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    string? FaenaCodigo = null,
    string? ContractCode = null,
    string? ActivoCodigo = null,
    AvailabilityCause? Cause = null);

public sealed record UpsertAvailabilityContractRequest(
    string ContractCode,
    string Nombre,
    string Cliente,
    string FaenaCodigo,
    decimal HorasComprometidasDia = 24,
    decimal DisponibilidadObjetivo = 0.9m,
    DateTimeOffset? FechaInicio = null,
    DateTimeOffset? FechaFin = null,
    string? ReglasCliente = null,
    bool Activo = true,
    string? Reason = null);

public sealed record AssignContractAssetRequest(
    string ContractCode,
    string ActivoCodigo,
    ContractAssetRole Rol,
    DateTimeOffset? FechaInicio = null,
    DateTimeOffset? FechaFin = null,
    bool Activo = true,
    string? Reason = null);

public sealed record RegisterAvailabilityEventRequest(
    string ContractCode,
    string ActivoCodigo,
    AvailabilityCause Causa,
    DateTimeOffset InicioUtc,
    DateTimeOffset? FinUtc,
    bool PuedeUtilizarse,
    bool AtribuibleMantenimiento = true,
    string? NumeroOT = null,
    string? Comentario = null);

public sealed record AvailabilityContractResponse(
    string ContractCode,
    string Nombre,
    string Cliente,
    string FaenaCodigo,
    decimal HorasComprometidasDia,
    decimal DisponibilidadObjetivo,
    DateTimeOffset? FechaInicio,
    DateTimeOffset? FechaFin,
    string? ReglasCliente,
    bool Activo,
    IReadOnlyCollection<AvailabilityContractAssetResponse> Assets);

public sealed record AvailabilityContractAssetResponse(
    string AssignmentId,
    string ContractCode,
    string ActivoCodigo,
    string? ActivoNombre,
    string FaenaCodigo,
    ContractAssetRole Rol,
    DateTimeOffset? FechaInicio,
    DateTimeOffset? FechaFin,
    bool Activo);

public sealed record AvailabilityEventResponse(
    string EventId,
    string ContractCode,
    string ActivoCodigo,
    string? ActivoNombre,
    string FaenaCodigo,
    AvailabilityCause Causa,
    DateTimeOffset InicioUtc,
    DateTimeOffset? FinUtc,
    bool PuedeUtilizarse,
    bool AtribuibleMantenimiento,
    bool PenalizaDisponibilidad,
    string? NumeroOT,
    string? Comentario,
    string UsuarioId,
    DateTimeOffset CreatedAtUtc);

public sealed record AvailabilityDashboardResponse(
    AvailabilityKpiResponse Kpi,
    IReadOnlyCollection<AvailabilityContractSummary> ByContract,
    IReadOnlyCollection<AvailabilityFaenaSummary> ByFaena,
    IReadOnlyCollection<AvailabilityCauseSummary> ByCause,
    IReadOnlyCollection<UnavailableAssetResponse> UnavailableAssets,
    IReadOnlyCollection<AvailabilityTrendPoint> Trends,
    IReadOnlyCollection<AvailabilityEventResponse> Events);

public sealed record AvailabilityKpiResponse(
    int EquiposComprometidos,
    int EquiposCubiertos,
    int EquiposNoDisponibles,
    decimal HorasComprometidas,
    decimal HorasDisponibles,
    decimal HorasNoDisponiblesPenalizadas,
    decimal DisponibilidadCantidad,
    decimal DisponibilidadHoras,
    decimal DisponibilidadObjetivo,
    bool CumpleObjetivo);

public sealed record AvailabilityContractSummary(
    string ContractCode,
    string Nombre,
    string Cliente,
    string FaenaCodigo,
    int EquiposComprometidos,
    int EquiposCubiertos,
    decimal HorasComprometidas,
    decimal HorasDisponibles,
    decimal DisponibilidadCantidad,
    decimal DisponibilidadHoras,
    decimal DisponibilidadObjetivo,
    bool CumpleObjetivo);

public sealed record AvailabilityFaenaSummary(
    string FaenaCodigo,
    int EquiposComprometidos,
    int EquiposCubiertos,
    decimal DisponibilidadCantidad,
    decimal DisponibilidadHoras);

public sealed record AvailabilityCauseSummary(
    AvailabilityCause Causa,
    decimal HorasNoDisponibles,
    int Eventos,
    bool PenalizaDisponibilidad);

public sealed record UnavailableAssetResponse(
    string ContractCode,
    string ActivoCodigo,
    string? ActivoNombre,
    string FaenaCodigo,
    AvailabilityCause Causa,
    DateTimeOffset InicioUtc,
    DateTimeOffset? FinUtc,
    decimal HorasNoDisponibles,
    bool PenalizaDisponibilidad,
    bool CubiertoPorBackup,
    string? NumeroOT);

public sealed record AvailabilityTrendPoint(
    string PeriodKey,
    DateTimeOffset From,
    DateTimeOffset To,
    decimal DisponibilidadCantidad,
    decimal DisponibilidadHoras,
    int EquiposComprometidos,
    int EquiposCubiertos,
    decimal HorasComprometidas,
    decimal HorasDisponibles);
