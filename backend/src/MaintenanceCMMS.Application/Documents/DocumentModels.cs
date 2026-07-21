namespace MaintenanceCMMS.Application.Documents;

public enum DocumentEntityType
{
    Activo = 0,
    OT = 1,
    Faena = 2
}

public enum DocumentLifecycleStatus
{
    Vigente = 0,
    PorVencer = 1,
    Vencido = 2,
    PendienteCarga = 3,
    PendienteValidacion = 4,
    Rechazado = 5,
    Reemplazado = 6,
    Anulado = 7
}

public sealed record DocumentQuery(
    DocumentEntityType? EntidadTipo = null,
    string? EntidadCodigo = null,
    string? FaenaCodigo = null,
    string? TipoDocumento = null,
    DocumentLifecycleStatus? Estado = null,
    bool IncludeHistorical = false);

public sealed record CreateDocumentTypeRequest(
    string Codigo,
    string Nombre,
    DocumentEntityType? AplicaA = null,
    bool Obligatorio = false,
    bool Critico = false,
    bool BloqueaDisponibilidad = false,
    int PlazoAlertaDias = 30,
    IReadOnlyCollection<string>? RolesResponsables = null,
    bool RequierePdfAlerta = false,
    string? PlantillaHtmlCodigo = null,
    bool Activo = true);

public sealed record UpdateDocumentTypeRequest(
    string Nombre,
    DocumentEntityType? AplicaA = null,
    bool Obligatorio = false,
    bool Critico = false,
    bool BloqueaDisponibilidad = false,
    int PlazoAlertaDias = 30,
    IReadOnlyCollection<string>? RolesResponsables = null,
    bool RequierePdfAlerta = false,
    string? PlantillaHtmlCodigo = null,
    bool Activo = true,
    string? Reason = null);

public sealed record DocumentTypeResponse(
    string Codigo,
    string Nombre,
    DocumentEntityType? AplicaA,
    bool Obligatorio,
    bool Critico,
    bool BloqueaDisponibilidad,
    int PlazoAlertaDias,
    IReadOnlyCollection<string> RolesResponsables,
    bool RequierePdfAlerta,
    string? PlantillaHtmlCodigo,
    bool Activo);

public sealed record CreateDocumentRequest(
    DocumentEntityType EntidadTipo,
    string EntidadCodigo,
    string TipoDocumento,
    DateOnly? FechaEmision,
    DateOnly? FechaVencimiento,
    string? ArchivoKey,
    string? SharePointUrl,
    bool? Critico = null,
    bool? Obligatorio = null,
    bool? BloqueaDisponibilidad = null,
    string? Reason = null,
    IReadOnlyCollection<string>? EntidadCodigos = null,
    string? NombreOriginal = null,
    string? TipoMime = null,
    long? TamanoBytes = null,
    string? Checksum = null);

public sealed record UpdateDocumentRequest(
    DateOnly? FechaEmision,
    DateOnly? FechaVencimiento,
    string? ArchivoKey,
    string? SharePointUrl,
    bool? Critico = null,
    bool? Obligatorio = null,
    bool? BloqueaDisponibilidad = null,
    string? Reason = null);

public sealed record ReplaceDocumentRequest(
    DateOnly? FechaEmision,
    DateOnly? FechaVencimiento,
    string? ArchivoKey,
    string? SharePointUrl,
    string Reason);

public sealed record ValidateDocumentRequest(string? Comments = null);

public sealed record RejectDocumentRequest(string Reason);

public sealed record AnnulDocumentRequest(string Reason);

public sealed record DocumentResponse(
    string DocumentoId,
    DocumentEntityType EntidadTipo,
    string EntidadCodigo,
    string TipoDocumento,
    DocumentLifecycleStatus Estado,
    DateOnly? FechaEmision,
    DateOnly? FechaVencimiento,
    string? ArchivoKey,
    string? SharePointUrl,
    bool Critico,
    bool Obligatorio,
    bool BloqueaDisponibilidad,
    bool EsHistorico,
    bool FechaVencimientoValidada,
    string? ValidadoPor,
    DateTimeOffset? ValidadoEnUtc,
    string? RechazadoPor,
    DateTimeOffset? RechazadoEnUtc,
    string? MotivoRechazo,
    string? ReemplazaDocumentoId,
    string? ReemplazadoPorDocumentoId,
    string? AnuladoPor,
    DateTimeOffset? AnuladoEnUtc,
    string? MotivoAnulacion,
    DateTimeOffset FechaCargaUtc,
    string CargadoPor,
    int? DiasParaVencer,
    bool BloqueaDisponibilidadActual,
    IReadOnlyCollection<string>? EntidadCodigos = null,
    int? VersionVigente = null,
    string? ArchivoId = null);

public sealed record DocumentVersionResponse(
    string VersionId,
    string DocumentoId,
    int NumeroVersion,
    string CodigoVersion,
    string ArchivoId,
    string ArchivoKey,
    string? SharePointUrl,
    DateTimeOffset FechaCargaUtc,
    string CargadoPor,
    string? Observaciones,
    bool Vigente,
    DateOnly? FechaEmision = null,
    DateOnly? FechaVencimiento = null,
    string? EstadoValidacion = null,
    string? ValidadoPor = null,
    DateTimeOffset? ValidadoEnUtc = null,
    string? RechazadoPor = null,
    DateTimeOffset? RechazadoEnUtc = null,
    string? MotivoRechazo = null,
    string? ReemplazaVersionId = null,
    string? ResponsableCorreccion = null,
    string? EstadoCorreccion = null,
    string? ObservacionCorreccion = null,
    string? CicloCorreccionId = null);

public sealed record AssignDocumentAssetsRequest(
    IReadOnlyCollection<string> ActivoCodigos,
    string? Reason = null);

public sealed record UnassignDocumentAssetRequest(string Reason);

public sealed record DocumentMatrixRow(
    DocumentEntityType EntidadTipo,
    string EntidadCodigo,
    string NombreEntidad,
    string TipoDocumento,
    bool Obligatorio,
    bool BloqueaDisponibilidad,
    DocumentLifecycleStatus Estado,
    string? DocumentoId,
    DateOnly? FechaVencimiento,
    bool BloqueaDisponibilidadActual);

public sealed record DocumentDashboardSummary(
    int Total,
    int Vigentes,
    int PorVencer,
    int Vencidos,
    int PendientesCarga,
    int PendientesValidacion,
    int Rechazados,
    int Reemplazados,
    int Anulados,
    int BloqueanDisponibilidad);
