namespace MaintenanceCMMS.Application.Auditing;

public enum AuditSeverity
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

public static class AuditModules
{
    public const string Assets = "Activos";
    public const string TechnicalHierarchy = "JerarquiaTecnica";
    public const string Documents = "Documentos";
    public const string Warehouse = "Bodega";
    public const string Stock = "Stock";
    public const string SpareParts = "Repuestos";
    public const string MaterialRequests = "SolicitudesRepuestos";
    public const string Procurement = "Abastecimiento";
    public const string WorkNotifications = "AvisosTrabajo";
    public const string WorkOrders = "OT";
    public const string LaborHours = "HH";
    public const string Evidence = "Evidencias";
    public const string Signatures = "Firmas";
    public const string Costs = "Costos";
    public const string Users = "Usuarios";
    public const string Imports = "Importaciones";
    public const string Alerts = "Alertas";
    public const string Configuration = "Configuracion";
    public const string SharePoint = "SharePoint";
    public const string Pdfs = "PDFs";
    public const string Mail = "Correos";
    public const string Authentication = "Autenticacion";
}

public sealed record AuditEventRequest(
    string UserId,
    string Action,
    string Module,
    string EntityName,
    string EntityId,
    string? PreviousValue = null,
    string? NewValue = null,
    string? FaenaCodigo = null,
    AuditSeverity Severity = AuditSeverity.Low,
    string? Reason = null,
    bool Success = true,
    string? Detail = null,
    string? IpAddress = null,
    string? Device = null,
    string? CorrelationId = null,
    DateTimeOffset? OccurredAtUtc = null);

public record AuditLog(
    string AuditId,
    DateTimeOffset OccurredAtUtc,
    string UserId,
    string Action,
    string Module,
    string EntityName,
    string EntityId,
    string? FaenaCodigo,
    AuditSeverity Severity,
    string? PreviousValue,
    string? NewValue,
    string? IpAddress,
    string? Device,
    string? Reason,
    bool Success,
    string? Detail,
    string? CorrelationId);

public sealed record AuditLogEntry(
    string AuditId,
    DateTimeOffset OccurredAtUtc,
    string UserId,
    string Action,
    string Module,
    string EntityName,
    string EntityId,
    string? FaenaCodigo,
    AuditSeverity Severity,
    string? PreviousValue,
    string? NewValue,
    string? IpAddress,
    string? Device,
    string? Reason,
    bool Success,
    string? Detail,
    string? CorrelationId)
    : AuditLog(
        AuditId,
        OccurredAtUtc,
        UserId,
        Action,
        Module,
        EntityName,
        EntityId,
        FaenaCodigo,
        Severity,
        PreviousValue,
        NewValue,
        IpAddress,
        Device,
        Reason,
        Success,
        Detail,
        CorrelationId);

public sealed record AuditQuery(
    string? UserId = null,
    string? Module = null,
    string? EntityName = null,
    string? Action = null,
    string? FaenaCodigo = null,
    AuditSeverity? Severity = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    int Skip = 0,
    int Take = 200);

public sealed record AuditQueryResult(
    int TotalCount,
    IReadOnlyCollection<AuditLogEntry> Items);

public sealed record AuditRequestContext(
    string? IpAddress,
    string? Device,
    string? CorrelationId);
