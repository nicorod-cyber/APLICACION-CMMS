using MaintenanceCMMS.Domain.Common;

namespace MaintenanceCMMS.Application.Abstractions.Data;

public interface IRepository<T>
    where T : Entity
{
    Task<T?> GetByIdAsync(EntityId id, CancellationToken cancellationToken);

    Task<IReadOnlyList<T>> ListAsync(ISpecification<T> specification, CancellationToken cancellationToken);

    Task AddAsync(T entity, CancellationToken cancellationToken);

    Task UpdateAsync(T entity, CancellationToken cancellationToken);
}

public interface IExcelRepository<T> : IRepository<T>
    where T : Entity
{
}

public interface ISqlRepository<T> : IRepository<T>
    where T : Entity
{
}
