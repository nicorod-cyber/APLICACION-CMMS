using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

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
    public DbSet<AuditLogEntity> AuditLogs => Set<AuditLogEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("pgcrypto");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CmmsDbContext).Assembly);
    }
}
