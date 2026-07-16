namespace MaintenanceCMMS.Application.Auth;

public static class AuthRoles
{
    public const string Admin = "admin";
    public const string Planner = "planificador";
    public const string MaintenanceSupervisor = "supervisor_mantenimiento";
    public const string Technician = "tecnico";
    public const string Warehouse = "bodeguero";
    public const string WarehouseSupervisor = "supervisor_bodega";
    public const string Management = "gerencia";
    public const string FaenaViewer = "consulta_faena";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Admin,
        Planner,
        MaintenanceSupervisor,
        Technician,
        Warehouse,
        WarehouseSupervisor,
        Management,
        FaenaViewer
    };
}

public static class AuthPermissions
{
    public const string Administration = "administracion";
    public const string ManageUsers = "usuarios.gestionar";
    public const string AssignFaenas = "faenas.asignar";
    public const string ViewFaenas = "faenas.ver";
    public const string CreateFaenas = "faenas.crear";
    public const string EditFaenas = "faenas.editar";
    public const string DeactivateFaenas = "faenas.desactivar";
    public const string ApproveImports = "importaciones.aprobar";
    public const string ChangeAssetFaena = "activos.cambiar_faena";
    public const string ManageEquipmentFamilies = "familias_equipo.gestionar";
    public const string ManageAssetCatalogs = "activos.catalogos.administrar";
    public const string ManageAssetAttributes = "activos.atributos.administrar";
    public const string RegisterAssetReadings = "activos.lecturas.registrar";
    public const string CorrectAssetReadings = "activos.lecturas.corregir";
    public const string ViewOperationalUnits = "unidades_operativas.ver";
    public const string ManageOperationalUnits = "unidades_operativas.administrar";
    public const string ManageOperationalUnitComposition = "unidades_operativas.composicion";
    public const string ManageDocumentRequirements = "documentos.requisitos.administrar";
    public const string ManageTechnicalHierarchy = "jerarquia.gestionar";
    public const string ManageDocuments = "documentos.gestionar";
    public const string ValidateDocuments = "documentos.validar";
    public const string ConfigureDocumentTypes = "documentos.configurar";
    public const string ChangeValidatedDocumentExpiry = "documentos.vencimiento_validado.modificar";
    public const string ManageAlerts = "alertas.gestionar";
    public const string ConfigureAlerts = "alertas.configurar";
    public const string AdjustStock = "stock.ajustar";
    public const string CloseWorkOrders = "ot.cerrar";
    public const string FinalValidateWorkOrders = "ot.validar_final";
    public const string ViewCosts = "costos.ver";
    public const string ViewGlobalWarehouses = "bodegas.global";
    public const string ViewAssignedWorkOrders = "ot.ver_asignadas";
}
