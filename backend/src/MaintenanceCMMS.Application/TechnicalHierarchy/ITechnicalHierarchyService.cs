using MaintenanceCMMS.Application.Auth;

namespace MaintenanceCMMS.Application.TechnicalHierarchy;

public interface ITechnicalHierarchyService
{
    Task<IReadOnlyCollection<TechnicalNodeResponse>> ListAsync(
        TechnicalHierarchyQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<TechnicalHierarchyTreeNode>> GetTreeAsync(
        TechnicalHierarchyQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<TechnicalNodeResponse?> GetByCodeAsync(
        string code,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<TechnicalNodeResponse> CreateAsync(
        CreateTechnicalNodeRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<TechnicalNodeResponse?> UpdateAsync(
        string code,
        UpdateTechnicalNodeRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<TechnicalNodeResponse?> MarkObsoleteAsync(
        string code,
        MarkTechnicalNodeObsoleteRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<SimilarTechnicalNode>> DetectSimilarAsync(
        TechnicalHierarchyQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<TechnicalNodeResponse?> MergeAsync(
        MergeTechnicalNodesRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<TechnicalNodeResponse>> AssignFamiliesAsync(
        BulkFamilyAssignmentRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<TechnicalNodeResponse?> AssignAssetsAsync(
        string code,
        AssetAssignmentRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);
}
