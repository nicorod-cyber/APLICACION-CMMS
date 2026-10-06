using MaintenanceCMMS.Application.Auth;

namespace MaintenanceCMMS.Application.WorkNotifications;

public interface IWorkNotificationService
{
    Task<IReadOnlyCollection<WorkNotificationResponse>> ListAsync(WorkNotificationQuery query, UserAccessContext user, CancellationToken cancellationToken);
    Task<WorkNotificationResponse?> GetByIdAsync(string id, UserAccessContext user, CancellationToken cancellationToken);
    Task<WorkNotificationResponse> CreateAsync(CreateWorkNotificationRequest request, UserAccessContext user, CancellationToken cancellationToken);
    Task<WorkNotificationResponse?> UpdateDraftAsync(string id, UpdateWorkNotificationDraftRequest request, UserAccessContext user, CancellationToken cancellationToken);
    Task<WorkNotificationResponse?> SubmitAsync(string id, UserAccessContext user, CancellationToken cancellationToken);
    Task<WorkNotificationResponse?> ReturnToSiteAsync(string id, WorkNotificationActionRequest request, UserAccessContext user, CancellationToken cancellationToken);
    Task<WorkNotificationResponse?> AcceptForManagementAsync(string id, UserAccessContext user, CancellationToken cancellationToken);
    Task<WorkNotificationResponse?> RejectAsync(string id, WorkNotificationActionRequest request, UserAccessContext user, CancellationToken cancellationToken);
    Task<WorkNotificationResponse?> AnnulAsync(string id, WorkNotificationActionRequest request, UserAccessContext user, CancellationToken cancellationToken);
}
