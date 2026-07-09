namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;

public abstract class PostgreSqlEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public uint Version { get; set; }
}

public sealed class AppUserEntity : PostgreSqlEntity
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsLocked { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public List<UserRoleEntity> Roles { get; set; } = [];
    public List<UserFaenaEntity> Faenas { get; set; } = [];
}

public sealed class RoleEntity : PostgreSqlEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public List<RolePermissionEntity> Permissions { get; set; } = [];
}

public sealed class PermissionEntity : PostgreSqlEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public sealed class UserRoleEntity : PostgreSqlEntity
{
    public Guid UserId { get; set; }
    public AppUserEntity User { get; set; } = null!;
    public Guid RoleId { get; set; }
    public RoleEntity Role { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public string? AssignedByUserId { get; set; }
    public DateTimeOffset AssignedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UnassignedByUserId { get; set; }
    public DateTimeOffset? UnassignedAtUtc { get; set; }
    public string? UnassignedReason { get; set; }
}

public sealed class RolePermissionEntity : PostgreSqlEntity
{
    public Guid RoleId { get; set; }
    public RoleEntity Role { get; set; } = null!;
    public Guid PermissionId { get; set; }
    public PermissionEntity Permission { get; set; } = null!;
    public bool IsActive { get; set; } = true;
}

public sealed class UserFaenaEntity : PostgreSqlEntity
{
    public Guid UserId { get; set; }
    public AppUserEntity User { get; set; } = null!;
    public Guid FaenaId { get; set; }
    public FaenaEntity Faena { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset AssignedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? AssignedByUserId { get; set; }
    public DateTimeOffset? UnassignedAtUtc { get; set; }
    public string? UnassignedByUserId { get; set; }
    public string? UnassignedReason { get; set; }
}

public sealed class FaenaEntity : PostgreSqlEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public sealed class AssetOperationalStateEntity : PostgreSqlEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public sealed class EquipmentFamilyEntity : PostgreSqlEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public sealed class AssetEntity : PostgreSqlEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid FaenaId { get; set; }
    public FaenaEntity Faena { get; set; } = null!;
    public Guid FamilyId { get; set; }
    public EquipmentFamilyEntity Family { get; set; } = null!;
    public Guid OperationalStateId { get; set; }
    public AssetOperationalStateEntity OperationalState { get; set; } = null!;
    public string RecordStatus { get; set; } = "vigente";
    public string AssetType { get; set; } = string.Empty;
    public string? TechnicalLocationCode { get; set; }
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? Plate { get; set; }
    public string? SerialNumber { get; set; }
    public string? Ownership { get; set; }
    public string? Criticality { get; set; }
    public string? DocumentStatus { get; set; }
    public bool TechnicalSheetValidated { get; set; }
}

public sealed class AssetStateEventEntity : PostgreSqlEntity
{
    public Guid AssetId { get; set; }
    public AssetEntity Asset { get; set; } = null!;
    public Guid? PreviousStateId { get; set; }
    public AssetOperationalStateEntity? PreviousState { get; set; }
    public Guid NewStateId { get; set; }
    public AssetOperationalStateEntity NewState { get; set; } = null!;
    public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string UserId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public sealed class DocumentTypeEntity : PostgreSqlEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? AppliesTo { get; set; }
    public bool IsMandatory { get; set; }
    public bool IsCritical { get; set; }
    public bool BlocksAvailability { get; set; }
    public int AlertDays { get; set; } = 30;
    public string? ResponsibleRoles { get; set; }
    public bool RequiresAlertPdf { get; set; }
    public string? HtmlTemplateCode { get; set; }
    public bool IsActive { get; set; } = true;
    public string? CreatedByUserId { get; set; }
    public string? UpdatedByUserId { get; set; }
    public List<DocumentEntity> Documents { get; set; } = [];
}

public sealed class DocumentEntity : PostgreSqlEntity
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid DocumentTypeId { get; set; }
    public DocumentTypeEntity DocumentType { get; set; } = null!;
    public string Status { get; set; } = "PendienteCarga";
    public DateOnly? IssueDate { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    public bool IsCurrent { get; set; } = true;
    public bool IsAnnulled { get; set; }
    public string? AnnulledByUserId { get; set; }
    public DateTimeOffset? AnnulledAtUtc { get; set; }
    public string? AnnulReason { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public string? UpdatedByUserId { get; set; }
    public string? ValidatedByUserId { get; set; }
    public DateTimeOffset? ValidatedAtUtc { get; set; }
    public string? RejectedByUserId { get; set; }
    public DateTimeOffset? RejectedAtUtc { get; set; }
    public string? RejectReason { get; set; }
    public bool ExpiryDateValidated { get; set; }
    public Guid? ReplacesDocumentId { get; set; }
    public DocumentEntity? ReplacesDocument { get; set; }
    public Guid? ReplacedByDocumentId { get; set; }
    public DocumentEntity? ReplacedByDocument { get; set; }
    public bool IsHistorical { get; set; }
    public bool IsCritical { get; set; }
    public bool IsMandatory { get; set; }
    public bool BlocksAvailability { get; set; }
    public string? ChangeReason { get; set; }
    public List<DocumentVersionEntity> Versions { get; set; } = [];
    public List<DocumentAssetEntity> Assets { get; set; } = [];
    public List<DocumentFaenaEntity> Faenas { get; set; } = [];
}

public sealed class DocumentVersionEntity : PostgreSqlEntity
{
    public Guid DocumentId { get; set; }
    public DocumentEntity Document { get; set; } = null!;
    public int VersionNumber { get; set; }
    public string VersionCode { get; set; } = string.Empty;
    public Guid FileId { get; set; }
    public FileMetadataEntity File { get; set; } = null!;
    public DateTimeOffset UploadedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string UploadedByUserId { get; set; } = string.Empty;
    public string? Observations { get; set; }
    public bool IsCurrent { get; set; } = true;
    public string Status { get; set; } = "vigente";
}

public sealed class FileMetadataEntity : PostgreSqlEntity
{
    public string FileKey { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string LogicalUri { get; set; } = string.Empty;
    public string? LogicalPath { get; set; }
    public string? MimeType { get; set; }
    public long? SizeBytes { get; set; }
    public string? Checksum { get; set; }
    public string Status { get; set; } = "vigente";
    public string? MetadataJson { get; set; }
    public string? AuthorUserId { get; set; }
}

public sealed class DocumentAssetEntity : PostgreSqlEntity
{
    public Guid DocumentId { get; set; }
    public DocumentEntity Document { get; set; } = null!;
    public Guid AssetId { get; set; }
    public AssetEntity Asset { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset AssignedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? AssignedByUserId { get; set; }
    public DateTimeOffset? UnassignedAtUtc { get; set; }
    public string? UnassignedByUserId { get; set; }
    public string? UnassignedReason { get; set; }
}

public sealed class DocumentFaenaEntity : PostgreSqlEntity
{
    public Guid DocumentId { get; set; }
    public DocumentEntity Document { get; set; } = null!;
    public Guid FaenaId { get; set; }
    public FaenaEntity Faena { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset AssignedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? AssignedByUserId { get; set; }
    public DateTimeOffset? UnassignedAtUtc { get; set; }
    public string? UnassignedByUserId { get; set; }
    public string? UnassignedReason { get; set; }
}

public sealed class AuditLogEntity : PostgreSqlEntity
{
    public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string UserId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? FaenaCode { get; set; }
    public string Severity { get; set; } = "Low";
    public string? PreviousValue { get; set; }
    public string? NewValue { get; set; }
    public string? IpAddress { get; set; }
    public string? Device { get; set; }
    public string? Reason { get; set; }
    public bool Success { get; set; } = true;
    public string? Detail { get; set; }
    public string? CorrelationId { get; set; }
}
