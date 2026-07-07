using MaintenanceCMMS.Application.Auditing;

namespace MaintenanceCMMS.Infrastructure.Auditing;

public sealed class AuditContextAccessor : IAuditContextAccessor
{
    private static readonly AsyncLocal<AuditRequestContext?> CurrentContext = new();

    public AuditRequestContext Current
    {
        get => CurrentContext.Value ?? new AuditRequestContext(null, null, null);
        set => CurrentContext.Value = value;
    }
}
