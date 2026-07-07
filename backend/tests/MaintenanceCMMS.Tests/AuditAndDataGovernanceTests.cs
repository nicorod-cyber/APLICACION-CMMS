using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Governance;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Domain.Enums;
using MaintenanceCMMS.Infrastructure.Auditing;
using MaintenanceCMMS.Infrastructure.Data.Excel;
using MaintenanceCMMS.Infrastructure.Governance;
using MaintenanceCMMS.Infrastructure.Options;
using Microsoft.Extensions.Options;
using Xunit;

namespace MaintenanceCMMS.Tests;

public sealed class AuditAndDataGovernanceTests
{
    [Fact]
    public async Task AuditAssetCreationAsync_RecordsAssetCreation()
    {
        var fixture = await CreateFixtureAsync();

        await fixture.GovernanceService.AuditAssetCreationAsync(new AssetCreationAuditRequest(
            new GovernanceActor("admin", "FAENA-1"),
            "asset-1",
            "EQ-001",
            """{"Codigo":"EQ-001","Nombre":"Camion"}"""), CancellationToken.None);

        var result = await fixture.AuditService.QueryAsync(new AuditQuery(Module: AuditModules.Assets), CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("asset.created", result.Items.Single().Action);
        Assert.Equal("FAENA-1", result.Items.Single().FaenaCodigo);
    }

    [Fact]
    public async Task AuditStockAdjustmentAsync_RequiresReasonAndRecordsCriticalEvent()
    {
        var fixture = await CreateFixtureAsync();

        await fixture.GovernanceService.AuditStockAdjustmentAsync(new StockAdjustmentAuditRequest(
            new GovernanceActor("bodeguero", "FAENA-1"),
            "stock-1",
            """{"StockFisico":10}""",
            """{"StockFisico":8}""",
            "Diferencia conteo fisico"), CancellationToken.None);

        var result = await fixture.AuditService.QueryAsync(new AuditQuery(Module: AuditModules.Stock), CancellationToken.None);

        var entry = Assert.Single(result.Items);
        Assert.Equal(AuditSeverity.Critical, entry.Severity);
        Assert.Equal("Diferencia conteo fisico", entry.Reason);
    }

    [Fact]
    public async Task AuditValidatedDocumentChangeAsync_RecordsValidatedDocumentModification()
    {
        var fixture = await CreateFixtureAsync();

        await fixture.GovernanceService.AuditValidatedDocumentChangeAsync(new ValidatedDocumentChangeRequest(
            new GovernanceActor("planificador", "FAENA-1"),
            "doc-1",
            "FechaVencimiento",
            "2026-06-30",
            "2026-07-31",
            DataGovernanceState.Validated,
            HasValidatedExpiryPermission: true,
            Reason: "Correccion con respaldo documental",
            HasApproval: true,
            ApprovalUserId: "supervisor"), CancellationToken.None);

        var result = await fixture.AuditService.QueryAsync(new AuditQuery(Module: AuditModules.Documents), CancellationToken.None);

        var entry = Assert.Single(result.Items);
        Assert.Equal("document.validated_field_changed", entry.Action);
        Assert.Equal("Correccion con respaldo documental", entry.Reason);
    }

    [Fact]
    public async Task AuditValidatedDocumentChangeAsync_BlocksValidatedFieldWithoutPermission()
    {
        var fixture = await CreateFixtureAsync();

        await Assert.ThrowsAsync<DomainException>(() =>
            fixture.GovernanceService.AuditValidatedDocumentChangeAsync(new ValidatedDocumentChangeRequest(
                new GovernanceActor("tecnico", "FAENA-1"),
                "doc-1",
                "FechaVencimiento",
                "2026-06-30",
                "2026-07-31",
                DataGovernanceState.Validated,
                HasValidatedExpiryPermission: false,
                Reason: "Correccion sin permiso",
                HasApproval: true,
                ApprovalUserId: "supervisor"), CancellationToken.None));
    }

    [Fact]
    public void EnsureCriticalChangeApproved_BlocksCriticalChangeWithoutApproval()
    {
        var fixture = CreateFixtureWithoutExcel();

        Assert.Throws<DomainException>(() =>
            fixture.GovernanceService.EnsureCriticalChangeApproved(new CriticalChangeApprovalRequest(
                AuditModules.Documents,
                "AssetDocument",
                "doc-1",
                IsApproved: false,
                ApprovalUserId: null,
                Reason: "Cambio critico sin aprobacion")));
    }

    private static async Task<AuditFixture> CreateFixtureAsync()
    {
        var provider = new ExcelDataProvider(
            new ExcelSchemaRegistry(),
            Options.Create(new DataProviderSettings
            {
                Provider = "Excel",
                ExcelPath = Path.Combine(Path.GetTempPath(), "maintenance-cmms-audit-tests", Guid.NewGuid().ToString("N"))
            }));

        await provider.InitializeAsync(CancellationToken.None);
        var auditService = new ExcelAuditService(provider, new AuditContextAccessor());

        return new AuditFixture(
            auditService,
            new DataGovernanceService(auditService));
    }

    private static AuditFixture CreateFixtureWithoutExcel()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "maintenance-cmms-audit-tests", Guid.NewGuid().ToString("N"));
        var provider = new ExcelDataProvider(
            new ExcelSchemaRegistry(),
            Options.Create(new DataProviderSettings
            {
                Provider = "Excel",
                ExcelPath = tempPath
            }));

        var auditService = new ExcelAuditService(provider, new AuditContextAccessor());
        return new AuditFixture(auditService, new DataGovernanceService(auditService));
    }

    private sealed record AuditFixture(
        IAuditService AuditService,
        IDataGovernanceService GovernanceService);
}
