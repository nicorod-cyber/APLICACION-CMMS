namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;

/// <summary>
/// PostgreSQL-backed operational collection used by modules originally modelled as spreadsheet row sets.
/// </summary>
public sealed class OperationalDataSetEntity : PostgreSqlEntity
{
    public string Code { get; set; } = string.Empty;
    public string Payload { get; set; } = "[]";
}
