using MaintenanceCMMS.Application.Auth;

namespace MaintenanceCMMS.Application.Documents;

public sealed record DocumentRequirementMatrixItemRequest(
    string TipoDocumentoCodigo,
    bool Obligatorio,
    bool Critico,
    bool BloqueaDisponibilidad,
    bool RequiereFechaVencimiento = true,
    int DiasAnticipacion = 45);

public sealed record CreateDocumentRequirementMatrixVersionRequest(
    string Codigo,
    string TipoActivoCodigo,
    string? FamiliaEquipoCodigo,
    DateOnly VigenciaDesde,
    string MotivoCambio,
    IReadOnlyCollection<DocumentRequirementMatrixItemRequest> Requisitos);

public sealed record DocumentRequirementMatrixItemResponse(
    string Id,
    string TipoDocumentoCodigo,
    bool Obligatorio,
    bool Critico,
    bool BloqueaDisponibilidad,
    bool RequiereFechaVencimiento,
    int DiasAnticipacion);

public sealed record DocumentRequirementMatrixResponse(
    string Id,
    string Codigo,
    int NumeroVersion,
    string TipoActivoCodigo,
    string? FamiliaEquipoCodigo,
    DateOnly VigenciaDesde,
    DateOnly? VigenciaHasta,
    string Estado,
    string CreadoPor,
    string? MotivoCambio,
    IReadOnlyCollection<DocumentRequirementMatrixItemResponse> Requisitos);

public interface IDocumentRequirementMatrixService
{
    Task<IReadOnlyCollection<DocumentRequirementMatrixResponse>> ListAsync(bool incluirHistoricas, UserAccessContext user, CancellationToken cancellationToken);
    Task<DocumentRequirementMatrixResponse> CreateVersionAsync(CreateDocumentRequirementMatrixVersionRequest request, UserAccessContext user, CancellationToken cancellationToken);
}
