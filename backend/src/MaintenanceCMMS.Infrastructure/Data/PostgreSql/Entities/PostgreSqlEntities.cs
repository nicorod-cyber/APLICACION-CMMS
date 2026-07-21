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
    public List<FaenaEntity> ResponsibleFaenas { get; set; } = [];
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
    public string? Zone { get; set; }
    public string? Client { get; set; }
    public string? CostCenter { get; set; }
    public string? FaenaType { get; set; }
    public string? Region { get; set; }
    public string? Commune { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public Guid? ResponsibleUserId { get; set; }
    public AppUserEntity? ResponsibleUser { get; set; }
    public TechnicalLocationEntity? TechnicalLocation { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class AssetOperationalStateEntity : PostgreSqlEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Severity { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class AssetTypeEntity : PostgreSqlEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public bool IsMobile { get; set; }
    public bool IsMountable { get; set; }
    public bool CanBeCarrier { get; set; }
    public bool ControlsMaintenance { get; set; } = true;
    public bool ParticipatesInAvailability { get; set; } = true;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class EquipmentFamilyEntity : PostgreSqlEntity
{
    public Guid AssetTypeId { get; set; }
    public AssetTypeEntity AssetType { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ReferenceBrand { get; set; }
    public string? ReferenceModel { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class AssetEntity : PostgreSqlEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid AssetTypeId { get; set; }
    public AssetTypeEntity AssetTypeDefinition { get; set; } = null!;
    public Guid? FaenaId { get; set; }
    public FaenaEntity? Faena { get; set; }
    public Guid? FamilyId { get; set; }
    public EquipmentFamilyEntity? Family { get; set; }
    public Guid OperationalStateId { get; set; }
    public AssetOperationalStateEntity OperationalState { get; set; } = null!;
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public string? Ownership { get; set; }
    public string? Criticality { get; set; }
    public short? ManufacturingYear { get; set; }
    public DateOnly? AcquisitionDate { get; set; }
    public DateOnly? CommissioningDate { get; set; }
    public DateOnly? DecommissioningDate { get; set; }
    public string? UsageMeasurementType { get; set; }
    public string? Observations { get; set; }

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
    public string? ReferenceType { get; set; }
    public string? ReferenceId { get; set; }
}

public sealed class AssetTransferEntity : PostgreSqlEntity
{
    public Guid AssetId { get; set; }
    public AssetEntity Asset { get; set; } = null!;
    public Guid? OriginFaenaId { get; set; }
    public FaenaEntity? OriginFaena { get; set; }
    public Guid? DestinationFaenaId { get; set; }
    public FaenaEntity? DestinationFaena { get; set; }
    public Guid? OperationalUnitId { get; set; }
    public OperationalUnitEntity? OperationalUnit { get; set; }
    public DateTimeOffset EffectiveAtUtc { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTimeOffset RegisteredAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? Observations { get; set; }
}

public sealed class AssetLocationPeriodEntity : PostgreSqlEntity
{
    public Guid AssetId { get; set; }
    public AssetEntity Asset { get; set; } = null!;
    public Guid? FaenaId { get; set; }
    public FaenaEntity? Faena { get; set; }
    public DateTimeOffset ValidFromUtc { get; set; }
    public DateTimeOffset? ValidToUtc { get; set; }
    public Guid? TransferId { get; set; }
    public AssetTransferEntity? Transfer { get; set; }
}

public sealed class AssetIdentifierAliasEntity : PostgreSqlEntity
{
    public Guid AssetId { get; set; }
    public AssetEntity Asset { get; set; } = null!;
    public string IdentifierType { get; set; } = string.Empty;
    public string ScopeKey { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string NormalizedValue { get; set; } = string.Empty;
    public DateTimeOffset ValidFromUtc { get; set; }
    public DateTimeOffset? ValidToUtc { get; set; }
    public Guid? ReplacedByAliasId { get; set; }
    public AssetIdentifierAliasEntity? ReplacedByAlias { get; set; }
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
    public List<DocumentWorkOrderEntity> WorkOrders { get; set; } = [];
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
    public DateOnly? IssueDate { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    public string ValidationStatus { get; set; } = "PendienteValidacion";
    public string? ValidatedByUserId { get; set; }
    public DateTimeOffset? ValidatedAtUtc { get; set; }
    public string? RejectedByUserId { get; set; }
    public DateTimeOffset? RejectedAtUtc { get; set; }
    public string? RejectReason { get; set; }
    public Guid? ReplacesVersionId { get; set; }
    public DocumentVersionEntity? ReplacesVersion { get; set; }
    public string? CorrectionResponsibleUserId { get; set; }
    public string? CorrectionStatus { get; set; }
    public string? CorrectionObservation { get; set; }
    public Guid? CorrectionCycleId { get; set; }
}

public sealed class FileMetadataEntity : PostgreSqlEntity
{
    public string FileKey { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string StorageMode { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? FaenaCode { get; set; }
    public string? AssetCode { get; set; }
    public string? WorkOrderNumber { get; set; }
    public string LogicalUri { get; set; } = string.Empty;
    public string? LogicalPath { get; set; }
    public string? PhysicalLocation { get; set; }
    public string? MimeType { get; set; }
    public long? SizeBytes { get; set; }
    public string? Checksum { get; set; }
    public string Status { get; set; } = "vigente";
    public int FileVersion { get; set; } = 1;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
    public string? DeletedByUserId { get; set; }
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





public sealed class AssetAttributeDefinitionEntity : PostgreSqlEntity
{
    public Guid AssetTypeId { get; set; }
    public AssetTypeEntity AssetType { get; set; } = null!;
    public Guid? EquipmentFamilyId { get; set; }
    public EquipmentFamilyEntity? EquipmentFamily { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DataType { get; set; } = "TEXTO";
    public string? Unit { get; set; }
    public bool IsRequired { get; set; }
    public bool IsIdentifier { get; set; }
    public bool IsUnique { get; set; }
    public bool IsSearchable { get; set; }
    public bool IsFilterable { get; set; }
    public bool ShowInList { get; set; }
    public decimal? MinimumValue { get; set; }
    public decimal? MaximumValue { get; set; }
    public string? ValidationPattern { get; set; }
    public string? OptionsJson { get; set; }
    public string? DisplayGroup { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class AssetAttributeValueEntity : PostgreSqlEntity
{
    public Guid AssetId { get; set; }
    public AssetEntity Asset { get; set; } = null!;
    public Guid AttributeDefinitionId { get; set; }
    public AssetAttributeDefinitionEntity AttributeDefinition { get; set; } = null!;
    public string? TextValue { get; set; }
    public decimal? NumericValue { get; set; }
    public bool? BooleanValue { get; set; }
    public DateOnly? DateValue { get; set; }
    public string? Observations { get; set; }
}

public sealed class AssetReadingEntity : PostgreSqlEntity
{
    public Guid AssetId { get; set; }
    public AssetEntity Asset { get; set; } = null!;
    public DateTimeOffset ReadAtUtc { get; set; }
    public decimal Value { get; set; }
    public string Source { get; set; } = "MANUAL";
    public Guid? WorkOrderId { get; set; }
    public WorkOrderEntity? WorkOrder { get; set; }
    public string? RegisteredByUserId { get; set; }
    public string? EvidenceReference { get; set; }
    public string? Observations { get; set; }
    public bool IsCorrection { get; set; }
    public Guid? CorrectedReadingId { get; set; }
    public AssetReadingEntity? CorrectedReading { get; set; }
    public string? CorrectionReason { get; set; }
    public string? AuthorizedByUserId { get; set; }
    public bool IsAnomalous { get; set; }
    public string? ValidationMessage { get; set; }
}

public sealed class AssetDocumentRequirementEntity : PostgreSqlEntity
{
    public Guid AssetTypeId { get; set; }
    public AssetTypeEntity AssetType { get; set; } = null!;
    public Guid? EquipmentFamilyId { get; set; }
    public EquipmentFamilyEntity? EquipmentFamily { get; set; }
    public Guid DocumentTypeId { get; set; }
    public DocumentTypeEntity DocumentType { get; set; } = null!;
    public bool IsMandatory { get; set; }
    public bool IsCritical { get; set; }
    public bool BlocksAvailability { get; set; }
    public bool RequiresExpirationDate { get; set; }
    public int? AlertDays { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class DocumentRequirementMatrixEntity : PostgreSqlEntity
{
    public string Code { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public string Status { get; set; } = "VIGENTE";
    public Guid AssetTypeId { get; set; }
    public AssetTypeEntity AssetType { get; set; } = null!;
    public Guid? EquipmentFamilyId { get; set; }
    public EquipmentFamilyEntity? EquipmentFamily { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public string? ChangeReason { get; set; }
    public List<DocumentRequirementMatrixItemEntity> Items { get; set; } = [];
}

public sealed class DocumentRequirementMatrixItemEntity : PostgreSqlEntity
{
    public Guid MatrixId { get; set; }
    public DocumentRequirementMatrixEntity Matrix { get; set; } = null!;
    public Guid DocumentTypeId { get; set; }
    public DocumentTypeEntity DocumentType { get; set; } = null!;
    public bool IsMandatory { get; set; }
    public bool IsCritical { get; set; }
    public bool BlocksAvailability { get; set; }
    public bool RequiresExpirationDate { get; set; }
    public int AlertDays { get; set; } = 45;
}

public sealed class OperationalUnitTypeEntity : PostgreSqlEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool ParticipatesInAvailability { get; set; } = true;
    public bool IsActive { get; set; } = true;
}

public sealed class OperationalUnitEntity : PostgreSqlEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid OperationalUnitTypeId { get; set; }
    public OperationalUnitTypeEntity OperationalUnitType { get; set; } = null!;
    public Guid? FaenaId { get; set; }
    public FaenaEntity? Faena { get; set; }
    public Guid OperationalStateId { get; set; }
    public AssetOperationalStateEntity OperationalState { get; set; } = null!;
    public Guid? BaselineOperationalStateId { get; set; }
    public AssetOperationalStateEntity? BaselineOperationalState { get; set; }
    public Guid? DerivedFromAssetId { get; set; }
    public AssetEntity? DerivedFromAsset { get; set; }
    public string? DerivedStateReason { get; set; }
    public DateTimeOffset? DerivedStateCalculatedAtUtc { get; set; }
    public string? Criticality { get; set; }
    public DateOnly? CommissioningDate { get; set; }
    public DateOnly? DecommissioningDate { get; set; }
    public string? Observations { get; set; }
}

public sealed class OperationalUnitComponentRoleEntity : PostgreSqlEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsCritical { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class OperationalUnitCompositionRuleEntity : PostgreSqlEntity
{
    public Guid OperationalUnitTypeId { get; set; }
    public OperationalUnitTypeEntity OperationalUnitType { get; set; } = null!;
    public Guid ComponentRoleId { get; set; }
    public OperationalUnitComponentRoleEntity ComponentRole { get; set; } = null!;
    public int MinimumQuantity { get; set; }
    public int MaximumQuantity { get; set; }
    public bool IsMandatory { get; set; }
    public bool IsActive { get; set; } = true;
    public List<OperationalUnitCompositionRuleAllowedAssetEntity> AllowedAssets { get; set; } = [];
}

public sealed class OperationalUnitCompositionRuleAllowedAssetEntity : PostgreSqlEntity
{
    public Guid OperationalUnitCompositionRuleId { get; set; }
    public OperationalUnitCompositionRuleEntity OperationalUnitCompositionRule { get; set; } = null!;
    public Guid? AssetTypeId { get; set; }
    public AssetTypeEntity? AssetType { get; set; }
    public Guid? EquipmentFamilyId { get; set; }
    public EquipmentFamilyEntity? EquipmentFamily { get; set; }
}

public sealed class OperationalUnitComponentEntity : PostgreSqlEntity
{
    public Guid OperationalUnitId { get; set; }
    public OperationalUnitEntity OperationalUnit { get; set; } = null!;
    public Guid AssetId { get; set; }
    public AssetEntity Asset { get; set; } = null!;
    public Guid ComponentRoleId { get; set; }
    public OperationalUnitComponentRoleEntity ComponentRole { get; set; } = null!;
    public DateTimeOffset InstalledAtUtc { get; set; }
    public DateTimeOffset? RemovedAtUtc { get; set; }
    public Guid? InstallationWorkOrderId { get; set; }
    public WorkOrderEntity? InstallationWorkOrder { get; set; }
    public Guid? RemovalWorkOrderId { get; set; }
    public WorkOrderEntity? RemovalWorkOrder { get; set; }
    public string InstalledByUserId { get; set; } = string.Empty;
    public string? InstallationReason { get; set; }
    public string? RemovedByUserId { get; set; }
    public string? RemovalReason { get; set; }
    public string? CriticalRoleCode { get; set; }
    public string? Observations { get; set; }
}



