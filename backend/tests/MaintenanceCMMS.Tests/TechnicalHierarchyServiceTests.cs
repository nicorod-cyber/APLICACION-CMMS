using ClosedXML.Excel;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.TechnicalHierarchy;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Infrastructure.Auditing;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using MaintenanceCMMS.Infrastructure.Security;
using MaintenanceCMMS.Infrastructure.TechnicalHierarchy;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace MaintenanceCMMS.Tests;

public sealed class TechnicalHierarchyServiceTests
{
    private static readonly UserAccessContext Admin = new("admin",[AuthRoles.Admin],[AuthPermissions.Administration,AuthPermissions.ManageTechnicalHierarchy],[]);
    private static readonly UserAccessContext FaenaUser = new("planner",[],[AuthPermissions.ManageTechnicalHierarchy],["FAE-1"]);

    [Fact]
    public async Task CreateAsync_CreatesCompleteHierarchyAndPersistsWithNewContext()
    {
        await using var fx=await Fixture.CreateAsync();
        await fx.Service.CreateAsync(new("S-MOT","Motor",TechnicalHierarchyLevel.Sistema,FaenaCodigo:"FAE-1"),Admin,CancellationToken.None);
        await fx.Service.CreateAsync(new("SS-LUB","Lubricacion",TechnicalHierarchyLevel.Subsistema,"S-MOT"),Admin,CancellationToken.None);
        await fx.Service.CreateAsync(new("C-BOM","Bomba aceite",TechnicalHierarchyLevel.Componente,"SS-LUB"),Admin,CancellationToken.None);
        var sub=await fx.Service.CreateAsync(new("SC-SEL","Sello mecanico",TechnicalHierarchyLevel.Subcomponente,"C-BOM",FamiliasEquipo:["FAM-1"],ActivosAsignados:["ACT-1"]),Admin,CancellationToken.None);
        Assert.Equal("Motor / Lubricacion / Bomba aceite / Sello mecanico",sub.Ruta);
        await using var second=fx.NewContext();
        var persisted=await second.TechnicalNodes.Include(x=>x.Assets).Include(x=>x.Families).SingleAsync(x=>x.Code=="SC-SEL");
        Assert.Single(persisted.Assets);Assert.Single(persisted.Families);
    }

    [Fact]
    public async Task Validations_RejectDuplicatesBadParentsSelfCycleAndMissingReferences()
    {
        await using var fx=await Fixture.CreateAsync();
        await fx.Service.CreateAsync(new("S-1","Sistema",TechnicalHierarchyLevel.Sistema,FaenaCodigo:"FAE-1"),Admin,CancellationToken.None);
        await Assert.ThrowsAsync<DomainException>(()=>fx.Service.CreateAsync(new("S-1","Otro",TechnicalHierarchyLevel.Sistema),Admin,CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(()=>fx.Service.CreateAsync(new("SS-BAD","Bad",TechnicalHierarchyLevel.Subsistema,"NOPE"),Admin,CancellationToken.None));
        var ss=await fx.Service.CreateAsync(new("SS-1","Subsistema",TechnicalHierarchyLevel.Subsistema,"S-1"),Admin,CancellationToken.None);
        await Assert.ThrowsAsync<DomainException>(()=>fx.Service.UpdateAsync("SS-1",new("Subsistema","SS-1"),Admin,CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(()=>fx.Service.CreateAsync(new("S-2","Sistema",TechnicalHierarchyLevel.Sistema,FaenaCodigo:"NOPE"),Admin,CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(()=>fx.Service.CreateAsync(new("S-3","Sistema",TechnicalHierarchyLevel.Sistema,FamiliasEquipo:["NO-FAM"]),Admin,CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(()=>fx.Service.CreateAsync(new("S-4","Sistema",TechnicalHierarchyLevel.Sistema,ActivosAsignados:["NO-ACT"]),Admin,CancellationToken.None));
        var rootId=(await fx.Db.TechnicalNodes.SingleAsync(x=>x.Code=="S-1")).Id;
        var badChild=new TechnicalNodeEntity{Code="BAD-SS",Name="Bad",NormalizedName="BAD",Level="Subsistema",ParentId=rootId};
        fx.Db.TechnicalNodes.Add(badChild);await fx.Db.SaveChangesAsync();
        var badParent=new TechnicalNodeEntity{Code="BAD-C",Name="BadC",NormalizedName="BADC",Level="Componente",ParentId=badChild.Id};
        fx.Db.TechnicalNodes.Add(badParent);await fx.Db.SaveChangesAsync();
        badChild.ParentId=badParent.Id;await fx.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(()=>fx.Service.UpdateAsync("BAD-C",new("BadC","BAD-SS"),Admin,CancellationToken.None));
    }

    [Fact]
    public async Task Queries_FilterTreeUsageAndDetectSimilarNodes()
    {
        await using var fx=await Fixture.CreateAsync();
        await fx.Service.CreateAsync(new("S-MP1","Motor Principal",TechnicalHierarchyLevel.Sistema,FaenaCodigo:"FAE-1",FamiliasEquipo:["FAM-1"]),Admin,CancellationToken.None);
        await fx.Service.CreateAsync(new("S-MP2","Motor Prinicpal",TechnicalHierarchyLevel.Sistema,FaenaCodigo:"FAE-2"),Admin,CancellationToken.None);
        await fx.Service.CreateAsync(new("SS-A","Aceite",TechnicalHierarchyLevel.Subsistema,"S-MP1",ActivosAsignados:["ACT-1"]),Admin,CancellationToken.None);
        var faenaNodes=await fx.Service.ListAsync(new(FaenaCodigo:"FAE-1"),Admin,CancellationToken.None);Assert.Equal(2,faenaNodes.Count);Assert.Contains(faenaNodes,x=>x.Codigo=="S-MP1");Assert.Contains(faenaNodes,x=>x.Codigo=="SS-A");
        Assert.Single(await fx.Service.ListAsync(new(Familia:"FAM-1"),Admin,CancellationToken.None));
        Assert.Single(await fx.Service.ListAsync(new(SistemaCodigo:"S-MP1",Nivel:TechnicalHierarchyLevel.Subsistema),Admin,CancellationToken.None));
        var tree=await fx.Service.GetTreeAsync(new(FaenaCodigo:"FAE-1"),Admin,CancellationToken.None);Assert.Single(tree);Assert.Single(tree.First().Children);
        var node=await fx.Service.GetByCodeAsync("S-MP1",Admin,CancellationToken.None);Assert.True(node!.TieneHijos);Assert.True(node.EnUso);
        Assert.NotEmpty(await fx.Service.DetectSimilarAsync(new(),Admin,CancellationToken.None));
    }

    [Fact]
    public async Task Merge_ReassignsRelationsAliasesAndMarksSourceObsolete()
    {
        await using var fx=await Fixture.CreateAsync();
        await fx.Service.CreateAsync(new("S-A","Motor A",TechnicalHierarchyLevel.Sistema,FaenaCodigo:"FAE-1",FamiliasEquipo:["FAM-1"],ActivosAsignados:["ACT-1"]),Admin,CancellationToken.None);
        await fx.Service.CreateAsync(new("S-B","Motor B",TechnicalHierarchyLevel.Sistema,FaenaCodigo:"FAE-1",FamiliasEquipo:["FAM-2"],ActivosAsignados:["ACT-2"]),Admin,CancellationToken.None);
        await fx.Service.CreateAsync(new("SS-A","Hijo",TechnicalHierarchyLevel.Subsistema,"S-A"),Admin,CancellationToken.None);
        var merged=await fx.Service.MergeAsync(new("S-A","S-B","Duplicado"),Admin,CancellationToken.None);
        Assert.NotNull(merged);Assert.Contains("FAM-1",merged!.FamiliasEquipo);Assert.Contains("ACT-1",merged.ActivosAsignados);Assert.Contains("S-A",merged.AliasHistoricos);
        var source=await fx.Service.GetByCodeAsync("S-A",Admin,CancellationToken.None);Assert.True(source!.Obsoleto);Assert.Equal("S-B",source.FusionadoEnCodigo);
        var child=await fx.Service.GetByCodeAsync("SS-A",Admin,CancellationToken.None);Assert.Equal("S-B",child!.CodigoPadre);
    }

    [Fact]
    public async Task Security_RestrictsFaenasAndAuditIsRecorded()
    {
        await using var fx=await Fixture.CreateAsync();
        await fx.Service.CreateAsync(new("S-SEC","Seguro",TechnicalHierarchyLevel.Sistema,FaenaCodigo:"FAE-1"),FaenaUser,CancellationToken.None);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>fx.Service.CreateAsync(new("S-DENY","No",TechnicalHierarchyLevel.Sistema,FaenaCodigo:"FAE-2"),FaenaUser,CancellationToken.None));
        Assert.Single(await fx.Service.ListAsync(new(FaenaCodigo:"FAE-1"),FaenaUser,CancellationToken.None));
        Assert.True(await fx.Db.AuditLogs.AnyAsync(x=>x.Module==AuditModules.TechnicalHierarchy));
    }

    [Fact]
    public async Task Importer_UpdatesTheUniqueLocationBeforeNodesAndIsIdempotent()
    {
        await using var fx=await Fixture.CreateAsync();var dir=Path.Combine(Path.GetTempPath(),"cmms-th-import",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);var loc=Path.Combine(dir,"ubicaciones_tecnicas.xlsx");var sys=Path.Combine(dir,"sistemas_componentes.xlsx");
        WriteWorkbook(loc,["Codigo","Nombre","FaenaCodigo"],[["LOC-1","Planta","FAE-1"]]);
        WriteWorkbook(sys,["Codigo","Nombre","Nivel","CodigoPadre","NombreNormalizado","FaenaCodigo","FamiliasEquipo","ActivosAsignados","AliasHistoricos","Obsoleto","FusionadoEnCodigo","FechaCreacionUtc","FechaActualizacionUtc"],[["S-IMP","Sistema importado","Sistema","","","FAE-1","FAM-1","ACT-1","Alias viejo","false","","",""]]);
        var importer=new TechnicalHierarchyExcelImportService(fx.Db,new AuthorizationPolicyService(),new PostgreSqlAuditService(fx.Db,new AuditContextAccessor()));
        var first=await importer.ImportAsync(new(sys,loc,"admin"),Admin,CancellationToken.None);var second=await importer.ImportAsync(new(sys,loc,"admin"),Admin,CancellationToken.None);
        Assert.Equal(1,first.RegistrosInsertados);Assert.Equal(1,first.RegistrosActualizados);Assert.Equal(1,second.RegistrosActualizados);Assert.Empty(first.Errores);Assert.Empty(first.ReferenciasNoEncontradas);
        await using var next=fx.NewContext();Assert.True(await next.TechnicalNodes.AnyAsync(x=>x.Code=="S-IMP"));Assert.True(await next.TechnicalLocations.AnyAsync(x=>x.Code=="LOC-1"));
    }

    [Fact]
    public async Task Importer_RollsBackWhenReferenceIsMissing()
    {
        await using var fx=await Fixture.CreateAsync();var dir=Path.Combine(Path.GetTempPath(),"cmms-th-import",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);var loc=Path.Combine(dir,"ubicaciones_tecnicas.xlsx");var sys=Path.Combine(dir,"sistemas_componentes.xlsx");
        WriteWorkbook(loc,["Codigo","Nombre","FaenaCodigo"],[["LOC-BAD","Planta","NO-FAE"]]);
        WriteWorkbook(sys,["Codigo","Nombre","Nivel","CodigoPadre","NombreNormalizado","FaenaCodigo","FamiliasEquipo","ActivosAsignados","AliasHistoricos","Obsoleto","FusionadoEnCodigo","FechaCreacionUtc","FechaActualizacionUtc"],[["S-BAD","Sistema","Sistema","","","FAE-1","","","","false","","",""]]);
        var beforeHash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(sys)));
        var importer=new TechnicalHierarchyExcelImportService(fx.Db,new AuthorizationPolicyService(),new PostgreSqlAuditService(fx.Db,new AuditContextAccessor()));
        var result=await importer.ImportAsync(new(sys,loc,"admin"),Admin,CancellationToken.None);
        var afterHash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(sys)));
        Assert.NotEmpty(result.ReferenciasNoEncontradas);Assert.False(await fx.Db.TechnicalNodes.AnyAsync(x=>x.Code=="S-BAD"));Assert.False(await fx.Db.TechnicalLocations.AnyAsync(x=>x.Code=="LOC-BAD"));Assert.Equal(beforeHash,afterHash);
    }

    private static void WriteWorkbook(string path,string[] headers,string[][] rows)
    {
        using var wb=new XLWorkbook();var ws=wb.Worksheets.Add("Data");for(var i=0;i<headers.Length;i++)ws.Cell(1,i+1).Value=headers[i];for(var r=0;r<rows.Length;r++)for(var c=0;c<headers.Length;c++)ws.Cell(r+2,c+1).Value=rows[r][c];wb.SaveAs(path);
    }

    private sealed class Fixture:IAsyncDisposable
    {
        private Fixture(string name,string adminConnectionString,CmmsDbContext db){Name=name;AdminConnectionString=adminConnectionString;Db=db;Service=new TechnicalHierarchyService(db,new PostgreSqlAuditService(db,new AuditContextAccessor()),new AuthorizationPolicyService());}
        public string Name{get;}public string AdminConnectionString{get;}public CmmsDbContext Db{get;}public ITechnicalHierarchyService Service{get;}
        public static async Task<Fixture> CreateAsync()
        {
            var name=$"cmms_th_tests_{Guid.NewGuid():N}";var adminConnectionString=await PostgreSqlWorkTestFixture.GetAdminConnectionStringAsync();await PostgreSqlWorkTestFixture.CreateDatabaseAsync(name,adminConnectionString);
            var db=CreateContext(name,adminConnectionString);await db.Database.MigrateAsync();await SeedAsync(db);return new Fixture(name,adminConnectionString,db);
        }
        public CmmsDbContext NewContext()=>CreateContext(Name,AdminConnectionString);
        private static CmmsDbContext CreateContext(string name,string adminConnectionString)=>new(new DbContextOptionsBuilder<CmmsDbContext>().UseNpgsql(PostgreSqlWorkTestFixture.ConnectionString(adminConnectionString,name)).Options);
        private static async Task SeedAsync(CmmsDbContext db)
        {
            var fae1=new FaenaEntity{Code="FAE-1",Name="Faena Uno",IsActive=true};var fae2=new FaenaEntity{Code="FAE-2",Name="Faena Dos",IsActive=true};
            var loc1=new TechnicalLocationEntity{Code="UT-FAE-1",Name="Ubicacion FAE-1",FaenaId=fae1.Id,Faena=fae1};
            var loc2=new TechnicalLocationEntity{Code="UT-FAE-2",Name="Ubicacion FAE-2",FaenaId=fae2.Id,Faena=fae2};
            fae1.TechnicalLocation=loc1;fae2.TechnicalLocation=loc2;
            var type=new AssetTypeEntity{Code="EQUIPO",Name="Equipo",IsActive=true};var fam1=new EquipmentFamilyEntity{Code="FAM-1",Name="Familia Uno",AssetTypeId=type.Id,IsActive=true};var fam2=new EquipmentFamilyEntity{Code="FAM-2",Name="Familia Dos",AssetTypeId=type.Id,IsActive=true};var state=new AssetOperationalStateEntity{Code="OPERATIVO_FAENA",Name="Operativo",IsActive=true};
            db.AddRange(fae1,fae2,loc1,loc2,type,fam1,fam2,state);db.Assets.AddRange(new AssetEntity{Code="ACT-1",Name="Activo Uno",Faena=fae1,Family=fam1,OperationalState=state,AssetTypeId=type.Id},new AssetEntity{Code="ACT-2",Name="Activo Dos",Faena=fae1,Family=fam2,OperationalState=state,AssetTypeId=type.Id},new AssetEntity{Code="ACT-3",Name="Activo Tres",Faena=fae2,Family=fam2,OperationalState=state,AssetTypeId=type.Id});await db.SaveChangesAsync();
        }
        public async ValueTask DisposeAsync(){await Db.DisposeAsync();await PostgreSqlWorkTestFixture.DropDatabaseAsync(Name,AdminConnectionString);}
    }
}
