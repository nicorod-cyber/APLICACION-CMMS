using MaintenanceCMMS.Domain.Common;

namespace MaintenanceCMMS.Domain.Scheduling;

public sealed class SchedulePlan : AuditableEntity
{
    public SchedulePlan(EntityId faenaId, DateOnly startDate, DateOnly endDate)
    {
        if (endDate < startDate)
        {
            throw new DomainException("Schedule end date cannot be before start date.");
        }

        FaenaId = faenaId;
        StartDate = startDate;
        EndDate = endDate;
    }

    public EntityId FaenaId { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }
}

public sealed class ScheduleItem : AuditableEntity
{
    public ScheduleItem(EntityId schedulePlanId, EntityId workOrderId, DateTimeOffset startsAt, DateTimeOffset endsAt)
    {
        if (endsAt <= startsAt)
        {
            throw new DomainException("Schedule item end must be after start.");
        }

        SchedulePlanId = schedulePlanId;
        WorkOrderId = workOrderId;
        StartsAt = startsAt;
        EndsAt = endsAt;
    }

    public EntityId SchedulePlanId { get; private set; }

    public EntityId WorkOrderId { get; private set; }

    public DateTimeOffset StartsAt { get; private set; }

    public DateTimeOffset EndsAt { get; private set; }
}

public sealed class Workshop : AuditableEntity
{
    public Workshop(EntityId faenaId, string name)
    {
        DomainGuard.AgainstEmpty(name, nameof(name));
        FaenaId = faenaId;
        Name = name.Trim();
    }

    public EntityId FaenaId { get; private set; }

    public string Name { get; private set; }
}

public sealed class WorkshopCapacity : AuditableEntity
{
    public WorkshopCapacity(EntityId workshopId, DateOnly date, decimal availableHours)
    {
        DomainGuard.AgainstNegative(availableHours, nameof(availableHours));
        WorkshopId = workshopId;
        Date = date;
        AvailableHours = availableHours;
    }

    public EntityId WorkshopId { get; private set; }

    public DateOnly Date { get; private set; }

    public decimal AvailableHours { get; private set; }
}

public sealed class GanttDependency : AuditableEntity
{
    public GanttDependency(EntityId predecessorScheduleItemId, EntityId successorScheduleItemId)
    {
        if (predecessorScheduleItemId == successorScheduleItemId)
        {
            throw new DomainException("A Gantt item cannot depend on itself.");
        }

        PredecessorScheduleItemId = predecessorScheduleItemId;
        SuccessorScheduleItemId = successorScheduleItemId;
    }

    public EntityId PredecessorScheduleItemId { get; private set; }

    public EntityId SuccessorScheduleItemId { get; private set; }
}

