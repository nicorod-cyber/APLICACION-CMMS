using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql.Configurations;

public sealed class TechnicalLocationConfiguration : IEntityTypeConfiguration<TechnicalLocationEntity>
{
    public void Configure(EntityTypeBuilder<TechnicalLocationEntity> builder)
    {
        builder.ToTable("ubicaciones_tecnicas");
        builder.ConfigureBase();
        builder.Property(entity => entity.Code).HasColumnName("codigo").HasMaxLength(120).IsRequired();
        builder.Property(entity => entity.Name).HasColumnName("nombre").HasMaxLength(240).IsRequired();
        builder.Property(entity => entity.FaenaId).HasColumnName("faena_id");
        builder.Property(entity => entity.IsObsolete).HasColumnName("obsoleto");
        builder.HasOne(entity => entity.Faena).WithOne(faena => faena.TechnicalLocation).HasForeignKey<TechnicalLocationEntity>(entity => entity.FaenaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => entity.Code).IsUnique();
        builder.HasIndex(entity => entity.FaenaId).IsUnique();
        builder.HasIndex(entity => entity.Name);
        builder.HasIndex(entity => entity.IsObsolete);
    }
}

public sealed class TechnicalNodeConfiguration : IEntityTypeConfiguration<TechnicalNodeEntity>
{
    public void Configure(EntityTypeBuilder<TechnicalNodeEntity> builder)
    {
        builder.ToTable("nodos_tecnicos");
        builder.ConfigureBase();
        builder.Property(entity => entity.Code).HasColumnName("codigo").HasMaxLength(120).IsRequired();
        builder.Property(entity => entity.Name).HasColumnName("nombre").HasMaxLength(240).IsRequired();
        builder.Property(entity => entity.NormalizedName).HasColumnName("nombre_normalizado").HasMaxLength(240).IsRequired();
        builder.Property(entity => entity.Level).HasColumnName("nivel").HasMaxLength(40).IsRequired();
        builder.Property(entity => entity.ParentId).HasColumnName("nodo_padre_id");
        builder.Property(entity => entity.FaenaId).HasColumnName("faena_id");
        builder.Property(entity => entity.IsObsolete).HasColumnName("obsoleto");
        builder.Property(entity => entity.MergedIntoNodeId).HasColumnName("fusionado_en_nodo_id");
        builder.Property(entity => entity.CreatedByUserId).HasColumnName("creado_por_usuario_id").HasMaxLength(120);
        builder.Property(entity => entity.UpdatedByUserId).HasColumnName("actualizado_por_usuario_id").HasMaxLength(120);
        builder.HasOne(entity => entity.Parent).WithMany(entity => entity.Children).HasForeignKey(entity => entity.ParentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.Faena).WithMany().HasForeignKey(entity => entity.FaenaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.MergedIntoNode).WithMany().HasForeignKey(entity => entity.MergedIntoNodeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => entity.Code).IsUnique();
        builder.HasIndex(entity => entity.ParentId);
        builder.HasIndex(entity => entity.Level);
        builder.HasIndex(entity => entity.FaenaId);
        builder.HasIndex(entity => entity.NormalizedName);
        builder.HasIndex(entity => entity.IsObsolete);
        builder.HasIndex(entity => entity.MergedIntoNodeId);
        builder.HasIndex(entity => new { entity.ParentId, entity.Level, entity.NormalizedName });
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_nodos_tecnicos_nivel", "nivel IN ('Sistema','Subsistema','Componente','Subcomponente')");
            table.HasCheckConstraint("ck_nodos_tecnicos_no_self_parent", "nodo_padre_id IS NULL OR nodo_padre_id <> id");
            table.HasCheckConstraint("ck_nodos_tecnicos_no_self_merge", "fusionado_en_nodo_id IS NULL OR fusionado_en_nodo_id <> id");
        });
    }
}

public sealed class TechnicalNodeFamilyConfiguration : IEntityTypeConfiguration<TechnicalNodeFamilyEntity>
{
    public void Configure(EntityTypeBuilder<TechnicalNodeFamilyEntity> builder)
    {
        builder.ToTable("nodo_tecnico_familias");
        builder.ConfigureBase();
        builder.Property(entity => entity.TechnicalNodeId).HasColumnName("nodo_tecnico_id");
        builder.Property(entity => entity.EquipmentFamilyId).HasColumnName("familia_equipo_id");
        builder.HasOne(entity => entity.TechnicalNode).WithMany(node => node.Families).HasForeignKey(entity => entity.TechnicalNodeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.EquipmentFamily).WithMany().HasForeignKey(entity => entity.EquipmentFamilyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.TechnicalNodeId, entity.EquipmentFamilyId }).IsUnique();
        builder.HasIndex(entity => entity.EquipmentFamilyId);
    }
}

public sealed class TechnicalNodeAssetConfiguration : IEntityTypeConfiguration<TechnicalNodeAssetEntity>
{
    public void Configure(EntityTypeBuilder<TechnicalNodeAssetEntity> builder)
    {
        builder.ToTable("nodo_tecnico_activos");
        builder.ConfigureBase();
        builder.Property(entity => entity.TechnicalNodeId).HasColumnName("nodo_tecnico_id");
        builder.Property(entity => entity.AssetId).HasColumnName("activo_id");
        builder.HasOne(entity => entity.TechnicalNode).WithMany(node => node.Assets).HasForeignKey(entity => entity.TechnicalNodeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.Asset).WithMany().HasForeignKey(entity => entity.AssetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.TechnicalNodeId, entity.AssetId }).IsUnique();
        builder.HasIndex(entity => entity.AssetId);
    }
}

public sealed class TechnicalNodeAliasConfiguration : IEntityTypeConfiguration<TechnicalNodeAliasEntity>
{
    public void Configure(EntityTypeBuilder<TechnicalNodeAliasEntity> builder)
    {
        builder.ToTable("nodo_tecnico_aliases");
        builder.ConfigureBase();
        builder.Property(entity => entity.TechnicalNodeId).HasColumnName("nodo_tecnico_id");
        builder.Property(entity => entity.Alias).HasColumnName("alias").HasMaxLength(240).IsRequired();
        builder.Property(entity => entity.NormalizedAlias).HasColumnName("alias_normalizado").HasMaxLength(240).IsRequired();
        builder.Property(entity => entity.Source).HasColumnName("origen").HasMaxLength(80).IsRequired();
        builder.HasOne(entity => entity.TechnicalNode).WithMany(node => node.Aliases).HasForeignKey(entity => entity.TechnicalNodeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.TechnicalNodeId, entity.NormalizedAlias }).IsUnique();
        builder.HasIndex(entity => entity.NormalizedAlias);
    }
}