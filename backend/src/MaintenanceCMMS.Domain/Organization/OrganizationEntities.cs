using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Domain.Common.ValueObjects;

namespace MaintenanceCMMS.Domain.Organization;

public sealed class Company : AuditableEntity
{
    public Company(string name, string taxId)
    {
        DomainGuard.AgainstEmpty(name, nameof(name));
        DomainGuard.AgainstEmpty(taxId, nameof(taxId));
        Name = name.Trim();
        TaxId = taxId.Trim();
    }

    public string Name { get; private set; }

    public string TaxId { get; private set; }
}

public sealed class Faena : AuditableEntity
{
    public Faena(EntityId companyId, string name, EntityCode code)
    {
        DomainGuard.AgainstEmpty(name, nameof(name));
        CompanyId = companyId;
        Name = name.Trim();
        Code = code;
    }

    public EntityId CompanyId { get; private set; }

    public string Name { get; private set; }

    public EntityCode Code { get; private set; }
}

public sealed class TechnicalLocation : AuditableEntity
{
    public TechnicalLocation(EntityId faenaId, string name, EntityCode code)
    {
        DomainGuard.AgainstEmpty(name, nameof(name));
        FaenaId = faenaId;
        Name = name.Trim();
        Code = code;
    }

    public EntityId FaenaId { get; private set; }

    public EntityId? ParentLocationId { get; private set; }

    public string Name { get; private set; }

    public EntityCode Code { get; private set; }
}

public sealed class Contract : AuditableEntity
{
    public Contract(EntityId faenaId, string number, DateRange validity)
    {
        DomainGuard.AgainstEmpty(number, nameof(number));
        FaenaId = faenaId;
        Number = number.Trim();
        Validity = validity;
    }

    public EntityId FaenaId { get; private set; }

    public string Number { get; private set; }

    public DateRange Validity { get; private set; }
}

public sealed class ContractAssetRequirement : AuditableEntity
{
    public ContractAssetRequirement(EntityId contractId, EntityId assetTypeId, decimal minimumAvailabilityPercentage)
    {
        DomainGuard.AgainstNegative(minimumAvailabilityPercentage, nameof(minimumAvailabilityPercentage));
        ContractId = contractId;
        AssetTypeId = assetTypeId;
        MinimumAvailabilityPercentage = minimumAvailabilityPercentage;
    }

    public EntityId ContractId { get; private set; }

    public EntityId AssetTypeId { get; private set; }

    public decimal MinimumAvailabilityPercentage { get; private set; }
}

