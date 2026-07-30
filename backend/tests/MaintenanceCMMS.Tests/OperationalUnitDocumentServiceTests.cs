using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.Documents;
using MaintenanceCMMS.Application.OperationalUnits;
using MaintenanceCMMS.Infrastructure.Auditing;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using MaintenanceCMMS.Infrastructure.Documents;
using MaintenanceCMMS.Infrastructure.OperationalUnits;
using MaintenanceCMMS.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MaintenanceCMMS.Tests;

public sealed class OperationalUnitDocumentServiceTests
{
    private const string Faena = "FAE-1";
    private static readonly UserAccessContext Admin = new("admin", [AuthRoles.Admin], [AuthPermissions.ViewOperationalUnits, AuthPermissions.ManageOperationalUnits, AuthPermissions.ManageOperationalUnitComposition, AuthPermissions.ManageDocuments, AuthPermissions.ValidateDocuments, AuthPermissions.ConfigureDocumentTypes], [Faena]);

    [Fact]
    public async Task ConsolidatedView_MergesCurrentChassisAndFactory_AndRecalculatesAfterReplacement()
    {
        await using var fixture = await PostgreSqlWorkTestFixture.CreateAsync();
        var db = fixture.DbContext;
        var audit = new PostgreSqlAuditService(db, new AuditContextAccessor());
        var authorization = new AuthorizationPolicyService();
        var documents = new DocumentService(db, audit, authorization);
        var service = new OperationalUnitDocumentService(db, documents, audit, authorization);
        var site = await db.Faenas.SingleAsync(item => item.Code == Faena);
        var state = await db.AssetOperationalStates.SingleAsync(item => item.Code == "OPERATIVO");
        var chassis = await db.Assets.SingleAsync(item => item.Code == "ACT-1");
        var factory = await db.Assets.SingleAsync(item => item.Code == "ACT-2");
        var factoryType = new AssetTypeEntity { Code = "FABRICA", Name = "Fábrica", IsActive = true };
        var factoryFamily = new EquipmentFamilyEntity { Code = "FAB-1", Name = "Fábrica", AssetType = factoryType, IsActive = true };
        var unitType = new OperationalUnitTypeEntity { Code = "CFA", Name = "Camión fábrica", IsActive = true };
        var chassisRole = new OperationalUnitComponentRoleEntity { Code = "CHASIS", Name = "Chasis", IsCritical = true, IsActive = true };
        var factoryRole = new OperationalUnitComponentRoleEntity { Code = "FABRICA", Name = "Fábrica", IsCritical = true, IsActive = true };
        db.AddRange(factoryType, factoryFamily, unitType, chassisRole, factoryRole);
        factory.AssetTypeDefinition = factoryType; factory.AssetTypeId = factoryType.Id; factory.Family = factoryFamily; factory.FamilyId = factoryFamily.Id;
        await db.SaveChangesAsync();
        var unit = new OperationalUnitEntity { Code = "QUADRA-1029", Name = "QUADRA-1029", OperationalUnitType = unitType, Faena = site, OperationalState = state, BaselineOperationalState = state };
        db.Add(unit);
        db.AddRange(
            new OperationalUnitCompositionRuleEntity { OperationalUnitType = unitType, ComponentRole = chassisRole, MinimumQuantity = 1, MaximumQuantity = 1, IsMandatory = true, IsActive = true },
            new OperationalUnitCompositionRuleEntity { OperationalUnitType = unitType, ComponentRole = factoryRole, MinimumQuantity = 1, MaximumQuantity = 1, IsMandatory = true, IsActive = true },
            new OperationalUnitComponentEntity { OperationalUnit = unit, Asset = chassis, ComponentRole = chassisRole, InstalledAtUtc = DateTimeOffset.UtcNow, InstalledByUserId = Admin.UserId, CriticalRoleCode = "CHASIS" },
            new OperationalUnitComponentEntity { OperationalUnit = unit, Asset = factory, ComponentRole = factoryRole, InstalledAtUtc = DateTimeOffset.UtcNow, InstalledByUserId = Admin.UserId, CriticalRoleCode = "FABRICA" });
        await db.SaveChangesAsync();

        await documents.CreateTypeAsync(new CreateDocumentTypeRequest("REVTEC", "Revisión técnica", DocumentEntityType.Activo, true, true, true), Admin, CancellationToken.None);
        await documents.CreateTypeAsync(new CreateDocumentTypeRequest("SNGM", "SNGM", DocumentEntityType.Activo, true, false, false), Admin, CancellationToken.None);
        var equipmentType = await db.AssetTypes.SingleAsync(item => item.Code == "EQUIPO");
        var equipmentFamily = await db.EquipmentFamilies.SingleAsync(item => item.Code == "FAM-1");
        var revtec = await db.DocumentTypes.SingleAsync(item => item.Code == "REVTEC");
        var sngm = await db.DocumentTypes.SingleAsync(item => item.Code == "SNGM");
        db.DocumentRequirementMatrices.AddRange(
            Matrix("M-CHASIS", site, equipmentType, equipmentFamily, revtec, true),
            Matrix("M-FABRICA", site, factoryType, factoryFamily, sngm, false));
        await db.SaveChangesAsync();

        var chassisDocument = await CreateAndValidateAsync(documents, "ACT-1", "REVTEC");
        var factoryDocument = await CreateAndValidateAsync(documents, "ACT-2", "SNGM");
        db.ChangeTracker.Clear();

        var view = await service.GetAsync(unit.Code, Admin, CancellationToken.None);
        Assert.NotNull(view);
        Assert.True(view!.CompositionComplete);
        Assert.True(view.MatrixConfigurationComplete);
        Assert.Equal(2, view.Rows.Count);
        Assert.Equal(2, view.Summary.Valid);
        Assert.Contains(view.Rows, row => row.TechnicalOwnerRole == "CHASIS" && row.DocumentId == chassisDocument.DocumentoId && row.TechnicalOwnerAssetCode == "ACT-1");
        Assert.Contains(view.Rows, row => row.TechnicalOwnerRole == "FABRICA" && row.DocumentId == factoryDocument.DocumentoId && row.TechnicalOwnerAssetCode == "ACT-2");

        var currentChassis = await db.OperationalUnitComponents.SingleAsync(item => item.OperationalUnitId == unit.Id && item.AssetId == chassis.Id && item.RemovedAtUtc == null);
        currentChassis.RemovedAtUtc = DateTimeOffset.UtcNow;
        var newChassis = new AssetEntity { Code = "ACT-3", Name = "Chasis nuevo", FaenaId = site.Id, AssetTypeId = equipmentType.Id, FamilyId = equipmentFamily.Id, OperationalStateId = state.Id };
        db.Assets.Add(newChassis);
        db.OperationalUnitComponents.Add(new OperationalUnitComponentEntity { OperationalUnitId = unit.Id, Asset = newChassis, ComponentRoleId = chassisRole.Id, InstalledAtUtc = DateTimeOffset.UtcNow, InstalledByUserId = Admin.UserId, CriticalRoleCode = "CHASIS" });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var afterReplacement = await service.GetAsync(unit.Code, Admin, CancellationToken.None);
        Assert.NotNull(afterReplacement);
        Assert.DoesNotContain(afterReplacement!.Rows, row => row.TechnicalOwnerAssetCode == "ACT-1");
        Assert.Contains(afterReplacement.Rows, row => row.TechnicalOwnerAssetCode == "ACT-3" && row.Status == nameof(DocumentLifecycleStatus.PendienteCarga));
        Assert.Single(await db.Documents.Where(item => item.Id == Guid.Parse(chassisDocument.DocumentoId)).ToArrayAsync());

        var factoryComponent = await db.OperationalUnitComponents.SingleAsync(item => item.OperationalUnitId == unit.Id && item.AssetId == factory.Id && item.RemovedAtUtc == null);
        factoryComponent.RemovedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var incomplete = await service.GetAsync(unit.Code, Admin, CancellationToken.None);
        Assert.False(incomplete!.CompositionComplete);
        Assert.Single(incomplete.ConfigurationWarnings.Where(item => item.Contains("composición está incompleta", StringComparison.OrdinalIgnoreCase)));
    }

    private static DocumentRequirementMatrixEntity Matrix(string code, FaenaEntity faena, AssetTypeEntity type, EquipmentFamilyEntity family, DocumentTypeEntity documentType, bool blocks) => new()
    {
        Code = code, VersionNumber = 1, ValidFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), Status = "VIGENTE", Faena = faena, AssetType = type, EquipmentFamily = family, CreatedByUserId = "planner",
        Items = [new DocumentRequirementMatrixItemEntity { DocumentType = documentType, IsMandatory = true, IsCritical = blocks, BlocksAvailability = blocks, RequiresExpirationDate = true, AlertDays = 30 }]
    };

    private static async Task<DocumentResponse> CreateAndValidateAsync(IDocumentService documents, string assetCode, string typeCode)
    {
        var created = await documents.CreateAsync(new CreateDocumentRequest(DocumentEntityType.Activo, assetCode, typeCode, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(90)), $"sharepoint://{assetCode}-{typeCode}.pdf", $"https://sharepoint.example/{assetCode}-{typeCode}.pdf", true, true, true, "Carga de prueba", [assetCode], "prueba.pdf", "application/pdf", 10, "checksum"), Admin, CancellationToken.None);
        return (await documents.ValidateAsync(created.DocumentoId, new ValidateDocumentRequest("Conforme"), Admin, CancellationToken.None))!;
    }
}