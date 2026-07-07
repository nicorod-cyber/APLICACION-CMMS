namespace MaintenanceCMMS.Application.Abstractions.Data;

public interface ISpecification<in T>
{
    bool IsSatisfiedBy(T entity);
}

