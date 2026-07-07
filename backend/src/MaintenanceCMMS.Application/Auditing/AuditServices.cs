namespace MaintenanceCMMS.Application.Auditing;

public interface IAuditService
{
    Task<string> RecordAsync(AuditEventRequest auditEvent, CancellationToken cancellationToken);

    Task<AuditQueryResult> QueryAsync(AuditQuery query, CancellationToken cancellationToken);
}

public interface IAuditContextAccessor
{
    AuditRequestContext Current { get; set; }
}
