namespace MaintenanceCMMS.Application.Abstractions.Data;

public sealed record DataProviderHealth(
    string Provider,
    bool IsHealthy,
    string BasePath,
    IReadOnlyCollection<DataProviderFileHealth> Files,
    IReadOnlyCollection<string> Errors);

public sealed record DataProviderFileHealth(
    string SchemaName,
    string FileName,
    bool Exists,
    bool HasDataSheet,
    IReadOnlyCollection<string> MissingColumns,
    IReadOnlyCollection<string> DuplicateNaturalKeys);

