using MaintenanceCMMS.Application.Auth;

namespace MaintenanceCMMS.Application.Faenas;

public interface IFaenaService
{
    Task<IReadOnlyCollection<FaenaResponse>> ListAsync(
        FaenaQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<FaenaResponse?> GetByCodeAsync(
        string code,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<FaenaResponse> CreateAsync(
        UpsertFaenaRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<FaenaResponse?> UpdateAsync(
        string code,
        UpsertFaenaRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);
}
