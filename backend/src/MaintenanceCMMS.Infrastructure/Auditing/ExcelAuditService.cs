using MaintenanceCMMS.Application.Abstractions.Data;
using MaintenanceCMMS.Application.Auditing;

namespace MaintenanceCMMS.Infrastructure.Auditing;

public sealed class ExcelAuditService : IAuditService
{
    private const string AuditSchema = "audit_log";

    private readonly IDataProvider _dataProvider;

    private readonly IAuditContextAccessor _contextAccessor;

    public ExcelAuditService(IDataProvider dataProvider, IAuditContextAccessor contextAccessor)
    {
        _dataProvider = dataProvider;
        _contextAccessor = contextAccessor;
    }

    public async Task<string> RecordAsync(AuditEventRequest auditEvent, CancellationToken cancellationToken)
    {
        var auditId = Guid.NewGuid().ToString("D");
        var context = _contextAccessor.Current;
        var rows = (await _dataProvider.ReadRowsAsync(AuditSchema, cancellationToken)).ToList();

        rows.Add(new DataRow(new Dictionary<string, string?>
        {
            ["AuditId"] = auditId,
            ["OccurredAtUtc"] = (auditEvent.OccurredAtUtc ?? DateTimeOffset.UtcNow).UtcDateTime.ToString("O"),
            ["UserId"] = auditEvent.UserId,
            ["Action"] = auditEvent.Action,
            ["Module"] = auditEvent.Module,
            ["EntityName"] = auditEvent.EntityName,
            ["EntityId"] = auditEvent.EntityId,
            ["FaenaCodigo"] = auditEvent.FaenaCodigo,
            ["Severity"] = auditEvent.Severity.ToString(),
            ["PreviousValue"] = auditEvent.PreviousValue,
            ["NewValue"] = auditEvent.NewValue,
            ["IpAddress"] = auditEvent.IpAddress ?? context.IpAddress,
            ["Device"] = auditEvent.Device ?? context.Device,
            ["Reason"] = auditEvent.Reason,
            ["Success"] = auditEvent.Success ? "true" : "false",
            ["Detail"] = auditEvent.Detail,
            ["CorrelationId"] = auditEvent.CorrelationId ?? context.CorrelationId,
            ["Before"] = auditEvent.PreviousValue,
            ["After"] = auditEvent.NewValue
        }));

        await _dataProvider.SaveRowsAsync(AuditSchema, rows, cancellationToken);
        return auditId;
    }

    public async Task<AuditQueryResult> QueryAsync(AuditQuery query, CancellationToken cancellationToken)
    {
        var rows = await _dataProvider.ReadRowsAsync(AuditSchema, cancellationToken);
        var entries = rows.Select(MapEntry).Where(entry => Matches(entry, query));
        var ordered = entries
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .ToArray();

        var items = ordered
            .Skip(Math.Max(0, query.Skip))
            .Take(Math.Clamp(query.Take, 1, 1000))
            .ToArray();

        return new AuditQueryResult(ordered.Length, items);
    }

    private static AuditLogEntry MapEntry(DataRow row)
    {
        var severity = Enum.TryParse<AuditSeverity>(row.GetValue("Severity"), ignoreCase: true, out var parsedSeverity)
            ? parsedSeverity
            : AuditSeverity.Low;

        return new AuditLogEntry(
            row.GetValue("AuditId")?.Trim() ?? Guid.NewGuid().ToString("D"),
            ParseDate(row.GetValue("OccurredAtUtc")) ?? DateTimeOffset.MinValue,
            row.GetValue("UserId")?.Trim() ?? string.Empty,
            row.GetValue("Action")?.Trim() ?? string.Empty,
            row.GetValue("Module")?.Trim() ?? InferModule(row.GetValue("Action"), row.GetValue("EntityName")),
            row.GetValue("EntityName")?.Trim() ?? string.Empty,
            row.GetValue("EntityId")?.Trim() ?? string.Empty,
            EmptyToNull(row.GetValue("FaenaCodigo")),
            severity,
            EmptyToNull(row.GetValue("PreviousValue")) ?? EmptyToNull(row.GetValue("Before")),
            EmptyToNull(row.GetValue("NewValue")) ?? EmptyToNull(row.GetValue("After")),
            EmptyToNull(row.GetValue("IpAddress")),
            EmptyToNull(row.GetValue("Device")),
            EmptyToNull(row.GetValue("Reason")),
            ParseBool(row.GetValue("Success"), defaultValue: true),
            EmptyToNull(row.GetValue("Detail")),
            EmptyToNull(row.GetValue("CorrelationId")));
    }

    private static bool Matches(AuditLogEntry entry, AuditQuery query)
    {
        return MatchesText(entry.UserId, query.UserId) &&
               MatchesText(entry.Module, query.Module) &&
               MatchesText(entry.EntityName, query.EntityName) &&
               MatchesText(entry.Action, query.Action) &&
               MatchesText(entry.FaenaCodigo, query.FaenaCodigo) &&
               (!query.Severity.HasValue || entry.Severity == query.Severity.Value) &&
               (!query.FromUtc.HasValue || entry.OccurredAtUtc >= query.FromUtc.Value) &&
               (!query.ToUtc.HasValue || entry.OccurredAtUtc <= query.ToUtc.Value);
    }

    private static bool MatchesText(string? value, string? query)
    {
        return string.IsNullOrWhiteSpace(query) ||
               (!string.IsNullOrWhiteSpace(value) && value.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static DateTimeOffset? ParseDate(string? value)
    {
        return DateTimeOffset.TryParse(value, out var result) ? result : null;
    }

    private static bool ParseBool(string? value, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Trim().Equals("si", StringComparison.OrdinalIgnoreCase) ||
               value.Trim().Equals("1", StringComparison.OrdinalIgnoreCase);
    }

    private static string InferModule(string? action, string? entityName)
    {
        var text = $"{action} {entityName}";
        if (text.Contains("login", StringComparison.OrdinalIgnoreCase))
        {
            return AuditModules.Authentication;
        }

        if (text.Contains("user", StringComparison.OrdinalIgnoreCase))
        {
            return AuditModules.Users;
        }

        return AuditModules.Configuration;
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
