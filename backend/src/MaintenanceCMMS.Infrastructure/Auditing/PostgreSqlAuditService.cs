using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceCMMS.Infrastructure.Auditing;

public sealed class PostgreSqlAuditService : IAuditService
{
    private readonly CmmsDbContext _dbContext;
    private readonly IAuditContextAccessor _contextAccessor;

    public PostgreSqlAuditService(CmmsDbContext dbContext, IAuditContextAccessor contextAccessor)
    {
        _dbContext = dbContext;
        _contextAccessor = contextAccessor;
    }

    public async Task<string> RecordAsync(AuditEventRequest auditEvent, CancellationToken cancellationToken)
    {
        var context = _contextAccessor.Current;
        var entry = new AuditLogEntity
        {
            Id = Guid.NewGuid(),
            OccurredAtUtc = (auditEvent.OccurredAtUtc ?? DateTimeOffset.UtcNow).ToUniversalTime(),
            UserId = auditEvent.UserId,
            Action = auditEvent.Action,
            Module = auditEvent.Module,
            EntityName = auditEvent.EntityName,
            EntityId = auditEvent.EntityId,
            FaenaCode = EmptyToNull(auditEvent.FaenaCodigo),
            Severity = auditEvent.Severity.ToString(),
            PreviousValue = auditEvent.PreviousValue,
            NewValue = auditEvent.NewValue,
            IpAddress = auditEvent.IpAddress ?? context.IpAddress,
            Device = auditEvent.Device ?? context.Device,
            Reason = auditEvent.Reason,
            Success = auditEvent.Success,
            Detail = auditEvent.Detail,
            CorrelationId = auditEvent.CorrelationId ?? context.CorrelationId
        };

        _dbContext.AuditLogs.Add(entry);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return entry.Id.ToString("D");
    }

    public async Task<AuditQueryResult> QueryAsync(AuditQuery query, CancellationToken cancellationToken)
    {
        var records = _dbContext.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.UserId))
        {
            records = records.Where(entry => entry.UserId.Contains(query.UserId));
        }

        if (!string.IsNullOrWhiteSpace(query.Module))
        {
            records = records.Where(entry => entry.Module.Contains(query.Module));
        }

        if (!string.IsNullOrWhiteSpace(query.EntityName))
        {
            records = records.Where(entry => entry.EntityName.Contains(query.EntityName));
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            records = records.Where(entry => entry.Action.Contains(query.Action));
        }

        if (!string.IsNullOrWhiteSpace(query.FaenaCodigo))
        {
            records = records.Where(entry => entry.FaenaCode != null && entry.FaenaCode.Contains(query.FaenaCodigo));
        }

        if (query.Severity.HasValue)
        {
            var severity = query.Severity.Value.ToString();
            records = records.Where(entry => entry.Severity == severity);
        }

        if (query.FromUtc.HasValue)
        {
            records = records.Where(entry => entry.OccurredAtUtc >= query.FromUtc.Value.ToUniversalTime());
        }

        if (query.ToUtc.HasValue)
        {
            records = records.Where(entry => entry.OccurredAtUtc <= query.ToUtc.Value.ToUniversalTime());
        }

        var total = await records.CountAsync(cancellationToken);
        var items = await records
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .Skip(Math.Max(0, query.Skip))
            .Take(Math.Clamp(query.Take, 1, 1000))
            .Select(entry => new AuditLogEntry(
                entry.Id.ToString("D"),
                entry.OccurredAtUtc,
                entry.UserId,
                entry.Action,
                entry.Module,
                entry.EntityName,
                entry.EntityId,
                entry.FaenaCode,
                Enum.Parse<AuditSeverity>(entry.Severity),
                entry.PreviousValue,
                entry.NewValue,
                entry.IpAddress,
                entry.Device,
                entry.Reason,
                entry.Success,
                entry.Detail,
                entry.CorrelationId))
            .ToListAsync(cancellationToken);

        return new AuditQueryResult(total, items);
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
