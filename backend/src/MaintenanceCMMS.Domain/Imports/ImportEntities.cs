using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Domain.Enums;

namespace MaintenanceCMMS.Domain.Imports;

public sealed class ImportBatch : AuditableEntity
{
    public ImportBatch(string fileName, string entityName)
    {
        DomainGuard.AgainstEmpty(fileName, nameof(fileName));
        DomainGuard.AgainstEmpty(entityName, nameof(entityName));
        FileName = fileName.Trim();
        EntityName = entityName.Trim();
        Status = ImportStatus.Draft;
    }

    public string FileName { get; private set; }

    public string EntityName { get; private set; }

    public ImportStatus Status { get; private set; }
}

public sealed class ImportBatchRow : AuditableEntity
{
    public ImportBatchRow(EntityId importBatchId, int rowNumber, string rawData)
    {
        if (rowNumber <= 0)
        {
            throw new DomainException("Import row number must be positive.");
        }

        ImportBatchId = importBatchId;
        RowNumber = rowNumber;
        RawData = rawData;
    }

    public EntityId ImportBatchId { get; private set; }

    public int RowNumber { get; private set; }

    public string RawData { get; private set; }
}

public sealed class ImportValidationError : AuditableEntity
{
    public ImportValidationError(EntityId importBatchRowId, string columnName, string message)
    {
        DomainGuard.AgainstEmpty(columnName, nameof(columnName));
        DomainGuard.AgainstEmpty(message, nameof(message));
        ImportBatchRowId = importBatchRowId;
        ColumnName = columnName.Trim();
        Message = message.Trim();
    }

    public EntityId ImportBatchRowId { get; private set; }

    public string ColumnName { get; private set; }

    public string Message { get; private set; }
}

public sealed class ImportApproval : AuditableEntity
{
    public ImportApproval(EntityId importBatchId, EntityId approvedByUserId, bool approved, string? comments = null)
    {
        ImportBatchId = importBatchId;
        ApprovedByUserId = approvedByUserId;
        Approved = approved;
        Comments = comments;
    }

    public EntityId ImportBatchId { get; private set; }

    public EntityId ApprovedByUserId { get; private set; }

    public bool Approved { get; private set; }

    public string? Comments { get; private set; }
}

