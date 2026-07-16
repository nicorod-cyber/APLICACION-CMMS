using MaintenanceCMMS.Application.Auth;

namespace MaintenanceCMMS.Application.OperationalUnits;

public sealed record OperationalUnitTypeRequest(string Codigo, string Nombre, string? Descripcion = null, bool ParticipaEnDisponibilidad = true);
public sealed record OperationalUnitRoleRequest(string Codigo, string Nombre, string? Descripcion = null);
public sealed record AllowedComponentRequest(string? TipoActivoCodigo = null, string? FamiliaEquipoCodigo = null);
public sealed record OperationalUnitRuleRequest(string TipoUnidadCodigo, string RolComponenteCodigo, int CantidadMinima, int CantidadMaxima, bool Obligatorio, IReadOnlyCollection<AllowedComponentRequest>? Permitidos = null);
public sealed record OperationalUnitRequest(string Codigo, string Nombre, string TipoUnidadCodigo, string? FaenaCodigo, string EstadoOperacionalCodigo, string? Criticidad = null, DateOnly? FechaPuestaServicio = null, DateOnly? FechaBaja = null, string? Observaciones = null);
public sealed record MountOperationalUnitComponentRequest(string ActivoCodigo, string RolComponenteCodigo, string? OrdenTrabajoNumero = null, DateTimeOffset? FechaMontajeUtc = null, string? Observaciones = null);
public sealed record UnmountOperationalUnitComponentRequest(string? OrdenTrabajoNumero = null, DateTimeOffset? FechaDesmontajeUtc = null, string? Observaciones = null);
public sealed record ReplaceOperationalUnitComponentRequest(string ActivoSalienteCodigo, string ActivoEntranteCodigo, string RolComponenteCodigo, string? OrdenTrabajoNumero = null, DateTimeOffset? FechaOperacionUtc = null, string? Observaciones = null);

public sealed record OperationalUnitComponentResponse(string ActivoCodigo, string ActivoNombre, string RolComponenteCodigo, DateTimeOffset FechaMontajeUtc, DateTimeOffset? FechaDesmontajeUtc, string? OrdenTrabajoMontaje, string? OrdenTrabajoDesmontaje, string? Observaciones);
public sealed record OperationalUnitCompositionResponse(bool Completa, IReadOnlyCollection<string> Faltantes, IReadOnlyCollection<OperationalUnitComponentResponse> Vigentes, IReadOnlyCollection<OperationalUnitComponentResponse> Historial);
public sealed record OperationalUnitResponse(string Codigo, string Nombre, string TipoUnidadCodigo, string? FaenaCodigo, string? UbicacionTecnicaCodigo, string EstadoOperacionalCodigo, string? Criticidad, DateOnly? FechaPuestaServicio, DateOnly? FechaBaja, string? Observaciones, OperationalUnitCompositionResponse Composicion);
public sealed record OperationalUnitRuleResponse(string TipoUnidadCodigo, string RolComponenteCodigo, int CantidadMinima, int CantidadMaxima, bool Obligatorio, IReadOnlyCollection<AllowedComponentRequest> Permitidos);

public interface IOperationalUnitService
{
    Task<IReadOnlyCollection<OperationalUnitResponse>> ListAsync(string? faenaCodigo, UserAccessContext user, CancellationToken cancellationToken);
    Task<OperationalUnitResponse?> GetAsync(string codigo, UserAccessContext user, CancellationToken cancellationToken);
    Task<OperationalUnitResponse> CreateAsync(OperationalUnitRequest request, UserAccessContext user, CancellationToken cancellationToken);
    Task<OperationalUnitTypeRequest> CreateTypeAsync(OperationalUnitTypeRequest request, UserAccessContext user, CancellationToken cancellationToken);
    Task<OperationalUnitRoleRequest> CreateRoleAsync(OperationalUnitRoleRequest request, UserAccessContext user, CancellationToken cancellationToken);
    Task<OperationalUnitRuleResponse> UpsertRuleAsync(OperationalUnitRuleRequest request, UserAccessContext user, CancellationToken cancellationToken);
    Task<OperationalUnitCompositionResponse?> MountAsync(string unidadCodigo, MountOperationalUnitComponentRequest request, UserAccessContext user, CancellationToken cancellationToken);
    Task<OperationalUnitCompositionResponse?> UnmountAsync(string unidadCodigo, string activoCodigo, UnmountOperationalUnitComponentRequest request, UserAccessContext user, CancellationToken cancellationToken);
    Task<OperationalUnitCompositionResponse?> ReplaceAsync(string unidadCodigo, ReplaceOperationalUnitComponentRequest request, UserAccessContext user, CancellationToken cancellationToken);
}

