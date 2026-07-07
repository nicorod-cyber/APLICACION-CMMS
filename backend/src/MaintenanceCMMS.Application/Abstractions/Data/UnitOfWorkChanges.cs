namespace MaintenanceCMMS.Application.Abstractions.Data;

public sealed record UnitOfWorkChanges(
    string Reason,
    IReadOnlyCollection<object> Added,
    IReadOnlyCollection<object> Updated,
    IReadOnlyCollection<object> Deleted);

