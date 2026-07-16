namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;

public sealed class TechnicalLocationEntity : PostgreSqlEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid FaenaId { get; set; }
    public FaenaEntity Faena { get; set; } = null!;
    public bool IsObsolete { get; set; }
}

public sealed class TechnicalNodeEntity : PostgreSqlEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public TechnicalNodeEntity? Parent { get; set; }
    public List<TechnicalNodeEntity> Children { get; set; } = [];
    public Guid? FaenaId { get; set; }
    public FaenaEntity? Faena { get; set; }
    public bool IsObsolete { get; set; }
    public Guid? MergedIntoNodeId { get; set; }
    public TechnicalNodeEntity? MergedIntoNode { get; set; }
    public string? CreatedByUserId { get; set; }
    public string? UpdatedByUserId { get; set; }
    public List<TechnicalNodeFamilyEntity> Families { get; set; } = [];
    public List<TechnicalNodeAssetEntity> Assets { get; set; } = [];
    public List<TechnicalNodeAliasEntity> Aliases { get; set; } = [];
}

public sealed class TechnicalNodeFamilyEntity : PostgreSqlEntity
{
    public Guid TechnicalNodeId { get; set; }
    public TechnicalNodeEntity TechnicalNode { get; set; } = null!;
    public Guid EquipmentFamilyId { get; set; }
    public EquipmentFamilyEntity EquipmentFamily { get; set; } = null!;
}

public sealed class TechnicalNodeAssetEntity : PostgreSqlEntity
{
    public Guid TechnicalNodeId { get; set; }
    public TechnicalNodeEntity TechnicalNode { get; set; } = null!;
    public Guid AssetId { get; set; }
    public AssetEntity Asset { get; set; } = null!;
}

public sealed class TechnicalNodeAliasEntity : PostgreSqlEntity
{
    public Guid TechnicalNodeId { get; set; }
    public TechnicalNodeEntity TechnicalNode { get; set; } = null!;
    public string Alias { get; set; } = string.Empty;
    public string NormalizedAlias { get; set; } = string.Empty;
    public string Source { get; set; } = "Manual";
}