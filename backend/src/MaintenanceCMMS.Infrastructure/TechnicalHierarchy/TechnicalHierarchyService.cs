using System.Globalization;
using System.Text;
using System.Text.Json;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.TechnicalHierarchy;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceCMMS.Infrastructure.TechnicalHierarchy;

public sealed class TechnicalHierarchyService : ITechnicalHierarchyService
{
    private readonly CmmsDbContext _db;
    private readonly IAuditService _audit;
    private readonly IAuthorizationPolicyService _auth;
    public TechnicalHierarchyService(CmmsDbContext db, IAuditService audit, IAuthorizationPolicyService auth){_db=db;_audit=audit;_auth=auth;}

    public async Task<IReadOnlyCollection<TechnicalNodeResponse>> ListAsync(TechnicalHierarchyQuery query, UserAccessContext user, CancellationToken ct)
    {
        await EnsureCanFilterByFaenaAsync(query.FaenaCodigo,user,ct);
        var all=await LoadAllAsync(ct);
        var q=BaseQuery().AsNoTracking();
        if(!query.IncludeObsolete)q=q.Where(x=>!x.IsObsolete);
        if(query.Nivel.HasValue){var level=query.Nivel.Value.ToString();q=q.Where(x=>x.Level==level);}        
        if(!string.IsNullOrWhiteSpace(query.FaenaCodigo)){var f=Code(query.FaenaCodigo)!;q=q.Where(x=>(x.Faena!=null&&x.Faena.Code.ToUpper()==f)||x.Assets.Any(a=>a.Asset.Faena.Code.ToUpper()==f));}
        if(!string.IsNullOrWhiteSpace(query.Familia)){var fam=Code(query.Familia)!;q=q.Where(x=>x.Families.Any(f=>f.EquipmentFamily.Code.ToUpper()==fam));}
        q=ApplyScope(q,user);
        var nodes=(await q.ToArrayAsync(ct)).Select(x=>ToResponse(x,all)).ToArray();
        if(!string.IsNullOrWhiteSpace(query.SistemaCodigo))nodes=nodes.Where(x=>Same(ResolveSystemCode(x.Codigo,all),query.SistemaCodigo)).ToArray();
        return nodes.OrderBy(x=>x.Ruta,StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<IReadOnlyCollection<TechnicalHierarchyTreeNode>> GetTreeAsync(TechnicalHierarchyQuery query, UserAccessContext user, CancellationToken ct)=>BuildTree(await ListAsync(query,user,ct));

    public async Task<TechnicalNodeResponse?> GetByCodeAsync(string code, UserAccessContext user, CancellationToken ct)
    {
        var c=Code(code)!;
        var node=await ApplyScope(BaseQuery().AsNoTracking(),user).SingleOrDefaultAsync(x=>x.Code.ToUpper()==c,ct);
        return node is null?null:ToResponse(node,await LoadAllAsync(ct));
    }

    public async Task<TechnicalNodeResponse> CreateAsync(CreateTechnicalNodeRequest r, UserAccessContext user, CancellationToken ct)
    {
        EnsureCanManage(user);Req(r.Codigo,nameof(r.Codigo));Req(r.Nombre,nameof(r.Nombre));var code=Code(r.Codigo)!;
        if(await _db.TechnicalNodes.AnyAsync(x=>x.Code.ToUpper()==code,ct))throw new DomainException($"Ya existe un nodo tecnico con codigo '{r.Codigo}'.");
        var parent=await ValidateParentAsync(null,r.Nivel,r.CodigoPadre,ct);var faena=await ResolveFaenaAsync(r.FaenaCodigo,user,ct);
        var fams=await ResolveFamiliesAsync(r.FamiliasEquipo??[],ct);var assets=await ResolveAssetsAsync(r.ActivosAsignados??[],user,ct);var norm=NormalizeName(r.Nombre);
        await EnsureNoDuplicateAsync(null,r.Nivel,parent?.Id,norm,ct);
        var node=new TechnicalNodeEntity{Code=code,Name=r.Nombre.Trim(),NormalizedName=norm,Level=r.Nivel.ToString(),ParentId=parent?.Id,FaenaId=faena?.Id,CreatedByUserId=user.UserId};
        foreach(var f in fams)node.Families.Add(new TechnicalNodeFamilyEntity{EquipmentFamilyId=f.Id});
        foreach(var a in assets)node.Assets.Add(new TechnicalNodeAssetEntity{AssetId=a.Id});
        AddAliases(node,r.AliasHistoricos??[],"Manual");_db.TechnicalNodes.Add(node);await _db.SaveChangesAsync(ct);DetachTrackedTechnicalHierarchy();
        await Audit(user,"Created",node.Code,null,JsonSerializer.Serialize(AuditShape(node)),"Nodo tecnico creado",ct);
        return (await GetByCodeAsync(node.Code,user,ct))!;
    }

    public async Task<TechnicalNodeResponse?> UpdateAsync(string code, UpdateTechnicalNodeRequest r, UserAccessContext user, CancellationToken ct)
    {
        EnsureCanManage(user);Req(r.Nombre,nameof(r.Nombre));var node=await BaseQuery(true).SingleOrDefaultAsync(x=>x.Code.ToUpper()==Code(code),ct);if(node is null)return null;
        var level=ParseLevel(node.Level);var parent=await ValidateParentAsync(node.Id,level,r.CodigoPadre,ct);var faena=await ResolveFaenaAsync(r.FaenaCodigo,user,ct);
        var fams=await ResolveFamiliesAsync(r.FamiliasEquipo??node.Families.Select(x=>x.EquipmentFamily.Code).ToArray(),ct);var assets=await ResolveAssetsAsync(r.ActivosAsignados??node.Assets.Select(x=>x.Asset.Code).ToArray(),user,ct);
        var norm=NormalizeName(r.Nombre);await EnsureNoDuplicateAsync(node.Id,level,parent?.Id,norm,ct);var prev=JsonSerializer.Serialize(AuditShape(node));var oldName=node.Name;
        node.Name=r.Nombre.Trim();node.NormalizedName=norm;node.ParentId=parent?.Id;node.FaenaId=faena?.Id;node.UpdatedByUserId=user.UserId;node.UpdatedAtUtc=DateTimeOffset.UtcNow;
        ReplaceFamilies(node,fams);ReplaceAssets(node,assets);if(!string.Equals(oldName,node.Name,StringComparison.OrdinalIgnoreCase))AddAliases(node,[oldName],"Rename");AddAliases(node,r.AliasHistoricos??[],"Manual");
        await _db.SaveChangesAsync(ct);await Audit(user,"Updated",node.Code,prev,JsonSerializer.Serialize(AuditShape(node)),r.Reason??"Nodo tecnico actualizado",ct);return await GetByCodeAsync(node.Code,user,ct);
    }

    public async Task<TechnicalNodeResponse?> MarkObsoleteAsync(string code, MarkTechnicalNodeObsoleteRequest r, UserAccessContext user, CancellationToken ct)
    {
        EnsureCanManage(user);var node=await _db.TechnicalNodes.SingleOrDefaultAsync(x=>x.Code.ToUpper()==Code(code),ct);if(node is null)return null;
        var prev=JsonSerializer.Serialize(new{node.Code,node.IsObsolete});node.IsObsolete=true;node.UpdatedByUserId=user.UserId;node.UpdatedAtUtc=DateTimeOffset.UtcNow;await _db.SaveChangesAsync(ct);
        await Audit(user,"MarkedObsolete",node.Code,prev,JsonSerializer.Serialize(new{node.Code,node.IsObsolete}),r.Reason??"Nodo tecnico marcado como obsoleto; no se elimina fisicamente.",ct);
        return await GetByCodeAsync(node.Code,user,ct);
    }

    public async Task<IReadOnlyCollection<SimilarTechnicalNode>> DetectSimilarAsync(TechnicalHierarchyQuery query, UserAccessContext user, CancellationToken ct)
    {
        var nodes=(await ListAsync(query with{IncludeObsolete=false},user,ct)).ToArray();var result=new List<SimilarTechnicalNode>();
        for(var i=0;i<nodes.Length;i++)for(var j=i+1;j<nodes.Length;j++){var l=nodes[i];var r=nodes[j];if(l.Nivel!=r.Nivel||!Same(l.CodigoPadre,r.CodigoPadre))continue;var sim=Similarity(l.NombreNormalizado,r.NombreNormalizado);var alias=l.AliasHistoricos.Any(a=>NormalizeName(a)==r.NombreNormalizado)||r.AliasHistoricos.Any(a=>NormalizeName(a)==l.NombreNormalizado);if(l.NombreNormalizado==r.NombreNormalizado||alias||sim>=0.82m)result.Add(new SimilarTechnicalNode(l,r,decimal.Round(sim,3),alias?"Alias historico coincidente":"Nombre normalizado similar"));}
        return result;
    }

    public async Task<TechnicalNodeResponse?> MergeAsync(MergeTechnicalNodesRequest r, UserAccessContext user, CancellationToken ct)
    {
        EnsureCanManage(user);Req(r.SourceCode,nameof(r.SourceCode));Req(r.TargetCode,nameof(r.TargetCode));Req(r.Reason,nameof(r.Reason));if(Same(r.SourceCode,r.TargetCode))throw new DomainException("El nodo origen y destino deben ser distintos.");DetachTrackedTechnicalHierarchy();
        await using var tx=await _db.Database.BeginTransactionAsync(ct);
        var sourceCode=Code(r.SourceCode)!;var targetCode=Code(r.TargetCode)!;
        var source=await _db.TechnicalNodes.SingleOrDefaultAsync(x=>x.Code.ToUpper()==sourceCode,ct);var target=await _db.TechnicalNodes.SingleOrDefaultAsync(x=>x.Code.ToUpper()==targetCode,ct);if(source is null||target is null)return null;
        if(!string.Equals(source.Level,target.Level,StringComparison.OrdinalIgnoreCase))throw new DomainException("Solo se pueden fusionar nodos del mismo nivel.");
        if(source.FaenaId.HasValue&&target.FaenaId.HasValue&&source.FaenaId!=target.FaenaId)throw new DomainException("Solo se pueden fusionar nodos de la misma faena.");
        var children=await _db.TechnicalNodes.Where(x=>x.ParentId==source.Id).ToArrayAsync(ct);foreach(var child in children){child.ParentId=target.Id;child.UpdatedAtUtc=DateTimeOffset.UtcNow;child.UpdatedByUserId=user.UserId;}
        var sourceFamilyIds=await _db.TechnicalNodeFamilies.AsNoTracking().Where(x=>x.TechnicalNodeId==source.Id).Select(x=>x.EquipmentFamilyId).Distinct().ToArrayAsync(ct);var targetFamilyIds=(await _db.TechnicalNodeFamilies.AsNoTracking().Where(x=>x.TechnicalNodeId==target.Id).Select(x=>x.EquipmentFamilyId).ToArrayAsync(ct)).ToHashSet();
        foreach(var id in sourceFamilyIds)if(targetFamilyIds.Add(id))_db.TechnicalNodeFamilies.Add(new TechnicalNodeFamilyEntity{TechnicalNodeId=target.Id,EquipmentFamilyId=id});
        var sourceAssetIds=await _db.TechnicalNodeAssets.AsNoTracking().Where(x=>x.TechnicalNodeId==source.Id).Select(x=>x.AssetId).Distinct().ToArrayAsync(ct);var targetAssetIds=(await _db.TechnicalNodeAssets.AsNoTracking().Where(x=>x.TechnicalNodeId==target.Id).Select(x=>x.AssetId).ToArrayAsync(ct)).ToHashSet();
        foreach(var id in sourceAssetIds)if(targetAssetIds.Add(id))_db.TechnicalNodeAssets.Add(new TechnicalNodeAssetEntity{TechnicalNodeId=target.Id,AssetId=id});
        var sourceAliases=await _db.TechnicalNodeAliases.AsNoTracking().Where(x=>x.TechnicalNodeId==source.Id).Select(x=>x.Alias).ToArrayAsync(ct);var existingAliases=(await _db.TechnicalNodeAliases.AsNoTracking().Where(x=>x.TechnicalNodeId==target.Id).Select(x=>x.NormalizedAlias).ToArrayAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach(var alias in sourceAliases.Concat([source.Code,source.Name]).Where(x=>!string.IsNullOrWhiteSpace(x)).Select(x=>x.Trim())){var normalized=NormalizeName(alias);if(!string.IsNullOrWhiteSpace(normalized)&&existingAliases.Add(normalized))_db.TechnicalNodeAliases.Add(new TechnicalNodeAliasEntity{TechnicalNodeId=target.Id,Alias=alias,NormalizedAlias=normalized,Source="Merge"});}
        source.IsObsolete=true;source.MergedIntoNodeId=target.Id;source.UpdatedAtUtc=DateTimeOffset.UtcNow;source.UpdatedByUserId=user.UserId;target.UpdatedAtUtc=DateTimeOffset.UtcNow;target.UpdatedByUserId=user.UserId;
        await _db.SaveChangesAsync(ct);await tx.CommitAsync(ct);DetachTrackedTechnicalHierarchy();var merged=await GetByCodeAsync(target.Code,user,ct);await Audit(user,"Merged",target.Code,JsonSerializer.Serialize(new{r.SourceCode,r.TargetCode}),JsonSerializer.Serialize(merged),r.Reason,ct);return merged;
    }

    public async Task<IReadOnlyCollection<TechnicalNodeResponse>> AssignFamiliesAsync(BulkFamilyAssignmentRequest r, UserAccessContext user, CancellationToken ct)
    {
        EnsureCanManage(user);if(r.NodeCodes.Count==0)throw new DomainException("Debe indicar al menos un nodo.");var fams=await ResolveFamiliesAsync(r.Families,ct);var changed=new List<string>();
        foreach(var code in r.NodeCodes){var node=await BaseQuery(true).SingleOrDefaultAsync(x=>x.Code.ToUpper()==Code(code),ct);if(node is null)continue;if(!r.Append)node.Families.Clear();foreach(var f in fams)if(node.Families.All(x=>x.EquipmentFamilyId!=f.Id))node.Families.Add(new TechnicalNodeFamilyEntity{TechnicalNodeId=node.Id,EquipmentFamilyId=f.Id});node.UpdatedAtUtc=DateTimeOffset.UtcNow;node.UpdatedByUserId=user.UserId;changed.Add(node.Code);}
        await _db.SaveChangesAsync(ct);await Audit(user,"AssignedFamilies",string.Join(";",changed),null,JsonSerializer.Serialize(r),"Asignacion masiva de familias",ct);var visible=await ListAsync(new TechnicalHierarchyQuery(IncludeObsolete:true),user,ct);return visible.Where(x=>changed.Contains(x.Codigo,StringComparer.OrdinalIgnoreCase)).ToArray();
    }

    public async Task<TechnicalNodeResponse?> AssignAssetsAsync(string code, AssetAssignmentRequest r, UserAccessContext user, CancellationToken ct)
    {
        EnsureCanManage(user);var node=await BaseQuery(true).SingleOrDefaultAsync(x=>x.Code.ToUpper()==Code(code),ct);if(node is null)return null;var assets=await ResolveAssetsAsync(r.AssetCodes,user,ct);var prev=JsonSerializer.Serialize(AuditShape(node));
        if(!r.Append)node.Assets.Clear();foreach(var a in assets)if(node.Assets.All(x=>x.AssetId!=a.Id))node.Assets.Add(new TechnicalNodeAssetEntity{TechnicalNodeId=node.Id,AssetId=a.Id});node.UpdatedAtUtc=DateTimeOffset.UtcNow;node.UpdatedByUserId=user.UserId;
        await _db.SaveChangesAsync(ct);await Audit(user,"AssignedAssets",node.Code,prev,JsonSerializer.Serialize(AuditShape(node)),"Asignacion de activos",ct);return await GetByCodeAsync(node.Code,user,ct);
    }

    private IQueryable<TechnicalNodeEntity> BaseQuery(bool tracked=false)
    {
        var q=_db.TechnicalNodes.Include(x=>x.Parent).Include(x=>x.Faena).ThenInclude(x=>x.TechnicalLocation).Include(x=>x.MergedIntoNode).Include(x=>x.Families).ThenInclude(x=>x.EquipmentFamily).Include(x=>x.Assets).ThenInclude(x=>x.Asset).ThenInclude(x=>x.Faena).Include(x=>x.Aliases).AsSplitQuery();
        return tracked?q:q.AsNoTracking();
    }

    private IQueryable<TechnicalNodeEntity> ApplyScope(IQueryable<TechnicalNodeEntity> q, UserAccessContext user)
    {
        if(user.Roles.Contains(AuthRoles.Admin,StringComparer.OrdinalIgnoreCase)||user.Permissions.Contains(AuthPermissions.Administration,StringComparer.OrdinalIgnoreCase))return q;
        var faenas=user.Faenas.Select(Code).Where(x=>x is not null).Select(x=>x!).ToArray();
        return q.Where(x=>x.FaenaId==null||(x.Faena!=null&&faenas.Contains(x.Faena.Code.ToUpper()))||x.Assets.Any(a=>faenas.Contains(a.Asset.Faena.Code.ToUpper())));
    }

    private async Task<IReadOnlyDictionary<string,TechnicalNodeEntity>> LoadAllAsync(CancellationToken ct)=>await BaseQuery().AsNoTracking().Where(x=>x.Code!="").ToDictionaryAsync(x=>NormCode(x.Code),StringComparer.OrdinalIgnoreCase,ct);

    private async Task<TechnicalNodeEntity?> ValidateParentAsync(Guid? currentId, TechnicalHierarchyLevel level, string? parentCode, CancellationToken ct)
    {
        if(level==TechnicalHierarchyLevel.Sistema){if(!string.IsNullOrWhiteSpace(parentCode))throw new DomainException("Un sistema no debe tener nodo padre.");return null;}
        if(string.IsNullOrWhiteSpace(parentCode))throw new DomainException($"El nivel {level} requiere CodigoPadre.");
        var parent=await _db.TechnicalNodes.SingleOrDefaultAsync(x=>x.Code.ToUpper()==Code(parentCode),ct)??throw new DomainException("El nodo padre indicado no existe.");
        if(currentId.HasValue&&parent.Id==currentId.Value)throw new DomainException("Un nodo no puede ser padre de si mismo.");
        if(parent.IsObsolete)throw new DomainException("No se puede asociar a un nodo padre obsoleto.");
        var expected=level switch{TechnicalHierarchyLevel.Subsistema=>TechnicalHierarchyLevel.Sistema,TechnicalHierarchyLevel.Componente=>TechnicalHierarchyLevel.Subsistema,TechnicalHierarchyLevel.Subcomponente=>TechnicalHierarchyLevel.Componente,_=>TechnicalHierarchyLevel.Sistema};
        if(ParseLevel(parent.Level)!=expected)throw new DomainException($"El padre de {level} debe ser {expected}.");
        if(currentId.HasValue)await EnsureNoCycleAsync(currentId.Value,parent.Id,ct);return parent;
    }

    private async Task EnsureNoCycleAsync(Guid nodeId, Guid parentId, CancellationToken ct)
    {
        var current=parentId;for(var guard=0;guard<100;guard++){if(current==nodeId)throw new DomainException("La asignacion de padre genera un ciclo en la jerarquia tecnica.");var parent=await _db.TechnicalNodes.AsNoTracking().Where(x=>x.Id==current).Select(x=>new{x.ParentId}).SingleOrDefaultAsync(ct);if(parent?.ParentId is null)return;current=parent.ParentId.Value;}
        throw new DomainException("La jerarquia tecnica supera la profundidad permitida o contiene un ciclo.");
    }

    private async Task<FaenaEntity?> ResolveFaenaAsync(string? faenaCode, UserAccessContext user, CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(faenaCode))return null;if(!_auth.CanViewFaena(user,faenaCode))throw new UnauthorizedAccessException("El usuario no tiene acceso a la faena indicada.");var code=Code(faenaCode)!;
        var faena = await _db.Faenas.Include(x => x.TechnicalLocation).SingleOrDefaultAsync(x=>x.Code.ToUpper()==code,ct)??throw new DomainException("La faena indicada no existe.");
        if (faena.TechnicalLocation is null) throw new DomainException("La faena indicada no tiene una ubicación técnica configurada.");
        return faena;
    }

    private async Task<IReadOnlyCollection<EquipmentFamilyEntity>> ResolveFamiliesAsync(IReadOnlyCollection<string> values, CancellationToken ct)
    {
        var codes=Clean(values);if(codes.Count==0)return [];var rows=await _db.EquipmentFamilies.Where(x=>codes.Contains(x.Code.ToUpper())).ToArrayAsync(ct);var found=rows.Select(x=>Code(x.Code)!).ToHashSet(StringComparer.OrdinalIgnoreCase);var missing=codes.Where(x=>!found.Contains(x)).ToArray();if(missing.Length>0)throw new DomainException($"Familias de equipo inexistentes: {string.Join(", ",missing)}.");return rows;
    }

    private async Task<IReadOnlyCollection<AssetEntity>> ResolveAssetsAsync(IReadOnlyCollection<string> values, UserAccessContext user, CancellationToken ct)
    {
        var codes=Clean(values);if(codes.Count==0)return [];var rows=await _db.Assets.Include(x=>x.Faena).Where(x=>codes.Contains(x.Code.ToUpper())).ToArrayAsync(ct);var found=rows.Select(x=>Code(x.Code)!).ToHashSet(StringComparer.OrdinalIgnoreCase);var missing=codes.Where(x=>!found.Contains(x)).ToArray();if(missing.Length>0)throw new DomainException($"Activos inexistentes: {string.Join(", ",missing)}.");foreach(var a in rows)if(!_auth.CanViewFaena(user,a.Faena.Code))throw new UnauthorizedAccessException($"El usuario no tiene acceso al activo '{a.Code}'.");return rows;
    }

    private async Task EnsureNoDuplicateAsync(Guid? currentId, TechnicalHierarchyLevel level, Guid? parentId, string normalizedName, CancellationToken ct)
    {
        var l=level.ToString();var exists=await _db.TechnicalNodes.AnyAsync(x=>(!currentId.HasValue||x.Id!=currentId.Value)&&!x.IsObsolete&&x.Level==l&&x.ParentId==parentId&&x.NormalizedName==normalizedName,ct);if(exists)throw new DomainException("Ya existe un nodo tecnico con el mismo nombre normalizado en el mismo nivel y padre.");
    }

    private async Task EnsureCanFilterByFaenaAsync(string? faenaCode, UserAccessContext user, CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(faenaCode))return;if(!_auth.CanViewFaena(user,faenaCode))throw new UnauthorizedAccessException("El usuario no tiene acceso a la faena solicitada.");var code=Code(faenaCode)!;if(!await _db.Faenas.AnyAsync(x=>x.Code.ToUpper()==code,ct))throw new DomainException("La faena indicada no existe.");
    }

    private void DetachTrackedTechnicalHierarchy()
    {
        foreach(var entry in _db.ChangeTracker.Entries().Where(x=>x.Entity is TechnicalNodeEntity or TechnicalNodeFamilyEntity or TechnicalNodeAssetEntity or TechnicalNodeAliasEntity or TechnicalLocationEntity).ToArray())entry.State=EntityState.Detached;
    }
    private static void ReplaceFamilies(TechnicalNodeEntity node,IReadOnlyCollection<EquipmentFamilyEntity> fams){var desired=fams.Select(x=>x.Id).ToHashSet();foreach(var e in node.Families.Where(x=>!desired.Contains(x.EquipmentFamilyId)).ToArray())node.Families.Remove(e);foreach(var f in fams)if(node.Families.All(x=>x.EquipmentFamilyId!=f.Id))node.Families.Add(new TechnicalNodeFamilyEntity{TechnicalNodeId=node.Id,EquipmentFamilyId=f.Id});}
    private static void ReplaceAssets(TechnicalNodeEntity node,IReadOnlyCollection<AssetEntity> assets){var desired=assets.Select(x=>x.Id).ToHashSet();foreach(var e in node.Assets.Where(x=>!desired.Contains(x.AssetId)).ToArray())node.Assets.Remove(e);foreach(var a in assets)if(node.Assets.All(x=>x.AssetId!=a.Id))node.Assets.Add(new TechnicalNodeAssetEntity{TechnicalNodeId=node.Id,AssetId=a.Id});}
    private static void AddAliases(TechnicalNodeEntity node,IEnumerable<string?> aliases,string source){var existing=node.Aliases.Select(x=>x.NormalizedAlias).ToHashSet(StringComparer.OrdinalIgnoreCase);foreach(var alias in aliases.Where(x=>!string.IsNullOrWhiteSpace(x)).Select(x=>x!.Trim())){var n=NormalizeName(alias);if(string.IsNullOrWhiteSpace(n)||existing.Contains(n))continue;node.Aliases.Add(new TechnicalNodeAliasEntity{TechnicalNodeId=node.Id,Alias=alias,NormalizedAlias=n,Source=source});existing.Add(n);}}

    private static TechnicalNodeResponse ToResponse(TechnicalNodeEntity n,IReadOnlyDictionary<string,TechnicalNodeEntity> all)
    {
        var fams=n.Families.Select(x=>x.EquipmentFamily.Code).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).ToArray();
        var assets=n.Assets.Select(x=>x.Asset.Code).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).ToArray();
        var aliases=n.Aliases.Select(x=>x.Alias).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).ToArray();
        var children=all.Values.Any(x=>x.ParentId==n.Id&&!x.IsObsolete);
        return new TechnicalNodeResponse(n.Code,n.Name,n.NormalizedName,ParseLevel(n.Level),n.Parent?.Code,n.Faena?.Code,n.Faena?.TechnicalLocation?.Code,fams,assets,aliases,n.IsObsolete,n.MergedIntoNode?.Code,n.CreatedAtUtc,n.UpdatedAtUtc,PathOf(n,all),children,children||assets.Length>0);
    }

    private static string PathOf(TechnicalNodeEntity node,IReadOnlyDictionary<string,TechnicalNodeEntity> all)
    {
        var byId=all.Values.ToDictionary(x=>x.Id);var parts=new Stack<string>();var current=node;
        for(var i=0;i<100;i++){parts.Push(string.IsNullOrWhiteSpace(current.Name)?current.Code:current.Name);if(current.ParentId is null||!byId.TryGetValue(current.ParentId.Value,out var p))break;current=p;}
        return string.Join(" / ",parts.Where(x=>!string.IsNullOrWhiteSpace(x)));
    }

    private static IReadOnlyCollection<TechnicalHierarchyTreeNode> BuildTree(IReadOnlyCollection<TechnicalNodeResponse> nodes)
    {
        var byParent=nodes.GroupBy(x=>NormCode(x.CodigoPadre)).ToDictionary(g=>g.Key,g=>g.OrderBy(x=>x.Nombre,StringComparer.OrdinalIgnoreCase).ToArray());var codes=nodes.Select(x=>NormCode(x.Codigo)).ToHashSet(StringComparer.OrdinalIgnoreCase);var roots=nodes.Where(x=>string.IsNullOrWhiteSpace(x.CodigoPadre)||!codes.Contains(NormCode(x.CodigoPadre))).OrderBy(x=>x.Nombre,StringComparer.OrdinalIgnoreCase).ToArray();return roots.Select(Build).ToArray();
        TechnicalHierarchyTreeNode Build(TechnicalNodeResponse n){var children=byParent.TryGetValue(NormCode(n.Codigo),out var direct)?direct.Select(Build).ToArray():[];return new TechnicalHierarchyTreeNode(n,children);}
    }

    private static string? ResolveSystemCode(string code,IReadOnlyDictionary<string,TechnicalNodeEntity> all)
    {
        var c=NormCode(code);for(var i=0;i<100&&all.TryGetValue(c,out var current);i++){if(ParseLevel(current.Level)==TechnicalHierarchyLevel.Sistema)return current.Code;c=NormCode(current.Parent?.Code);}return null;
    }

    private async Task Audit(UserAccessContext user,string action,string id,string? previous,string? next,string? detail,CancellationToken ct)=>await _audit.RecordAsync(new AuditEventRequest(user.UserId,action,AuditModules.TechnicalHierarchy,"TechnicalHierarchy",id,previous,next,Severity:action.Equals("Merged",StringComparison.OrdinalIgnoreCase)?AuditSeverity.High:AuditSeverity.Medium,Detail:detail),ct);
    private void EnsureCanManage(UserAccessContext user){if(!_auth.CanManageTechnicalHierarchy(user))throw new UnauthorizedAccessException("El usuario no tiene permiso para gestionar jerarquia tecnica.");}

    public static string NormalizeName(string? value)
    {
        if(string.IsNullOrWhiteSpace(value))return string.Empty;var normalized=value.Trim().Normalize(NormalizationForm.FormD);var b=new StringBuilder();var space=false;
        foreach(var ch in normalized){var cat=CharUnicodeInfo.GetUnicodeCategory(ch);if(cat==UnicodeCategory.NonSpacingMark)continue;if(char.IsLetterOrDigit(ch)){b.Append(char.ToUpperInvariant(ch));space=false;continue;}if(!space){b.Append(' ');space=true;}}
        return b.ToString().Normalize(NormalizationForm.FormC).Trim();
    }

    private static decimal Similarity(string left,string right){if(string.IsNullOrWhiteSpace(left)&&string.IsNullOrWhiteSpace(right))return 1;var max=Math.Max(left.Length,right.Length);if(max==0)return 1;return 1-((decimal)Distance(left,right)/max);}    
    private static int Distance(string left,string right){var d=new int[left.Length+1,right.Length+1];for(var i=0;i<=left.Length;i++)d[i,0]=i;for(var j=0;j<=right.Length;j++)d[0,j]=j;for(var i=1;i<=left.Length;i++)for(var j=1;j<=right.Length;j++){var cost=left[i-1]==right[j-1]?0:1;d[i,j]=Math.Min(Math.Min(d[i-1,j]+1,d[i,j-1]+1),d[i-1,j-1]+cost);}return d[left.Length,right.Length];}
    private static TechnicalHierarchyLevel ParseLevel(string? value)=>Enum.TryParse<TechnicalHierarchyLevel>(value,true,out var level)?level:TechnicalHierarchyLevel.Sistema;
    private static object AuditShape(TechnicalNodeEntity n)=>new{n.Code,n.Name,n.Level,Parent=n.Parent?.Code,Faena=n.Faena?.Code,Families=n.Families.Select(x=>x.EquipmentFamily?.Code).ToArray(),Assets=n.Assets.Select(x=>x.Asset?.Code).ToArray(),Aliases=n.Aliases.Select(x=>x.Alias).ToArray(),n.IsObsolete,MergedInto=n.MergedIntoNode?.Code};
    private static IReadOnlyCollection<string> Clean(IEnumerable<string?> values)=>values.Where(x=>!string.IsNullOrWhiteSpace(x)).Select(x=>Code(x)!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    private static string NormCode(string? value)=>value?.Trim().ToUpperInvariant()??string.Empty;
    private static string? Code(string? value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim().ToUpperInvariant();
    private static bool Same(string? left,string? right)=>string.Equals(NormCode(left),NormCode(right),StringComparison.OrdinalIgnoreCase);
    private static void Req(string? value,string name){if(string.IsNullOrWhiteSpace(value))throw new DomainException($"El campo {name} es obligatorio.");}
}
