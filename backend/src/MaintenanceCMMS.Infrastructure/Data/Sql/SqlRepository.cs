using MaintenanceCMMS.Application.Abstractions.Data;
using MaintenanceCMMS.Domain.Common;

namespace MaintenanceCMMS.Infrastructure.Data.Sql;

public sealed class SqlRepository<T> : ISqlRepository<T>
    where T : Entity
{
    public Task<T?> GetByIdAsync(EntityId id, CancellationToken cancellationToken)
    {
        throw new DomainException("SQL repository placeholder: add EF Core DbContext mappings here when DataProvider is SqlServer or PostgreSql.");
    }

    public Task<IReadOnlyList<T>> ListAsync(ISpecification<T> specification, CancellationToken cancellationToken)
    {
        throw new DomainException("SQL repository placeholder: add EF Core DbContext mappings here when DataProvider is SqlServer or PostgreSql.");
    }

    public Task AddAsync(T entity, CancellationToken cancellationToken)
    {
        throw new DomainException("SQL repository placeholder: add EF Core DbContext mappings here when DataProvider is SqlServer or PostgreSql.");
    }

    public Task UpdateAsync(T entity, CancellationToken cancellationToken)
    {
        throw new DomainException("SQL repository placeholder: add EF Core DbContext mappings here when DataProvider is SqlServer or PostgreSql.");
    }
}

