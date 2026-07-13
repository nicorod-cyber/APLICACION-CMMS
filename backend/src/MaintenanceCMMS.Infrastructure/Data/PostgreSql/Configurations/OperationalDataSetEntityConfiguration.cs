using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql.Configurations;

public sealed class OperationalDataSetEntityConfiguration : IEntityTypeConfiguration<OperationalDataSetEntity>
{
    public void Configure(EntityTypeBuilder<OperationalDataSetEntity> builder)
    {
        builder.ToTable("conjuntos_datos_operacionales");
        builder.ConfigureBase();
        builder.Property(x => x.Code).HasColumnName("codigo").HasMaxLength(120).IsRequired();
        builder.Property(x => x.Payload).HasColumnName("contenido").HasColumnType("jsonb").IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
    }
}
