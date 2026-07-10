using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql.Configurations;

public sealed class InventoryCatalogConfiguration : IEntityTypeConfiguration<InventoryCatalogEntity>
{
    public void Configure(EntityTypeBuilder<InventoryCatalogEntity> b)
    {
        b.ToTable("catalogos_inventario"); b.ConfigureBase();
        b.Property(x => x.Category).HasColumnName("categoria").HasMaxLength(80).IsRequired(); b.Property(x => x.Code).HasColumnName("codigo").HasMaxLength(80).IsRequired(); b.Property(x => x.Name).HasColumnName("nombre").HasMaxLength(160).IsRequired(); b.Property(x => x.Description).HasColumnName("descripcion").HasMaxLength(500); b.Property(x => x.IsActive).HasColumnName("activo"); b.Property(x => x.SortOrder).HasColumnName("orden");
        b.HasIndex(x => new { x.Category, x.Code }).IsUnique();
    }
}

public sealed class WarehouseConfiguration : IEntityTypeConfiguration<WarehouseEntity>
{
    public void Configure(EntityTypeBuilder<WarehouseEntity> b)
    {
        b.ToTable("bodegas"); b.ConfigureBase();
        b.Property(x => x.Code).HasColumnName("codigo").HasMaxLength(80).IsRequired(); b.Property(x => x.Name).HasColumnName("nombre").HasMaxLength(240).IsRequired(); b.Property(x => x.FaenaId).HasColumnName("faena_id"); b.Property(x => x.TypeId).HasColumnName("tipo_id"); b.Property(x => x.Location).HasColumnName("ubicacion").HasMaxLength(300); b.Property(x => x.ResponsibleUserId).HasColumnName("responsable_usuario_id").HasMaxLength(120); b.Property(x => x.IsActive).HasColumnName("activo"); b.Property(x => x.AllowsNegativeStock).HasColumnName("permite_stock_negativo"); b.Property(x => x.CreatedByUserId).HasColumnName("creado_por_usuario_id").HasMaxLength(120); b.Property(x => x.UpdatedByUserId).HasColumnName("actualizado_por_usuario_id").HasMaxLength(120);
        b.HasIndex(x => x.Code).IsUnique(); b.HasIndex(x => x.FaenaId); b.HasOne(x => x.Faena).WithMany().HasForeignKey(x => x.FaenaId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.Type).WithMany().HasForeignKey(x => x.TypeId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class WarehouseLocationConfiguration : IEntityTypeConfiguration<WarehouseLocationEntity>
{
    public void Configure(EntityTypeBuilder<WarehouseLocationEntity> b)
    {
        b.ToTable("ubicaciones_bodega"); b.ConfigureBase(); b.Property(x => x.WarehouseId).HasColumnName("bodega_id"); b.Property(x => x.Code).HasColumnName("codigo").HasMaxLength(80).IsRequired(); b.Property(x => x.Name).HasColumnName("nombre").HasMaxLength(160).IsRequired(); b.Property(x => x.Description).HasColumnName("descripcion").HasMaxLength(500); b.Property(x => x.Aisle).HasColumnName("pasillo").HasMaxLength(80); b.Property(x => x.Shelf).HasColumnName("estante").HasMaxLength(80); b.Property(x => x.Level).HasColumnName("nivel").HasMaxLength(80); b.Property(x => x.Position).HasColumnName("posicion").HasMaxLength(80); b.Property(x => x.IsActive).HasColumnName("activo"); b.HasOne(x => x.Warehouse).WithMany(x => x.Locations).HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict); b.HasIndex(x => new { x.WarehouseId, x.Code }).IsUnique();
    }
}

public sealed class SparePartConfiguration : IEntityTypeConfiguration<SparePartEntity>
{
    public void Configure(EntityTypeBuilder<SparePartEntity> b)
    {
        b.ToTable("repuestos"); b.ConfigureBase(); b.Property(x => x.Code).HasColumnName("codigo").HasMaxLength(80).IsRequired(); b.Property(x => x.SapCode).HasColumnName("codigo_sap").HasMaxLength(120); b.Property(x => x.SupplierCode).HasColumnName("codigo_proveedor").HasMaxLength(120); b.Property(x => x.Description).HasColumnName("descripcion").HasMaxLength(300).IsRequired(); b.Property(x => x.TechnicalDescription).HasColumnName("descripcion_tecnica").HasMaxLength(1000).IsRequired(); b.Property(x => x.UnitId).HasColumnName("unidad_id"); b.Property(x => x.CategoryId).HasColumnName("categoria_id"); b.Property(x => x.Manufacturer).HasColumnName("fabricante").HasMaxLength(160); b.Property(x => x.ModelReference).HasColumnName("modelo_referencia").HasMaxLength(160); b.Property(x => x.IsCritical).HasColumnName("critico"); b.Property(x => x.MinimumStock).HasColumnName("stock_minimo").HasColumnType("numeric(14,2)"); b.Property(x => x.MaximumStock).HasColumnName("stock_maximo").HasColumnType("numeric(14,2)"); b.Property(x => x.ReorderPoint).HasColumnName("punto_reposicion").HasColumnType("numeric(14,2)"); b.Property(x => x.LeadTimeDays).HasColumnName("lead_time_dias"); b.Property(x => x.AverageUnitCost).HasColumnName("costo_unitario_promedio").HasColumnType("numeric(14,2)"); b.Property(x => x.Status).HasColumnName("estado").HasMaxLength(40); b.Property(x => x.PreferredSupplier).HasColumnName("proveedor_preferente").HasMaxLength(160); b.Property(x => x.ReplacementCode).HasColumnName("reemplazo_codigo").HasMaxLength(80); b.Property(x => x.CreatedByUserId).HasColumnName("creado_por_usuario_id").HasMaxLength(120); b.Property(x => x.UpdatedByUserId).HasColumnName("actualizado_por_usuario_id").HasMaxLength(120);
        b.HasIndex(x => x.Code).IsUnique(); b.HasIndex(x => x.SapCode).IsUnique().HasFilter("codigo_sap IS NOT NULL"); b.HasOne(x => x.Unit).WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict); b.ToTable(t => t.HasCheckConstraint("ck_repuestos_stocks", "stock_minimo >= 0 AND stock_maximo >= 0 AND punto_reposicion >= 0"));
    }
}

public sealed class WarehouseStockConfiguration : IEntityTypeConfiguration<WarehouseStockEntity>
{
    public void Configure(EntityTypeBuilder<WarehouseStockEntity> b)
    {
        b.ToTable("stock_bodega"); b.ConfigureBase(); b.Property(x => x.SparePartId).HasColumnName("repuesto_id"); b.Property(x => x.WarehouseId).HasColumnName("bodega_id"); b.Property(x => x.WarehouseLocationId).HasColumnName("ubicacion_bodega_id"); b.Property(x => x.PhysicalQuantity).HasColumnName("cantidad_fisica").HasColumnType("numeric(14,2)"); b.Property(x => x.ReservedQuantity).HasColumnName("cantidad_reservada").HasColumnType("numeric(14,2)"); b.Property(x => x.MinimumStockOverride).HasColumnName("stock_minimo_especifico").HasColumnType("numeric(14,2)"); b.HasOne(x => x.SparePart).WithMany().HasForeignKey(x => x.SparePartId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.WarehouseLocation).WithMany().HasForeignKey(x => x.WarehouseLocationId).OnDelete(DeleteBehavior.Restrict); b.HasIndex(x => new { x.SparePartId, x.WarehouseId, x.WarehouseLocationId }).IsUnique(); b.ToTable(t => t.HasCheckConstraint("ck_stock_bodega_saldos", "cantidad_fisica >= 0 AND cantidad_reservada >= 0 AND cantidad_reservada <= cantidad_fisica"));
    }
}

public sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovementEntity>
{
    public void Configure(EntityTypeBuilder<StockMovementEntity> b)
    {
        b.ToTable("movimientos_stock"); b.ConfigureBase(); b.Property(x => x.MovementNumber).HasColumnName("numero_movimiento").HasMaxLength(80).IsRequired(); b.Property(x => x.MovementTypeId).HasColumnName("tipo_movimiento_id"); b.Property(x => x.SparePartId).HasColumnName("repuesto_id"); b.Property(x => x.Quantity).HasColumnName("cantidad").HasColumnType("numeric(14,2)"); b.Property(x => x.SourceWarehouseId).HasColumnName("bodega_origen_id"); b.Property(x => x.TargetWarehouseId).HasColumnName("bodega_destino_id"); b.Property(x => x.ReservationId).HasColumnName("reserva_id"); b.Property(x => x.TransferId).HasColumnName("transferencia_id"); b.Property(x => x.WorkOrderId).HasColumnName("orden_trabajo_id"); b.Property(x => x.ReferenceType).HasColumnName("tipo_referencia").HasMaxLength(80); b.Property(x => x.ReferenceId).HasColumnName("referencia_id").HasMaxLength(120); b.Property(x => x.Reason).HasColumnName("motivo").HasMaxLength(500); b.Property(x => x.UserId).HasColumnName("usuario_id").HasMaxLength(120); b.Property(x => x.OccurredAtUtc).HasColumnName("fecha_utc").HasColumnType("timestamptz"); b.Property(x => x.PhysicalBefore).HasColumnName("fisico_anterior").HasColumnType("numeric(14,2)"); b.Property(x => x.PhysicalAfter).HasColumnName("fisico_nuevo").HasColumnType("numeric(14,2)"); b.Property(x => x.ReservedBefore).HasColumnName("reservado_anterior").HasColumnType("numeric(14,2)"); b.Property(x => x.ReservedAfter).HasColumnName("reservado_nuevo").HasColumnType("numeric(14,2)"); b.Property(x => x.IsReversed).HasColumnName("anulado"); b.Property(x => x.ReversalOfMovementId).HasColumnName("movimiento_reverso_de_id"); b.HasIndex(x => x.MovementNumber).IsUnique(); b.HasIndex(x => new { x.SparePartId, x.OccurredAtUtc }); b.HasOne(x => x.MovementType).WithMany().HasForeignKey(x => x.MovementTypeId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.SparePart).WithMany().HasForeignKey(x => x.SparePartId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.SourceWarehouse).WithMany().HasForeignKey(x => x.SourceWarehouseId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.TargetWarehouse).WithMany().HasForeignKey(x => x.TargetWarehouseId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.Reservation).WithMany().HasForeignKey(x => x.ReservationId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.Transfer).WithMany().HasForeignKey(x => x.TransferId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.WorkOrder).WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Restrict); b.ToTable(t => t.HasCheckConstraint("ck_movimientos_stock_cantidad", "cantidad > 0"));
    }
}

public sealed class StockReservationConfiguration : IEntityTypeConfiguration<StockReservationEntity>
{
    public void Configure(EntityTypeBuilder<StockReservationEntity> b)
    {
        b.ToTable("reservas_stock"); b.ConfigureBase(); b.Property(x => x.ReservationNumber).HasColumnName("codigo").HasMaxLength(80).IsRequired(); b.Property(x => x.SparePartId).HasColumnName("repuesto_id"); b.Property(x => x.WarehouseId).HasColumnName("bodega_id"); b.Property(x => x.RequestedQuantity).HasColumnName("cantidad_solicitada").HasColumnType("numeric(14,2)"); b.Property(x => x.ReservedQuantity).HasColumnName("cantidad_reservada").HasColumnType("numeric(14,2)"); b.Property(x => x.DeliveredQuantity).HasColumnName("cantidad_entregada").HasColumnType("numeric(14,2)"); b.Property(x => x.ReleasedQuantity).HasColumnName("cantidad_liberada").HasColumnType("numeric(14,2)"); b.Property(x => x.WorkOrderId).HasColumnName("orden_trabajo_id"); b.Property(x => x.WorkOrderNumber).HasColumnName("orden_trabajo_numero").HasMaxLength(80); b.Property(x => x.RequestedBy).HasColumnName("solicitante").HasMaxLength(120); b.Property(x => x.Status).HasColumnName("estado").HasMaxLength(40); b.Property(x => x.Reason).HasColumnName("motivo").HasMaxLength(500); b.Property(x => x.CancellationReason).HasColumnName("motivo_anulacion").HasMaxLength(500); b.Property(x => x.CreatedByUserId).HasColumnName("creado_por_usuario_id").HasMaxLength(120); b.Property(x => x.DeliveredAtUtc).HasColumnName("entregado_at_utc").HasColumnType("timestamptz"); b.Property(x => x.ReleasedAtUtc).HasColumnName("liberado_at_utc").HasColumnType("timestamptz"); b.HasIndex(x => x.ReservationNumber).IsUnique(); b.HasOne(x => x.SparePart).WithMany().HasForeignKey(x => x.SparePartId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.WorkOrder).WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Restrict); b.ToTable(t => t.HasCheckConstraint("ck_reservas_stock_cantidades", "cantidad_solicitada > 0 AND cantidad_reservada >= 0 AND cantidad_entregada >= 0 AND cantidad_liberada >= 0 AND cantidad_entregada + cantidad_liberada <= cantidad_reservada"));
    }
}

public sealed class StockTransferConfiguration : IEntityTypeConfiguration<StockTransferEntity>
{
    public void Configure(EntityTypeBuilder<StockTransferEntity> b)
    {
        b.ToTable("transferencias_stock"); b.ConfigureBase(); b.Property(x => x.TransferNumber).HasColumnName("codigo").HasMaxLength(80).IsRequired(); b.Property(x => x.SourceWarehouseId).HasColumnName("bodega_origen_id"); b.Property(x => x.TransitWarehouseId).HasColumnName("bodega_transito_id"); b.Property(x => x.TargetWarehouseId).HasColumnName("bodega_destino_id"); b.Property(x => x.SparePartId).HasColumnName("repuesto_id"); b.Property(x => x.Quantity).HasColumnName("cantidad").HasColumnType("numeric(14,2)"); b.Property(x => x.Status).HasColumnName("estado").HasMaxLength(40); b.Property(x => x.Reason).HasColumnName("motivo").HasMaxLength(500); b.Property(x => x.RequestedByUserId).HasColumnName("solicitado_por_usuario_id").HasMaxLength(120); b.Property(x => x.RequestedAtUtc).HasColumnName("solicitado_at_utc").HasColumnType("timestamptz"); b.Property(x => x.ReceivedAtUtc).HasColumnName("recibido_at_utc").HasColumnType("timestamptz"); b.Property(x => x.ReceivedByUserId).HasColumnName("recibido_por_usuario_id").HasMaxLength(120); b.Property(x => x.ReceptionReason).HasColumnName("motivo_recepcion").HasMaxLength(500); b.Property(x => x.CancellationReason).HasColumnName("motivo_anulacion").HasMaxLength(500); b.HasIndex(x => x.TransferNumber).IsUnique(); b.HasOne(x => x.SourceWarehouse).WithMany().HasForeignKey(x => x.SourceWarehouseId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.TransitWarehouse).WithMany().HasForeignKey(x => x.TransitWarehouseId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.TargetWarehouse).WithMany().HasForeignKey(x => x.TargetWarehouseId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.SparePart).WithMany().HasForeignKey(x => x.SparePartId).OnDelete(DeleteBehavior.Restrict); b.ToTable(t => t.HasCheckConstraint("ck_transferencias_stock_bodegas", "bodega_origen_id <> bodega_destino_id AND cantidad > 0"));
    }
}
