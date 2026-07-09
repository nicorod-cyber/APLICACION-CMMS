using MaintenanceCMMS.Application.Abstractions.Data;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Domain.Enums;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MaintenanceCMMS.Infrastructure.Data.Sql;

public sealed class SqlDataProvider : IDataProvider
{
    private readonly DataProviderSettings _settings;
    private readonly CmmsDbContext? _dbContext;

    public SqlDataProvider(IOptions<DataProviderSettings> settings, CmmsDbContext? dbContext = null)
    {
        _settings = settings.Value;
        _dbContext = dbContext;
    }

    public string Name => _settings.Provider;

    public DataProviderType ProviderType => Enum.TryParse<DataProviderType>(_settings.Provider, ignoreCase: true, out var type)
        ? type
        : DataProviderType.SqlServer;

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        return ProviderType == DataProviderType.PostgreSql && _dbContext is not null
            ? _dbContext.Database.MigrateAsync(cancellationToken)
            : Task.CompletedTask;
    }

    public async Task<DataProviderHealth> CheckHealthAsync(CancellationToken cancellationToken)
    {
        var connectionConfigured = ProviderType switch
        {
            DataProviderType.PostgreSql => !string.IsNullOrWhiteSpace(_settings.PostgreSqlConnectionString),
            _ => !string.IsNullOrWhiteSpace(_settings.SqlServerConnectionString)
        };

        var errors = new List<string>();
        if (!connectionConfigured)
        {
            errors.Add($"{ProviderType} connection string is not configured yet. Configure it in DataProvider settings or environment variables.");
        }

        if (ProviderType == DataProviderType.PostgreSql)
        {
            if (_dbContext is null)
            {
                errors.Add("CmmsDbContext is not registered. Check PostgreSQL provider configuration.");
            }
            else
            {
                try
                {
                    var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
                    if (!canConnect)
                    {
                        errors.Add("PostgreSQL connection could not be opened.");
                    }

                    var pending = await _dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
                    foreach (var migration in pending)
                    {
                        errors.Add($"Pending migration: {migration}");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(ex.Message);
                }
            }
        }

        return new DataProviderHealth(Name, connectionConfigured && errors.Count == 0, string.Empty, [], errors);
    }

    public Task<IReadOnlyList<DataRow>> ReadRowsAsync(string schemaName, CancellationToken cancellationToken)
    {
        throw new DomainException("PostgreSQL provider is active, but schema-row access is intentionally disabled. Migrate this module to a typed PostgreSQL repository or DbContext query.");
    }

    public Task SaveRowsAsync(string schemaName, IReadOnlyCollection<DataRow> rows, CancellationToken cancellationToken)
    {
        throw new DomainException("PostgreSQL provider is active, but schema-row writes are intentionally disabled. Migrate this module to a typed PostgreSQL repository or DbContext command.");
    }

    public Task<IReadOnlyList<T>> QueryAsync<T>(DataQuery query, CancellationToken cancellationToken)
    {
        throw new DomainException("PostgreSQL provider is active, but generic SQL queries are not implemented. Use typed repositories or DbContext queries.");
    }

    public Task SaveChangesAsync(UnitOfWorkChanges changes, CancellationToken cancellationToken)
    {
        throw new DomainException("PostgreSQL provider is active, but generic unit-of-work changes are not implemented. Use typed repositories or DbContext transactions.");
    }
}

