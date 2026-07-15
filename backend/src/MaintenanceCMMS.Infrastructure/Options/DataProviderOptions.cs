namespace MaintenanceCMMS.Infrastructure.Options;

public sealed class DataProviderSettings
{
    public string Provider { get; set; } = "PostgreSql";

    public string ExcelPath { get; set; } = "data/excel";

    public string SqlServerConnectionString { get; set; } = string.Empty;

    public string PostgreSqlConnectionString { get; set; } = string.Empty;
}

public sealed class DataProviderOptions
{
    public ExcelProviderOptions Excel { get; init; } = new();

    public SqlProviderOptions SqlServer { get; init; } = new();

    public SqlProviderOptions PostgreSql { get; init; } = new();
}

public sealed class ExcelProviderOptions
{
    public string BasePath { get; init; } = "data/excel";
}

public sealed class SqlProviderOptions
{
    public string ConnectionStringName { get; init; } = string.Empty;
}
