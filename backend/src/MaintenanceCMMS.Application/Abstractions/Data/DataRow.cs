namespace MaintenanceCMMS.Application.Abstractions.Data;

public sealed record DataRow(IReadOnlyDictionary<string, string?> Values)
{
    public string? GetValue(string columnName)
    {
        return Values.TryGetValue(columnName, out var value) ? value : null;
    }
}

