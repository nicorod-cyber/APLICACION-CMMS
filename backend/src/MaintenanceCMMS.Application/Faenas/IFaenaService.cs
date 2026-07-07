using MaintenanceCMMS.Application.Auth;

namespace MaintenanceCMMS.Application.Faenas;

public interface IFaenaService
{
    Task<IReadOnlyCollection<FaenaResponse>> ListAsync(
        FaenaQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken);
}
