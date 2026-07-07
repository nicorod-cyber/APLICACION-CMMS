using MaintenanceCMMS.Application.Abstractions.Data;
using MaintenanceCMMS.Domain.Common;

namespace MaintenanceCMMS.Infrastructure.Data.Excel;

public sealed class ExcelRepository<T> : IExcelRepository<T>
    where T : Entity
{
    public Task<T?> GetByIdAsync(EntityId id, CancellationToken cancellationToken)
    {
        return Task.FromResult<T?>(null);
    }

    public Task<IReadOnlyList<T>> ListAsync(ISpecification<T> specification, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<T>>([]);
    }

    public Task AddAsync(T entity, CancellationToken cancellationToken)
    {
        // Entity-specific Excel mappers will be added by each module prompt. Schema-level Excel IO is implemented in ExcelDataProvider.
        return Task.CompletedTask;
    }

    public Task UpdateAsync(T entity, CancellationToken cancellationToken)
    {
        // Entity-specific Excel mappers will be added by each module prompt. Schema-level Excel IO is implemented in ExcelDataProvider.
        return Task.CompletedTask;
    }
}

