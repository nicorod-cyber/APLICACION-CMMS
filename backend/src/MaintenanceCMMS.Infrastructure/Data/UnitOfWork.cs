using MaintenanceCMMS.Application.Abstractions.Data;

namespace MaintenanceCMMS.Infrastructure.Data;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly IDataProvider _dataProvider;

    public UnitOfWork(IDataProvider dataProvider)
    {
        _dataProvider = dataProvider;
    }

    public Task SaveChangesAsync(string reason, CancellationToken cancellationToken)
    {
        return _dataProvider.SaveChangesAsync(new UnitOfWorkChanges(reason, [], [], []), cancellationToken);
    }
}

