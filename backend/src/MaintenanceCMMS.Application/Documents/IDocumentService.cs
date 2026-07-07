using MaintenanceCMMS.Application.Auth;

namespace MaintenanceCMMS.Application.Documents;

public interface IDocumentService
{
    Task<IReadOnlyCollection<DocumentTypeResponse>> ListTypesAsync(CancellationToken cancellationToken);

    Task<DocumentTypeResponse> CreateTypeAsync(
        CreateDocumentTypeRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<DocumentTypeResponse?> UpdateTypeAsync(
        string code,
        UpdateDocumentTypeRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<DocumentResponse>> ListAsync(
        DocumentQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<DocumentResponse?> GetByIdAsync(
        string id,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<DocumentResponse> CreateAsync(
        CreateDocumentRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<DocumentResponse?> UpdateAsync(
        string id,
        UpdateDocumentRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<DocumentResponse?> ValidateAsync(
        string id,
        ValidateDocumentRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<DocumentResponse?> RejectAsync(
        string id,
        RejectDocumentRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<DocumentResponse?> ReplaceAsync(
        string id,
        ReplaceDocumentRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<DocumentResponse?> AnnulAsync(
        string id,
        AnnulDocumentRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<DocumentResponse>> GetExpiredAsync(
        string? faenaCodigo,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<DocumentResponse>> GetExpiringAsync(
        string? faenaCodigo,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<DocumentMatrixRow>> GetMatrixAsync(
        string? faenaCodigo,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<DocumentDashboardSummary> GetSummaryAsync(
        string? faenaCodigo,
        UserAccessContext user,
        CancellationToken cancellationToken);
}
