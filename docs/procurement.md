# Abastecimiento, OC y lead time

El modulo de abastecimiento conecta solicitudes CMMS con proveedores, OC, recepciones y entregas a OT.

## Datos Excel

- `proveedores.xlsx`: maestro de proveedores con lead time esperado y estado.
- `abastecimiento_solicitudes.xlsx`: solicitud interna CMMS, solicitud externa, OC, proveedor, costos, documentos y fechas del flujo.
- `ordenes_compra.xlsx`: historial auditable de referencias de OC.
- `recepciones_abastecimiento.xlsx`: recepciones, despachos directos y movimientos de inventario asociados.

## API

- `GET/POST/PUT /api/procurement/suppliers`
- `GET/POST /api/procurement/requests`
- `POST /api/procurement/requests/{id}/purchase-order`
- `POST /api/procurement/requests/{id}/receptions`
- `POST /api/procurement/requests/{id}/delivery`

## Reglas

- La solicitud a abastecimiento la crea supervisor de bodega, admin o usuario con permiso de ajuste de stock.
- La recepcion con repuesto codificado registra entrada de stock.
- El despacho directo registra entrada y consumo hacia OT/activo/faena en el mismo flujo.
- Costos y documentos de respaldo quedan en la solicitud y en los eventos de OC/recepcion.
- Lead time se calcula por etapas: solicitud-aprobacion, aprobacion-envio, envio-OC, OC-recepcion, recepcion-entrega y total.
