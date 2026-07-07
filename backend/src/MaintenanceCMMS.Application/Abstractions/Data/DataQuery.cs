namespace MaintenanceCMMS.Application.Abstractions.Data;

public sealed record DataQuery(
    string EntityName,
    IReadOnlyDictionary<string, string>? Filters = null,
    int? Skip = null,
    int? Take = null);

