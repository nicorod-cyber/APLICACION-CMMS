using MaintenanceCMMS.Domain.Enums;

namespace MaintenanceCMMS.Application.Abstractions.Data;

public interface IDataProvider
{
    string Name { get; }

    DataProviderType ProviderType { get; }

    Task InitializeAsync(CancellationToken cancellationToken);

    Task<DataProviderHealth> CheckHealthAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<DataRow>> ReadRowsAsync(string schemaName, CancellationToken cancellationToken);

    Task SaveRowsAsync(string schemaName, IReadOnlyCollection<DataRow> rows, CancellationToken cancellationToken);

    Task<IReadOnlyList<T>> QueryAsync<T>(DataQuery query, CancellationToken cancellationToken);

    Task SaveChangesAsync(UnitOfWorkChanges changes, CancellationToken cancellationToken);
}
