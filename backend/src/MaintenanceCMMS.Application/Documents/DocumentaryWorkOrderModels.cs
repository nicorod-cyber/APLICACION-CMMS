namespace MaintenanceCMMS.Application.Documents;

public sealed record DocumentaryWorkOrderRequirementProgress(
    string Id,
    string TipoDocumentoCodigo,
    string Estado,
    bool Aplicable,
    string? DocumentoOrigenId,
    string? VersionDocumentoOrigenId,
    string? Observacion,
    DateTimeOffset? CompletadoEnUtc);

public sealed record DocumentaryWorkOrderProgress(
    string MatrizCodigo,
    int MatrizVersion,
    int Total,
    int Completados,
    int Pendientes,
    int Rechazados,
    int Vencidos,
    int Porcentaje,
    IReadOnlyCollection<DocumentaryWorkOrderRequirementProgress> Requisitos);

public sealed record DocumentaryEngineRunResponse(
    DateOnly FechaReferencia,
    int ActivosEvaluados,
    int OrdenesCreadas,
    int OrdenesReutilizadas,
    int RequisitosCreados,
    IReadOnlyCollection<string> NumerosOT);

public interface IDocumentaryWorkOrderService
{
    Task<DocumentaryEngineRunResponse> RunAsync(DateOnly fechaReferencia, string ejecutadoPor, CancellationToken cancellationToken);
}
