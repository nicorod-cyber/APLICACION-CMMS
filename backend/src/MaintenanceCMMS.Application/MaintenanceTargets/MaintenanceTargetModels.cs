using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Domain.Common;

namespace MaintenanceCMMS.Application.MaintenanceTargets;

/// <summary>
/// The kind of physical or operational record addressed by a maintenance workflow.
/// This is deliberately an application contract, not a third persisted entity.
/// </summary>
public enum MaintenanceTargetType
{
    Asset = 0,
    OperationalUnit = 1
}

public enum MaintenanceTargetScope
{
    Operational = 0,
    All = 1
}

public sealed record MaintenanceTargetReference(MaintenanceTargetType Tipo, string Codigo);

public sealed record MaintenanceTargetQuery(
    string? FaenaCodigo = null,
    string? Search = null,
    MaintenanceTargetType? Tipo = null,
    MaintenanceTargetScope Scope = MaintenanceTargetScope.Operational,
    bool SoloDisponibilidad = false,
    bool IncluirDadosDeBaja = false);

public sealed record MaintenanceTargetSummary(
    MaintenanceTargetType Tipo,
    string Codigo,
    string Nombre,
    string CategoriaCodigo,
    string CategoriaNombre,
    string? FaenaCodigo,
    string? FaenaNombre,
    string EstadoOperacionalCodigo,
    string EstadoOperacionalNombre,
    string? Criticidad,
    bool EsComposicion,
    bool? ComposicionCompleta,
    bool EsComponenteMontado,
    string? UnidadOperativaVigenteCodigo,
    string? UnidadOperativaVigenteNombre,
    string? RolComponenteVigente,
    bool ParticipaEnDisponibilidad);

public sealed record ResolvedMaintenanceTarget(
    MaintenanceTargetType Tipo,
    string Codigo,
    string Nombre,
    Guid? AssetId,
    Guid? OperationalUnitId,
    Guid? FaenaId,
    string? FaenaCodigo,
    string EstadoOperacionalCodigo,
    string? Criticidad,
    bool ParticipaEnDisponibilidad)
{
    public MaintenanceTargetSummary ToSummary() => new(
        Tipo, Codigo, Nombre, string.Empty, string.Empty, FaenaCodigo, null,
        EstadoOperacionalCodigo, EstadoOperacionalCodigo, Criticidad,
        Tipo == MaintenanceTargetType.OperationalUnit, null, false, null, null, null,
        ParticipaEnDisponibilidad);
}

public interface IMaintenanceTargetService
{
    Task<IReadOnlyCollection<MaintenanceTargetSummary>> ListAsync(
        MaintenanceTargetQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<ResolvedMaintenanceTarget> ResolveAsync(
        MaintenanceTargetReference reference,
        UserAccessContext user,
        CancellationToken cancellationToken);
}

/// <summary>
/// Normalizes the transitional payload accepted by work notifications and work orders.
/// New callers use <see cref="MaintenanceTargetReference"/>; legacy callers still send
/// a single asset or operational-unit code until their migration is complete.
/// </summary>
public static class MaintenanceTargetRequestNormalizer
{
    public static MaintenanceTargetReference? Normalize(
        MaintenanceTargetReference? objetivo,
        string? activoCodigo,
        string? unidadOperativaCodigo,
        bool required = true)
    {
        var hasObjective = objetivo is not null;
        var hasAsset = !string.IsNullOrWhiteSpace(activoCodigo);
        var hasUnit = !string.IsNullOrWhiteSpace(unidadOperativaCodigo);

        if (hasObjective && (hasAsset || hasUnit))
        {
            throw new DomainException("Debe informar Objetivo o un campo heredado, no ambos.");
        }

        if (hasAsset && hasUnit)
        {
            throw new DomainException("No se pueden informar activo y unidad operativa como objetivos principales.");
        }

        var result = objetivo ??
            (hasAsset ? new MaintenanceTargetReference(MaintenanceTargetType.Asset, activoCodigo!.Trim()) : null) ??
            (hasUnit ? new MaintenanceTargetReference(MaintenanceTargetType.OperationalUnit, unidadOperativaCodigo!.Trim()) : null);

        if (required && result is null)
        {
            throw new DomainException("Debe indicar un objetivo de mantenimiento.");
        }

        if (result is not null && string.IsNullOrWhiteSpace(result.Codigo))
        {
            throw new DomainException("El código del objetivo de mantenimiento es obligatorio.");
        }

        if (result is not null && !Enum.IsDefined(result.Tipo))
        {
            throw new DomainException("El tipo de objetivo de mantenimiento no es válido.");
        }

        return result is null ? null : result with { Codigo = result.Codigo.Trim().ToUpperInvariant() };
    }
}
