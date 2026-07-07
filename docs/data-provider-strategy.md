# Estrategia de proveedores de datos

## Objetivo

Definir una capa de abstraccion para que el sistema opere inicialmente con Excel y pueda migrar luego a SQL Server o PostgreSQL sin rehacer la logica de negocio, el frontend ni los casos de uso.

## Regla central

Excel es un proveedor temporal de datos. El centro de la arquitectura son los contratos, servicios de aplicacion, entidades de dominio, validaciones, auditoria y repositorios desacoplados.

## Configuracion esperada

```json
{
  "DataProvider": {
    "Provider": "Excel",
    "ExcelPath": "data/excel",
    "SqlServerConnectionString": "",
    "PostgreSqlConnectionString": ""
  },
  "DataProviders": {
    "Excel": {
      "BasePath": "data/excel"
    },
    "SqlServer": {
      "ConnectionStringName": "CMMS_SQLSERVER"
    },
    "PostgreSql": {
      "ConnectionStringName": "CMMS_POSTGRESQL"
    }
  }
}
```

Valores validos para `DataProvider:Provider`:

- `Excel`
- `SqlServer`
- `PostgreSql`

Las rutas y cadenas de conexion definitivas deben resolverse desde configuracion por ambiente y variables de entorno. No se deben hardcodear rutas locales, usuarios, claves ni correos.

## Contratos conceptuales

```csharp
public interface IDataProvider
{
    string Name { get; }
    Task<IReadOnlyList<T>> QueryAsync<T>(DataQuery query, CancellationToken cancellationToken);
    Task SaveChangesAsync(UnitOfWorkChanges changes, CancellationToken cancellationToken);
}

public interface IRepository<T> where T : Entity
{
    Task<T?> GetByIdAsync(EntityId id, CancellationToken cancellationToken);
    Task<IReadOnlyList<T>> ListAsync(ISpecification<T> specification, CancellationToken cancellationToken);
    Task AddAsync(T entity, CancellationToken cancellationToken);
    Task UpdateAsync(T entity, CancellationToken cancellationToken);
}
```

Estos contratos son orientativos para los prompts tecnicos siguientes. La implementacion exacta debe ajustarse al modelo de dominio y a los patrones del backend cuando exista el proyecto C#.

## Implementaciones previstas

### `ExcelDataProvider`

- Lee y escribe archivos ubicados bajo `/data/excel/`.
- Es responsable de mapear hojas/rangos a entidades o DTOs de infraestructura.
- Debe validar estructura de archivos, columnas requeridas y tipos.
- Debe coordinarse con los importadores para que los cambios aprobados queden auditados.
- No debe contener reglas de negocio.
- Esta implementado con ClosedXML y `ExcelSchemaRegistry`.

### `SqlDataProvider`

- Usa Entity Framework Core.
- Debe soportar SQL Server y PostgreSQL mediante configuracion.
- Administra `DbContext`, transacciones, migraciones y mapeos.
- Mantiene los mismos contratos de repositorio usados por la capa Application.
- Debe exponer datasets o vistas de reporting para Power BI sin comprometer tablas operacionales.

## Flujo recomendado de acceso a datos

```text
Frontend
  -> API Controller
    -> Application Service / Use Case
      -> IRepository<T> / IDataProvider
        -> ExcelDataProvider o SqlDataProvider
          -> /data/excel o base SQL
```

## Flujo de importacion Excel

```text
Archivo Excel recibido
  -> Validacion tecnica de estructura
  -> Validacion de negocio mediante Application
  -> Previsualizacion de errores y cambios
  -> Aprobacion por usuario autorizado
  -> Aplicacion mediante repositorios
  -> Auditoria completa
```

## Puntos donde se modifica para usar SQL

1. Cambiar `DataProvider:Provider` desde `Excel` a `SqlServer` o `PostgreSql`.
2. Configurar variables de entorno para cadenas de conexion.
3. Registrar `SqlDataProvider` y repositorios SQL en inyeccion de dependencias.
4. Ejecutar migraciones de EF Core.
5. Poblar tablas desde archivos Excel aprobados o scripts de seed.
6. Mantener sin cambios los servicios de aplicacion, controllers y frontend.

## Reglas para documentos y adjuntos

- Los archivos adjuntos, evidencias, firmas, respaldos y documentos tecnicos no se guardan dentro de Excel.
- En modo local se usa `/data/sharepoint-simulated/`.
- `/data/sharepoint-simulator/` queda reservado como alias legacy del contexto inicial.
- En integracion real se usa SharePoint mediante adaptador configurable.
- La base de datos o Excel solo guardan metadatos: identificador, ruta/logical key, tipo, vencimiento, estado, entidad relacionada, usuario y auditoria.

## Reglas para Power BI

- Power BI debe consumir vistas o datasets de reporting.
- Los datasets deben estar pensados para lectura, agregacion y filtros por periodo, faena, activo, OT, bodega, proveedor y rol.
- Los reportes no deben depender de archivos Excel operacionales sin gobierno.
- En etapa SQL, las vistas de reporting deben versionarse y documentarse.

## Riesgos especificos del proveedor Excel

| Riesgo | Mitigacion |
| --- | --- |
| Bloqueos de archivo por multiples usuarios | Centralizar escrituras en backend y evitar edicion directa durante operacion. |
| Tipos de datos ambiguos | Validar columnas, formatos y catalogos antes de aplicar cambios. |
| Falta de transacciones reales | Aplicar cambios por lotes auditados y con backups/versionado de archivo cuando se implemente. |
| Escalabilidad limitada | Mantener Excel como etapa temporal y preparar migracion temprana a SQL. |
| Duplicidad de reglas en macros/planillas | Prohibir reglas de negocio en Excel; toda regla vive en Application/Domain. |
