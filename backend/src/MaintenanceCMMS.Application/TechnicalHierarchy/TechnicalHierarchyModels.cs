namespace MaintenanceCMMS.Application.TechnicalHierarchy;

public enum TechnicalHierarchyLevel
{
    Sistema = 0,
    Subsistema = 1,
    Componente = 2,
    Subcomponente = 3
}

public sealed record TechnicalHierarchyQuery(
    string? FaenaCodigo = null,
    string? Familia = null,
    string? SistemaCodigo = null,
    TechnicalHierarchyLevel? Nivel = null,
    bool IncludeObsolete = false);

public sealed record CreateTechnicalNodeRequest(
    string Codigo,
    string Nombre,
    TechnicalHierarchyLevel Nivel,
    string? CodigoPadre = null,
    string? FaenaCodigo = null,
    IReadOnlyCollection<string>? FamiliasEquipo = null,
    IReadOnlyCollection<string>? ActivosAsignados = null,
    IReadOnlyCollection<string>? AliasHistoricos = null);

public sealed record UpdateTechnicalNodeRequest(
    string Nombre,
    string? CodigoPadre = null,
    string? FaenaCodigo = null,
    IReadOnlyCollection<string>? FamiliasEquipo = null,
    IReadOnlyCollection<string>? ActivosAsignados = null,
    IReadOnlyCollection<string>? AliasHistoricos = null,
    string? Reason = null);

public sealed record TechnicalNodeResponse(
    string Codigo,
    string Nombre,
    string NombreNormalizado,
    TechnicalHierarchyLevel Nivel,
    string? CodigoPadre,
    string? FaenaCodigo,
    string? UbicacionTecnicaCodigo,
    IReadOnlyCollection<string> FamiliasEquipo,
    IReadOnlyCollection<string> ActivosAsignados,
    IReadOnlyCollection<string> AliasHistoricos,
    bool Obsoleto,
    string? FusionadoEnCodigo,
    DateTimeOffset? FechaCreacionUtc,
    DateTimeOffset? FechaActualizacionUtc,
    string Ruta,
    bool TieneHijos,
    bool EnUso);

public sealed record TechnicalHierarchyTreeNode(
    TechnicalNodeResponse Node,
    IReadOnlyCollection<TechnicalHierarchyTreeNode> Children);

public sealed record SimilarTechnicalNode(
    TechnicalNodeResponse Node,
    TechnicalNodeResponse Candidate,
    decimal Similarity,
    string Reason);

public sealed record MergeTechnicalNodesRequest(
    string SourceCode,
    string TargetCode,
    string Reason);

public sealed record BulkFamilyAssignmentRequest(
    IReadOnlyCollection<string> NodeCodes,
    IReadOnlyCollection<string> Families,
    bool Append = true);

public sealed record AssetAssignmentRequest(
    IReadOnlyCollection<string> AssetCodes,
    bool Append = true);

public sealed record MarkTechnicalNodeObsoleteRequest(string? Reason);
