using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Domain.Enums;

namespace MaintenanceCMMS.Domain.WorkOrders;

public sealed class WorkNotice : AuditableEntity
{
    public WorkNotice(EntityId assetId, string description, MaintenanceType maintenanceType)
    {
        DomainGuard.AgainstEmpty(description, nameof(description));
        AssetId = assetId;
        Description = description.Trim();
        MaintenanceType = maintenanceType;
        Status = WorkNoticeStatus.Draft;
    }

    public EntityId AssetId { get; private set; }

    public string Description { get; private set; }

    public MaintenanceType MaintenanceType { get; private set; }

    public WorkNoticeStatus Status { get; private set; }
}

/// <summary>
/// Work order aggregate root for tasks, technicians, labor, evidence, checklists and signatures.
/// </summary>
public sealed class WorkOrder : AuditableEntity
{
    private readonly List<WorkOrderTask> _tasks = [];

    private readonly List<WorkOrderEvidence> _evidences = [];

    private readonly List<LaborEntry> _laborEntries = [];

    public WorkOrder(EntityId assetId, MaintenanceType maintenanceType, string description)
    {
        DomainGuard.AgainstEmpty(description, nameof(description));
        AssetId = assetId;
        MaintenanceType = maintenanceType;
        Description = description.Trim();
        Status = WorkOrderStatus.Draft;
    }

    public EntityId AssetId { get; private set; }

    public MaintenanceType MaintenanceType { get; private set; }

    public string Description { get; private set; }

    public WorkOrderStatus Status { get; private set; }

    public IReadOnlyCollection<WorkOrderTask> Tasks => _tasks.AsReadOnly();

    public IReadOnlyCollection<WorkOrderEvidence> Evidences => _evidences.AsReadOnly();

    public IReadOnlyCollection<LaborEntry> LaborEntries => _laborEntries.AsReadOnly();

    public void AddTask(WorkOrderTask task)
    {
        if (task.WorkOrderId != Id)
        {
            throw new DomainException("Task does not belong to this work order.");
        }

        _tasks.Add(task);
    }

    public void AddEvidence(WorkOrderEvidence evidence)
    {
        if (evidence.WorkOrderId != Id)
        {
            throw new DomainException("Evidence does not belong to this work order.");
        }

        _evidences.Add(evidence);
    }

    public void AddLaborEntry(LaborEntry laborEntry)
    {
        if (laborEntry.WorkOrderId != Id)
        {
            throw new DomainException("Labor entry does not belong to this work order.");
        }

        _laborEntries.Add(laborEntry);
    }

    public void Close(string userId)
    {
        var taskIdsRequiringEvidence = _tasks
            .Where(task => task.RequiresEvidence)
            .Select(task => task.Id)
            .ToHashSet();

        var taskIdsWithEvidence = _evidences
            .Where(evidence => evidence.TaskId.HasValue)
            .Select(evidence => evidence.TaskId!.Value)
            .ToHashSet();

        if (taskIdsRequiringEvidence.Any(taskId => !taskIdsWithEvidence.Contains(taskId)))
        {
            Status = WorkOrderStatus.PendingEvidence;
            throw new DomainException("Work order cannot close because required evidence is missing.");
        }

        var taskIdsRequiringLabor = _tasks
            .Where(task => task.RequiresLabor)
            .Select(task => task.Id)
            .ToHashSet();

        var taskIdsWithLabor = _laborEntries
            .Where(entry => entry.TaskId.HasValue && entry.Hours > 0)
            .Select(entry => entry.TaskId!.Value)
            .ToHashSet();

        if (taskIdsRequiringLabor.Any(taskId => !taskIdsWithLabor.Contains(taskId)))
        {
            Status = WorkOrderStatus.PendingLabor;
            throw new DomainException("Work order cannot close because mandatory labor hours are missing.");
        }

        Status = WorkOrderStatus.Closed;
        Touch(userId);
    }
}

public sealed class WorkOrderTask : AuditableEntity
{
    public WorkOrderTask(EntityId workOrderId, string description, bool requiresEvidence = false, bool requiresLabor = true)
    {
        DomainGuard.AgainstEmpty(description, nameof(description));
        WorkOrderId = workOrderId;
        Description = description.Trim();
        RequiresEvidence = requiresEvidence;
        RequiresLabor = requiresLabor;
    }

    public EntityId WorkOrderId { get; private set; }

    public string Description { get; private set; }

    public bool RequiresEvidence { get; private set; }

    public bool RequiresLabor { get; private set; }
}

public sealed class WorkOrderTaskTechnician : AuditableEntity
{
    public WorkOrderTaskTechnician(EntityId taskId, EntityId technicianUserId)
    {
        TaskId = taskId;
        TechnicianUserId = technicianUserId;
    }

    public EntityId TaskId { get; private set; }

    public EntityId TechnicianUserId { get; private set; }
}

public sealed class WorkOrderStatusHistory : AuditableEntity
{
    public WorkOrderStatusHistory(EntityId workOrderId, WorkOrderStatus fromStatus, WorkOrderStatus toStatus)
    {
        WorkOrderId = workOrderId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
    }

    public EntityId WorkOrderId { get; private set; }

    public WorkOrderStatus FromStatus { get; private set; }

    public WorkOrderStatus ToStatus { get; private set; }
}

public sealed class WorkOrderEvidence : AuditableEntity
{
    public WorkOrderEvidence(EntityId workOrderId, string fileKey, EntityId? taskId = null)
    {
        DomainGuard.AgainstEmpty(fileKey, nameof(fileKey));
        WorkOrderId = workOrderId;
        TaskId = taskId;
        FileKey = fileKey.Trim();
    }

    public EntityId WorkOrderId { get; private set; }

    public EntityId? TaskId { get; private set; }

    public string FileKey { get; private set; }
}

public sealed class WorkOrderChecklist : AuditableEntity
{
    public WorkOrderChecklist(EntityId workOrderId, string name)
    {
        DomainGuard.AgainstEmpty(name, nameof(name));
        WorkOrderId = workOrderId;
        Name = name.Trim();
    }

    public EntityId WorkOrderId { get; private set; }

    public string Name { get; private set; }
}

public sealed class WorkOrderChecklistItem : AuditableEntity
{
    public WorkOrderChecklistItem(EntityId checklistId, string text, bool isRequired)
    {
        DomainGuard.AgainstEmpty(text, nameof(text));
        ChecklistId = checklistId;
        Text = text.Trim();
        IsRequired = isRequired;
    }

    public EntityId ChecklistId { get; private set; }

    public string Text { get; private set; }

    public bool IsRequired { get; private set; }

    public bool IsCompleted { get; private set; }
}

public sealed class WorkOrderSignature : AuditableEntity
{
    public WorkOrderSignature(EntityId workOrderId, EntityId userId, string signatureFileKey)
    {
        DomainGuard.AgainstEmpty(signatureFileKey, nameof(signatureFileKey));
        WorkOrderId = workOrderId;
        UserId = userId;
        SignatureFileKey = signatureFileKey.Trim();
    }

    public EntityId WorkOrderId { get; private set; }

    public EntityId UserId { get; private set; }

    public string SignatureFileKey { get; private set; }
}

public sealed class LaborEntry : AuditableEntity
{
    public LaborEntry(EntityId workOrderId, EntityId technicianUserId, decimal hours, EntityId? taskId = null)
    {
        DomainGuard.AgainstNegative(hours, nameof(hours));
        WorkOrderId = workOrderId;
        TaskId = taskId;
        TechnicianUserId = technicianUserId;
        Hours = hours;
    }

    public EntityId WorkOrderId { get; private set; }

    public EntityId? TaskId { get; private set; }

    public EntityId TechnicianUserId { get; private set; }

    public decimal Hours { get; private set; }
}

