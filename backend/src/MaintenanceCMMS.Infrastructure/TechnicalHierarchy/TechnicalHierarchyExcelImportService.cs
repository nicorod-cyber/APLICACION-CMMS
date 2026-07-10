using ClosedXML.Excel;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.TechnicalHierarchy;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceCMMS.Infrastructure.TechnicalHierarchy;

public sealed class TechnicalHierarchyExcelImportService : ITechnicalHierarchyExcelImportService
{
    private readonly CmmsDbContext _db;
    private readonly IAuthorizationPolicyService _auth;
    private readonly IAuditService _audit;
    public TechnicalHierarchyExcelImportService(CmmsDbContext db, IAuthorizationPolicyService auth, IAuditService audit){_db=db;_auth=auth;_audit=audit;}

    public async Task<TechnicalHierarchyExcelImportResult> ImportAsync(TechnicalHierarchyExcelImportCommand command, UserAccessContext user, CancellationToken ct)
    {
        if(!_auth.CanManageTechnicalHierarchy(user))throw new UnauthorizedAccessException("El usuario no tiene permiso para importar jerarquia tecnica.");
        var files=new[]{command.UbicacionesTecnicasPath,command.SistemasComponentesPath};
        var errors=new List<string>();var warnings=new List<string>();var missing=new List<string>();
        foreach(var file in files)if(string.IsNullOrWhiteSpace(file)||!File.Exists(file))errors.Add($"Archivo no encontrado: {file}");
        if(errors.Count>0)return Result(files,0,0,0,0,warnings,errors,missing);
        var locations=ReadRows(command.UbicacionesTecnicasPath);var nodes=ReadRows(command.SistemasComponentesPath);var rowsRead=locations.Count+nodes.Count;
        await ValidateAsync(locations,nodes,errors,missing,ct);
        if(errors.Count>0||missing.Count>0)return Result(files,rowsRead,0,0,rowsRead,warnings,errors,missing);
        var inserted=0;var updated=0;var skipped=0;
        await using var tx=await _db.Database.BeginTransactionAsync(ct);
        try
        {
            foreach(var row in locations.OrderBy(x=>string.IsNullOrWhiteSpace(Get(x,"CodigoPadre"))?0:1))
            {
                var code=Code(Get(row,"Codigo"))!;var existing=await _db.TechnicalLocations.SingleOrDefaultAsync(x=>x.Code.ToUpper()==code,ct);var faena=await FaenaAsync(Get(row,"FaenaCodigo"),ct);var parent=await LocationAsync(Get(row,"CodigoPadre"),ct);
                if(existing is null){existing=new TechnicalLocationEntity{Code=code,CreatedByUserId=command.ImportedBy};_db.TechnicalLocations.Add(existing);inserted++;}else updated++;
                existing.Name=Get(row,"Nombre")!.Trim();existing.NormalizedName=TechnicalHierarchyService.NormalizeName(existing.Name);existing.FaenaId=faena!.Id;existing.ParentId=parent?.Id;existing.UpdatedAtUtc=DateTimeOffset.UtcNow;existing.UpdatedByUserId=command.ImportedBy;
                await _db.SaveChangesAsync(ct);
            }
            foreach(var level in new[]{"Sistema","Subsistema","Componente","Subcomponente"})
            foreach(var row in nodes.Where(x=>string.Equals(Get(x,"Nivel"),level,StringComparison.OrdinalIgnoreCase)))
            {
                var code=Code(Get(row,"Codigo"))!;var existing=await NodeQuery(true).SingleOrDefaultAsync(x=>x.Code.ToUpper()==code,ct);var parent=await NodeAsync(Get(row,"CodigoPadre"),ct);var faena=await FaenaAsync(Get(row,"FaenaCodigo"),ct);var loc=await LocationAsync(Get(row,"UbicacionTecnicaCodigo"),ct);
                if(existing is null){existing=new TechnicalNodeEntity{Code=code,CreatedByUserId=command.ImportedBy};_db.TechnicalNodes.Add(existing);inserted++;}else updated++;
                existing.Name=Get(row,"Nombre")!.Trim();existing.NormalizedName=string.IsNullOrWhiteSpace(Get(row,"NombreNormalizado"))?TechnicalHierarchyService.NormalizeName(existing.Name):Get(row,"NombreNormalizado")!.Trim();existing.Level=ParseLevel(Get(row,"Nivel")).ToString();existing.ParentId=parent?.Id;existing.FaenaId=faena?.Id;existing.TechnicalLocationId=loc?.Id;existing.IsObsolete=ParseBool(Get(row,"Obsoleto"));existing.UpdatedAtUtc=DateTimeOffset.UtcNow;existing.UpdatedByUserId=command.ImportedBy;
                await SyncFamiliesAsync(existing,Split(Get(row,"FamiliasEquipo")),ct);await SyncAssetsAsync(existing,Split(Get(row,"ActivosAsignados")),ct);SyncAliases(existing,Split(Get(row,"AliasHistoricos")),"Import");
                await _db.SaveChangesAsync(ct);
            }
            await tx.CommitAsync(ct);
            await _audit.RecordAsync(new AuditEventRequest(command.ImportedBy,"technical_hierarchy.imported",AuditModules.TechnicalHierarchy,"TechnicalHierarchyImport",DateTimeOffset.UtcNow.ToString("O"),NewValue:$"Filas: {rowsRead}",Severity:AuditSeverity.High,Detail:"Importacion explicita de jerarquia tecnica desde Excel"),ct);
            return Result(files,rowsRead,inserted,updated,skipped,warnings,errors,missing);
        }
        catch(Exception ex) when (ex is not DomainException)
        {
            await tx.RollbackAsync(ct);errors.Add(ex.Message);return Result(files,rowsRead,inserted,updated,skipped,warnings,errors,missing);
        }
    }

    private async Task ValidateAsync(IReadOnlyCollection<Dictionary<string,string?>> locations,IReadOnlyCollection<Dictionary<string,string?>> nodes,List<string> errors,List<string> missing,CancellationToken ct)
    {
        var locationCodes=locations.Select(x=>Code(Get(x,"Codigo"))).Where(x=>x is not null).Select(x=>x!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nodeCodes=nodes.Select(x=>Code(Get(x,"Codigo"))).Where(x=>x is not null).Select(x=>x!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach(var row in locations){Required(row,"Codigo",errors);Required(row,"Nombre",errors);Required(row,"FaenaCodigo",errors);await ExistsFaena(Get(row,"FaenaCodigo"),missing,ct);var parent=Code(Get(row,"CodigoPadre"));if(parent is not null&&!locationCodes.Contains(parent)&&!await _db.TechnicalLocations.AnyAsync(x=>x.Code.ToUpper()==parent,ct))missing.Add($"Ubicacion padre inexistente: {parent}");}
        foreach(var row in nodes)
        {
            Required(row,"Codigo",errors);Required(row,"Nombre",errors);Required(row,"Nivel",errors);var level=ParseLevel(Get(row,"Nivel"));var parent=Code(Get(row,"CodigoPadre"));
            if(level==MaintenanceCMMS.Application.TechnicalHierarchy.TechnicalHierarchyLevel.Sistema&&parent is not null)errors.Add($"Sistema con padre no permitido: {Get(row,"Codigo")}");
            if(level!=MaintenanceCMMS.Application.TechnicalHierarchy.TechnicalHierarchyLevel.Sistema&&parent is null)errors.Add($"Nodo sin padre requerido: {Get(row,"Codigo")}");
            if(parent is not null&&!nodeCodes.Contains(parent)&&!await _db.TechnicalNodes.AnyAsync(x=>x.Code.ToUpper()==parent,ct))missing.Add($"Nodo padre inexistente: {parent}");
            if(!string.IsNullOrWhiteSpace(Get(row,"FaenaCodigo")))await ExistsFaena(Get(row,"FaenaCodigo"),missing,ct);
            var loc=Code(Get(row,"UbicacionTecnicaCodigo"));if(loc is not null&&!locationCodes.Contains(loc)&&!await _db.TechnicalLocations.AnyAsync(x=>x.Code.ToUpper()==loc,ct))missing.Add($"Ubicacion tecnica inexistente: {loc}");
            foreach(var family in Split(Get(row,"FamiliasEquipo")))if(!await _db.EquipmentFamilies.AnyAsync(x=>x.Code.ToUpper()==Code(family),ct))missing.Add($"Familia inexistente: {family}");
            foreach(var asset in Split(Get(row,"ActivosAsignados")))if(!await _db.Assets.AnyAsync(x=>x.Code.ToUpper()==Code(asset),ct))missing.Add($"Activo inexistente: {asset}");
        }
    }

    private static IReadOnlyCollection<Dictionary<string,string?>> ReadRows(string path)
    {
        using var workbook=new XLWorkbook(path);var sheet=workbook.Worksheets.First();var used=sheet.RangeUsed();if(used is null)return [];
        var headers=used.FirstRow().Cells().Select(c=>c.GetString().Trim()).ToArray();var result=new List<Dictionary<string,string?>>();
        foreach(var row in used.RowsUsed().Skip(1)){var values=new Dictionary<string,string?>(StringComparer.OrdinalIgnoreCase);for(var i=0;i<headers.Length;i++)values[headers[i]]=row.Cell(i+1).GetString();if(values.Values.Any(v=>!string.IsNullOrWhiteSpace(v)))result.Add(values);}return result;
    }

    private IQueryable<TechnicalNodeEntity> NodeQuery(bool tracked=false){var q=_db.TechnicalNodes.Include(x=>x.Families).ThenInclude(x=>x.EquipmentFamily).Include(x=>x.Assets).ThenInclude(x=>x.Asset).Include(x=>x.Aliases).AsSplitQuery();return tracked?q:q.AsNoTracking();}
    private async Task<FaenaEntity?> FaenaAsync(string? code,CancellationToken ct)=>string.IsNullOrWhiteSpace(code)?null:await _db.Faenas.SingleAsync(x=>x.Code.ToUpper()==Code(code),ct);
    private async Task<TechnicalLocationEntity?> LocationAsync(string? code,CancellationToken ct)=>string.IsNullOrWhiteSpace(code)?null:await _db.TechnicalLocations.SingleAsync(x=>x.Code.ToUpper()==Code(code),ct);
    private async Task<TechnicalNodeEntity?> NodeAsync(string? code,CancellationToken ct)=>string.IsNullOrWhiteSpace(code)?null:await _db.TechnicalNodes.SingleAsync(x=>x.Code.ToUpper()==Code(code),ct);
    private async Task ExistsFaena(string? code,List<string> missing,CancellationToken ct){if(string.IsNullOrWhiteSpace(code))return;var c=Code(code)!;if(!await _db.Faenas.AnyAsync(x=>x.Code.ToUpper()==c,ct))missing.Add($"Faena inexistente: {c}");}
    private static void Required(Dictionary<string,string?> row,string col,List<string> errors){if(string.IsNullOrWhiteSpace(Get(row,col)))errors.Add($"Columna requerida vacia: {col}");}

    private async Task SyncFamiliesAsync(TechnicalNodeEntity node,IReadOnlyCollection<string> codes,CancellationToken ct)
    {
        var desired=new HashSet<Guid>();foreach(var code in codes){var c=Code(code)!;desired.Add((await _db.EquipmentFamilies.SingleAsync(x=>x.Code.ToUpper()==c,ct)).Id);}foreach(var item in node.Families.Where(x=>!desired.Contains(x.EquipmentFamilyId)).ToArray())node.Families.Remove(item);foreach(var id in desired)if(node.Families.All(x=>x.EquipmentFamilyId!=id))node.Families.Add(new TechnicalNodeFamilyEntity{TechnicalNodeId=node.Id,EquipmentFamilyId=id});
    }
    private async Task SyncAssetsAsync(TechnicalNodeEntity node,IReadOnlyCollection<string> codes,CancellationToken ct)
    {
        var desired=new HashSet<Guid>();foreach(var code in codes){var c=Code(code)!;desired.Add((await _db.Assets.SingleAsync(x=>x.Code.ToUpper()==c,ct)).Id);}foreach(var item in node.Assets.Where(x=>!desired.Contains(x.AssetId)).ToArray())node.Assets.Remove(item);foreach(var id in desired)if(node.Assets.All(x=>x.AssetId!=id))node.Assets.Add(new TechnicalNodeAssetEntity{TechnicalNodeId=node.Id,AssetId=id});
    }
    private static void SyncAliases(TechnicalNodeEntity node,IReadOnlyCollection<string> aliases,string source){var existing=node.Aliases.Select(x=>x.NormalizedAlias).ToHashSet(StringComparer.OrdinalIgnoreCase);foreach(var alias in aliases){var n=TechnicalHierarchyService.NormalizeName(alias);if(existing.Contains(n))continue;node.Aliases.Add(new TechnicalNodeAliasEntity{TechnicalNodeId=node.Id,Alias=alias,NormalizedAlias=n,Source=source});existing.Add(n);}}

    private static TechnicalHierarchyExcelImportResult Result(IEnumerable<string> files,int read,int inserted,int updated,int skipped,IReadOnlyCollection<string> warnings,IReadOnlyCollection<string> errors,IReadOnlyCollection<string> missing)=>new(files.Select(Path.GetFileName).Where(x=>x is not null).Select(x=>x!).ToArray(),read,inserted,updated,skipped,warnings,errors,missing.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    private static string? Get(IReadOnlyDictionary<string,string?> row,string key)=>row.TryGetValue(key,out var value)?(string.IsNullOrWhiteSpace(value)?null:value.Trim()):null;
    private static IReadOnlyCollection<string> Split(string? value)=>string.IsNullOrWhiteSpace(value)?[]:value.Split([';',','],StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    private static string? Code(string? value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim().ToUpperInvariant();
    private static bool ParseBool(string? value)=>!string.IsNullOrWhiteSpace(value)&&(value.Equals("true",StringComparison.OrdinalIgnoreCase)||value.Equals("si",StringComparison.OrdinalIgnoreCase)||value=="1");
    private static MaintenanceCMMS.Application.TechnicalHierarchy.TechnicalHierarchyLevel ParseLevel(string? value)=>Enum.TryParse<MaintenanceCMMS.Application.TechnicalHierarchy.TechnicalHierarchyLevel>(value,true,out var level)?level:MaintenanceCMMS.Application.TechnicalHierarchy.TechnicalHierarchyLevel.Sistema;
}
