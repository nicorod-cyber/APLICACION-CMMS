using MaintenanceCMMS.Application.Auth;

namespace MaintenanceCMMS.Application.Inventory;

public interface IInventoryService
{
    Task<InventoryDashboardResponse> GetDashboardAsync(
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<WarehouseResponse>> ListWarehousesAsync(
        WarehouseQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<WarehouseResponse> CreateWarehouseAsync(
        CreateWarehouseRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<SparePartSummary>> ListSparePartsAsync(
        SparePartQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<SparePartDetail?> GetSparePartAsync(
        string code,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<SparePartDetail> CreateSparePartAsync(
        CreateSparePartRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<SparePartDetail?> UpdateSparePartAsync(
        string code,
        UpdateSparePartRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<StockItemResponse>> ListStockAsync(
        StockQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<StockMovementResponse>> ListMovementsAsync(
        StockMovementQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<StockMovementResponse> RegisterMovementAsync(
        StockMovementRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<StockReservationResponse>> ListReservationsAsync(
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<StockReservationResponse> CreateReservationAsync(
        CreateStockReservationRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<StockReservationResponse?> ReleaseReservationAsync(
        string reservationId,
        ReleaseStockReservationRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<StockMovementResponse> DeliverMaterialAsync(
        DeliverMaterialRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<StockTransferResponse>> ListTransfersAsync(
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<StockTransferResponse> TransferStockAsync(
        TransferStockRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<StockTransferResponse?> ReceiveTransferAsync(
        string transferId,
        ReceiveTransferRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<StockMovementResponse> ReturnStockAsync(
        ReturnStockRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<StockMovementResponse> AdjustStockAsync(
        AdjustStockRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<StockMovementResponse> WriteOffStockAsync(
        WriteOffStockRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);
}
