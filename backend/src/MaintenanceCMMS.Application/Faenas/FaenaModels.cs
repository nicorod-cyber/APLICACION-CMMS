namespace MaintenanceCMMS.Application.Faenas;

public sealed record FaenaQuery(
    string? Search = null,
    bool IncludeInactive = false);

public sealed record FaenaResponse(
    string Codigo,
    string Nombre,
    string Empresa,
    string? Descripcion,
    string? UbicacionTecnica,
    string? CentroCostos,
    string? TipoFaena,
    string? Region,
    string? Comuna,
    string? Latitud,
    string? Longitud,
    string? Responsable,
    string Estado,
    bool Activa,
    IReadOnlyDictionary<string, string?> Metadata);
