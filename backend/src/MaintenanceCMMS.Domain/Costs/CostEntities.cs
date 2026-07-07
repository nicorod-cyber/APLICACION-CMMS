using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Domain.Common.ValueObjects;
using MaintenanceCMMS.Domain.Enums;

namespace MaintenanceCMMS.Domain.Costs;

public sealed class CostEntry : AuditableEntity
{
    public CostEntry(EntityId workOrderId, CostType costType, Money amount)
    {
        WorkOrderId = workOrderId;
        CostType = costType;
        Amount = amount;
    }

    public EntityId WorkOrderId { get; private set; }

    public CostType CostType { get; private set; }

    public Money Amount { get; private set; }
}

public sealed class LaborRate : AuditableEntity
{
    public LaborRate(EntityId roleId, Money hourlyRate)
    {
        RoleId = roleId;
        HourlyRate = hourlyRate;
    }

    public EntityId RoleId { get; private set; }

    public Money HourlyRate { get; private set; }
}

public sealed class ExternalService : AuditableEntity
{
    public ExternalService(EntityId workOrderId, EntityId supplierId, string description, Money amount)
    {
        DomainGuard.AgainstEmpty(description, nameof(description));
        WorkOrderId = workOrderId;
        SupplierId = supplierId;
        Description = description.Trim();
        Amount = amount;
    }

    public EntityId WorkOrderId { get; private set; }

    public EntityId SupplierId { get; private set; }

    public string Description { get; private set; }

    public Money Amount { get; private set; }
}

public sealed class PaymentStatement : AuditableEntity
{
    public PaymentStatement(EntityId supplierId, string statementNumber, Money amount)
    {
        DomainGuard.AgainstEmpty(statementNumber, nameof(statementNumber));
        SupplierId = supplierId;
        StatementNumber = statementNumber.Trim();
        Amount = amount;
    }

    public EntityId SupplierId { get; private set; }

    public string StatementNumber { get; private set; }

    public Money Amount { get; private set; }
}

