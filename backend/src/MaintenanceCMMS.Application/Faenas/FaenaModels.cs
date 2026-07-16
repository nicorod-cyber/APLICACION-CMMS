namespace MaintenanceCMMS.Application.Faenas;

public sealed record FaenaQuery(
    string? Search = null,
    bool IncludeInactive = false,
    string? Codigo = null,
    string? Nombre = null,
    string? Zona = null,
    string? Cliente = null,
    string? TipoFaena = null,
    string? Region = null,
    string? Comuna = null,
    Guid? ResponsableUsuarioId = null,
    bool? Activa = null,
    string? UbicacionTecnicaCodigo = null);

public sealed record TechnicalLocationSummary(
    Guid Id,
    string Codigo,
    string Nombre,
    bool Obsoleto);

public sealed record FaenaResponse(
    Guid Id,
    string Codigo,
    string Nombre,
    string? Zona,
    string? Cliente,
    string? CentroCostes,
    string? TipoFaena,
    string? Region,
    string? Comuna,
    decimal? Latitud,
    decimal? Longitud,
    Guid? ResponsableUsuarioId,
    string? ResponsableNombre,
    bool Activo,
    TechnicalLocationSummary? UbicacionTecnica);

public sealed record UpsertFaenaRequest(
    string Codigo,
    string Nombre,
    string Zona,
    string Cliente,
    string? CentroCostes,
    string TipoFaena,
    string Region,
    string Comuna,
    decimal? Latitud,
    decimal? Longitud,
    Guid ResponsableUsuarioId,
    bool Activo,
    string UbicacionTecnicaCodigo,
    string UbicacionTecnicaNombre,
    bool UbicacionTecnicaObsoleta = false);
