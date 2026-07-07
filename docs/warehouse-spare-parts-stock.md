# Bodega, repuestos, stock y movimientos

El modulo implementa maestro de repuestos, bodegas, stock por bodega, reservas, transferencias, devoluciones, ajustes y movimientos auditados.

## Archivos Excel

- `repuestos.xlsx`: maestro de repuestos con codigo CMMS automatico, SAP unico opcional, proveedor, descripcion tecnica, familia compatible, fabricante, modelo, criticidad, parametros de stock, lead time, costo promedio y estado.
- `bodegas.xlsx`: bodegas centrales, taller, faena o material en transito, con ubicaciones internas opcionales.
- `stock_bodegas.xlsx`: saldo por bodega y repuesto. `StockDisponible` se calcula como `StockFisico - StockReservado`.
- `stock_movements.xlsx`: historial auditable de recepciones, consumos, reservas, liberaciones, transferencias, ajustes y conteos.
- `stock_reservations.xlsx`: estado operacional de reservas para OT, cantidades entregadas/liberadas y saldo pendiente.
- `stock_transfers.xlsx`: estado de transferencias entre bodegas, bodega de transito, recepcion y movimientos asociados.

## Reglas

- El codigo CMMS de repuesto se genera como `REP-000001`, `REP-000002`, etc.
- `CodigoSap` debe ser unico cuando existe.
- Si no existe `CodigoSap`, el repuesto queda marcado como no codificado.
- El stock no se edita directo desde UI operativa; se registra un movimiento.
- No se permite stock negativo salvo que la bodega lo permita y el movimiento marque excepcion auditada.
- Los costos se devuelven solo a roles con permiso de costos.
- La reserva aumenta `StockReservado` y reduce disponible, pero no descuenta fisico.
- La entrega fisica a OT, activo, faena o centro de costo descuenta `StockFisico` y exige referencia operativa.
- La transferencia registra salida de origen, entrada a bodega `Transito`, recepcion destino y salida desde transito.
- La devolucion desde OT/activo aumenta stock solo cuando se marca como reutilizable.
- Los ajustes positivos/negativos exigen motivo; si requieren aprobacion supervisor, queda indicado el aprobador en la referencia.
- La baja de material descuenta stock fisico y queda auditada como movimiento.

## Endpoints

- `GET /api/inventory/dashboard`
- `GET /api/inventory/warehouses`
- `POST /api/inventory/warehouses`
- `GET /api/inventory/spare-parts`
- `GET /api/inventory/spare-parts/{code}`
- `POST /api/inventory/spare-parts`
- `PUT /api/inventory/spare-parts/{code}`
- `GET /api/inventory/stock`
- `GET /api/inventory/stock/movements`
- `POST /api/inventory/stock/movements`
- `GET /api/inventory/reservations`
- `POST /api/inventory/reservations`
- `POST /api/inventory/reservations/{reservationId}/release`
- `POST /api/inventory/deliveries`
- `GET /api/inventory/transfers`
- `POST /api/inventory/transfers`
- `POST /api/inventory/transfers/{transferId}/receive`
- `POST /api/inventory/returns`
- `POST /api/inventory/adjustments`
- `POST /api/inventory/write-offs`

## Frontend

- `/bodega` muestra un kanban logistico con reservas, transferencias en transito y recepcionadas.
- El formulario de operacion permite reservar, entregar, transferir, recepcionar, devolver, ajustar, dar baja o registrar un movimiento manual.
- La misma pagina muestra historial de movimientos y stock por bodega.

## Migracion a SQL

La logica vive en `IInventoryService`; el cambio futuro debe reemplazar lectura/escritura Excel por repositorios SQL manteniendo los contratos de aplicacion. Las tablas candidatas son `Warehouses`, `SpareParts`, `WarehouseStock`, `StockMovements`, `StockReservations` y `StockTransfers`.
