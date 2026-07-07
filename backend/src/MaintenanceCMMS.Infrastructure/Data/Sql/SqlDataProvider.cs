using MaintenanceCMMS.Application.Abstractions.Data;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Domain.Enums;
using MaintenanceCMMS.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace MaintenanceCMMS.Infrastructure.Data.Sql;

public sealed class SqlDataProvider : IDataProvider
{
    private readonly DataProviderSettings _settings;

    public SqlDataProvider(IOptions<DataProviderSettings> settings)
    {
        _settings = settings.Value;
    }

    public string Name => _settings.Provider;

    public DataProviderType ProviderType => Enum.TryParse<DataProviderType>(_settings.Provider, ignoreCase: true, out var type)
        ? type
        : DataProviderType.SqlServer;

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task<DataProviderHealth> CheckHealthAsync(CancellationToken cancellationToken)
    {
        var connectionConfigured = ProviderType switch
        {
            DataProviderType.PostgreSql => !string.IsNullOrWhiteSpace(_settings.PostgreSqlConnectionString),
            _ => !string.IsNullOrWhiteSpace(_settings.SqlServerConnectionString)
        };

        var errors = connectionConfigured
            ? Array.Empty<string>()
            : [$"{ProviderType} connection string is not configured yet. Configure it in DataProvider settings or environment variables."];

        return Task.FromResult(new DataProviderHealth(Name, connectionConfigured, string.Empty, [], errors));
    }

    public Task<IReadOnlyList<DataRow>> ReadRowsAsync(string schemaName, CancellationToken cancellationToken)
    {
        throw new DomainException("SQL provider is a documented placeholder. Configure EF Core DbContext and repositories in Infrastructure to activate SQL.");
    }

    public Task SaveRowsAsync(string schemaName, IReadOnlyCollection<DataRow> rows, CancellationToken cancellationToken)
    {
        throw new DomainException("SQL provider is a documented placeholder. Configure EF Core DbContext and repositories in Infrastructure to activate SQL.");
    }

    public Task<IReadOnlyList<T>> QueryAsync<T>(DataQuery query, CancellationToken cancellationToken)
    {
        throw new DomainException("SQL provider is a documented placeholder. Configure EF Core DbContext and repositories in Infrastructure to activate SQL.");
    }

    public Task SaveChangesAsync(UnitOfWorkChanges changes, CancellationToken cancellationToken)
    {
        throw new DomainException("SQL provider is a documented placeholder. Configure EF Core DbContext and repositories in Infrastructure to activate SQL.");
    }
}

