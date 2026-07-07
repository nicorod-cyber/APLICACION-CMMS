using MaintenanceCMMS.Application.Auth;

namespace MaintenanceCMMS.Application.Assets;

public interface IAssetService
{
    Task<IReadOnlyCollection<AssetSummary>> ListAsync(
        AssetListQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<AssetDetail?> GetByIdAsync(
        string codigo,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<AssetDetail> CreateAsync(
        CreateAssetRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<AssetDetail?> UpdateAsync(
        string codigo,
        UpdateAssetRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<AssetStateEventResponse?> AddStateEventAsync(
        string codigo,
        CreateAssetStateEventRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AssetHistoryEntry>> GetHistoryAsync(
        string codigo,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AssetDocumentResponse>> GetDocumentsAsync(
        string codigo,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<AssetCostSummary?> GetCostsAsync(
        string codigo,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<AssetAvailabilityResponse?> GetAvailabilityAsync(
        string codigo,
        UserAccessContext user,
        CancellationToken cancellationToken);
}
