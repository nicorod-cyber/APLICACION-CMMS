using MaintenanceCMMS.Domain.Common;

namespace MaintenanceCMMS.Domain.Reporting;

public sealed class KpiDefinition : AuditableEntity
{
    public KpiDefinition(string code, string name, string reportingDataset)
    {
        DomainGuard.AgainstEmpty(code, nameof(code));
        DomainGuard.AgainstEmpty(name, nameof(name));
        DomainGuard.AgainstEmpty(reportingDataset, nameof(reportingDataset));
        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        ReportingDataset = reportingDataset.Trim();
    }

    public string Code { get; private set; }

    public string Name { get; private set; }

    public string ReportingDataset { get; private set; }
}

public sealed class KpiTarget : AuditableEntity
{
    public KpiTarget(EntityId kpiDefinitionId, EntityId faenaId, decimal targetValue)
    {
        KpiDefinitionId = kpiDefinitionId;
        FaenaId = faenaId;
        TargetValue = targetValue;
    }

    public EntityId KpiDefinitionId { get; private set; }

    public EntityId FaenaId { get; private set; }

    public decimal TargetValue { get; private set; }
}

public sealed class DashboardWidget : AuditableEntity
{
    public DashboardWidget(string name, string widgetType)
    {
        DomainGuard.AgainstEmpty(name, nameof(name));
        DomainGuard.AgainstEmpty(widgetType, nameof(widgetType));
        Name = name.Trim();
        WidgetType = widgetType.Trim();
    }

    public string Name { get; private set; }

    public string WidgetType { get; private set; }
}

public sealed class UserDashboardWidget : AuditableEntity
{
    public UserDashboardWidget(EntityId userId, EntityId dashboardWidgetId, int sortOrder)
    {
        UserId = userId;
        DashboardWidgetId = dashboardWidgetId;
        SortOrder = sortOrder;
    }

    public EntityId UserId { get; private set; }

    public EntityId DashboardWidgetId { get; private set; }

    public int SortOrder { get; private set; }
}

