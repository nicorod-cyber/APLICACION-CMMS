using MaintenanceCMMS.Application.Auth;

namespace MaintenanceCMMS.Application.TechnicalHierarchy;

public sealed record TechnicalHierarchyExcelImportRequest(
    string SistemasComponentesPath,
    string UbicacionesTecnicasPath);

public sealed record TechnicalHierarchyExcelImportCommand(
    string SistemasComponentesPath,
    string UbicacionesTecnicasPath,
    string ImportedBy);

public sealed record TechnicalHierarchyExcelImportResult(
    IReadOnlyCollection<string> ArchivosProcesados,
    int FilasLeidas,
    int RegistrosInsertados,
    int RegistrosActualizados,
    int RegistrosOmitidos,
    IReadOnlyCollection<string> Advertencias,
    IReadOnlyCollection<string> Errores,
    IReadOnlyCollection<string> ReferenciasNoEncontradas);

public interface ITechnicalHierarchyExcelImportService
{
    Task<TechnicalHierarchyExcelImportResult> ImportAsync(
        TechnicalHierarchyExcelImportCommand command,
        UserAccessContext user,
        CancellationToken cancellationToken);
}