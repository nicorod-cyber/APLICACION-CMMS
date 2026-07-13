using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql;

public sealed class CmmsDbContext : DbContext
{
    public CmmsDbContext(DbContextOptions<CmmsDbContext> options)
        : base(options)
    {
    }

    public DbSet<AppUserEntity> Users => Set<AppUserEntity>();
    public DbSet<RoleEntity> Roles => Set<RoleEntity>();
    public DbSet<PermissionEntity> Permissions => Set<PermissionEntity>();
    public DbSet<UserRoleEntity> UserRoles => Set<UserRoleEntity>();
    public DbSet<RolePermissionEntity> RolePermissions => Set<RolePermissionEntity>();
    public DbSet<UserFaenaEntity> UserFaenas => Set<UserFaenaEntity>();
    public DbSet<FaenaEntity> Faenas => Set<FaenaEntity>();
    public DbSet<AssetOperationalStateEntity> AssetOperationalStates => Set<AssetOperationalStateEntity>();
    public DbSet<EquipmentFamilyEntity> EquipmentFamilies => Set<EquipmentFamilyEntity>();
    public DbSet<AssetEntity> Assets => Set<AssetEntity>();
    public DbSet<AssetStateEventEntity> AssetStateEvents => Set<AssetStateEventEntity>();
    public DbSet<DocumentTypeEntity> DocumentTypes => Set<DocumentTypeEntity>();
    public DbSet<DocumentEntity> Documents => Set<DocumentEntity>();
    public DbSet<DocumentVersionEntity> DocumentVersions => Set<DocumentVersionEntity>();
    public DbSet<FileMetadataEntity> Files => Set<FileMetadataEntity>();
    public DbSet<DocumentAssetEntity> DocumentAssets => Set<DocumentAssetEntity>();
    public DbSet<DocumentFaenaEntity> DocumentFaenas => Set<DocumentFaenaEntity>();
    public DbSet<WorkCatalogEntity> WorkCatalogs => Set<WorkCatalogEntity>();
    public DbSet<WorkNotificationEntity> WorkNotifications => Set<WorkNotificationEntity>();
    public DbSet<WorkOrderEntity> WorkOrders => Set<WorkOrderEntity>();
    public DbSet<WorkOrderTaskEntity> WorkOrderTasks => Set<WorkOrderTaskEntity>();
    public DbSet<WorkOrderTaskTechnicianEntity> WorkOrderTaskTechnicians => Set<WorkOrderTaskTechnicianEntity>();
    public DbSet<WorkOrderLaborEntity> WorkOrderLabor => Set<WorkOrderLaborEntity>();
    public DbSet<WorkOrderEvidenceEntity> WorkOrderEvidences => Set<WorkOrderEvidenceEntity>();
    public DbSet<WorkOrderSparePartEntity> WorkOrderSpareParts => Set<WorkOrderSparePartEntity>();
    public DbSet<ChecklistTemplateEntity> ChecklistTemplates => Set<ChecklistTemplateEntity>();
    public DbSet<ChecklistTemplateItemEntity> ChecklistTemplateItems => Set<ChecklistTemplateItemEntity>();
    public DbSet<WorkOrderChecklistEntity> WorkOrderChecklist => Set<WorkOrderChecklistEntity>();
    public DbSet<WorkOrderSignatureEntity> WorkOrderSignatures => Set<WorkOrderSignatureEntity>();
    public DbSet<WorkOrderStatusHistoryEntity> WorkOrderStatusHistory => Set<WorkOrderStatusHistoryEntity>();
    public DbSet<DocumentWorkOrderEntity> DocumentWorkOrders => Set<DocumentWorkOrderEntity>();
    public DbSet<AuditLogEntity> AuditLogs => Set<AuditLogEntity>();
    public DbSet<InventoryCatalogEntity> InventoryCatalogs => Set<InventoryCatalogEntity>();
    public DbSet<WarehouseEntity> Warehouses => Set<WarehouseEntity>();
    public DbSet<WarehouseLocationEntity> WarehouseLocations => Set<WarehouseLocationEntity>();
    public DbSet<SparePartEntity> SpareParts => Set<SparePartEntity>();
    public DbSet<WarehouseStockEntity> WarehouseStocks => Set<WarehouseStockEntity>();
    public DbSet<StockMovementEntity> StockMovements => Set<StockMovementEntity>();
    public DbSet<StockReservationEntity> StockReservations => Set<StockReservationEntity>();
    public DbSet<StockTransferEntity> StockTransfers => Set<StockTransferEntity>();
    public DbSet<TechnicalLocationEntity> TechnicalLocations => Set<TechnicalLocationEntity>();
    public DbSet<TechnicalNodeEntity> TechnicalNodes => Set<TechnicalNodeEntity>();
    public DbSet<TechnicalNodeFamilyEntity> TechnicalNodeFamilies => Set<TechnicalNodeFamilyEntity>();
    public DbSet<TechnicalNodeAssetEntity> TechnicalNodeAssets => Set<TechnicalNodeAssetEntity>();
    public DbSet<TechnicalNodeAliasEntity> TechnicalNodeAliases => Set<TechnicalNodeAliasEntity>();
    public DbSet<PdfTemplateEntity> PdfTemplates => Set<PdfTemplateEntity>();
    public DbSet<AlertRuleEntity> AlertRules => Set<AlertRuleEntity>();
    public DbSet<AlertRuleRecipientEntity> AlertRuleRecipients => Set<AlertRuleRecipientEntity>();
    public DbSet<AlertEntity> Alerts => Set<AlertEntity>();
    public DbSet<NotificationEntity> Notifications => Set<NotificationEntity>();
    public DbSet<NotificationRecipientEntity> NotificationRecipients => Set<NotificationRecipientEntity>();
    public DbSet<NotificationAttemptEntity> NotificationAttempts => Set<NotificationAttemptEntity>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.ReplaceService<IMigrationsIdGenerator, CmmsLegacyMigrationsIdGenerator>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("pgcrypto");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CmmsDbContext).Assembly);
    }
}

