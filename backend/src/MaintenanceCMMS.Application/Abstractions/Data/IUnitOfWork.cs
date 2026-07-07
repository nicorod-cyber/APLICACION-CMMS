namespace MaintenanceCMMS.Application.Abstractions.Data;

public interface IUnitOfWork
{
    Task SaveChangesAsync(string reason, CancellationToken cancellationToken);
}

