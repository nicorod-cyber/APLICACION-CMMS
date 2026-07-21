using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.Documents;
using MaintenanceCMMS.Infrastructure.Auditing;
using MaintenanceCMMS.Infrastructure.Documents;
using MaintenanceCMMS.Infrastructure.Security;
using MaintenanceCMMS.Infrastructure.WorkOrders;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MaintenanceCMMS.Tests;

public sealed class DocumentaryComplianceTests
{
    private const string FaenaCode = "FAE-1";
    private static readonly UserAccessContext Admin = new("admin", [AuthRoles.Admin], [AuthPermissions.ManageDocuments, AuthPermissions.ConfigureDocumentTypes], [FaenaCode]);
    private static readonly UserAccessContext Planner = new(
        PostgreSqlWorkTestFixture.PlannerUserId.ToString("D"),
        [AuthRoles.Planner],
        [AuthPermissions.ManageDocuments, AuthPermissions.ReviewDocuments, AuthPermissions.ValidateDocuments, AuthPermissions.RejectDocuments, AuthPermissions.ManageDocumentRequirements],
        [FaenaCode]);

    [Fact]
    public async Task DocumentaryEngine_At45Days_IsIdempotentAndKeepsOriginVersions()
    {
        await using var fixture = await PostgreSqlWorkTestFixture.CreateAsync();
        var db = fixture.DbContext;
        var audit = new PostgreSqlAuditService(db, new AuditContextAccessor());
        var documents = new DocumentService(db, audit, new AuthorizationPolicyService());
        var matrices = new DocumentRequirementMatrixService(db);
        var engine = new DocumentaryWorkOrderService(db);
        var workOrders = new WorkOrderService(db, audit);
        var referenceDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var inactiveAsset = await db.Assets.SingleAsync(asset => asset.Code == "ACT-2");
        inactiveAsset.DecommissioningDate = referenceDate;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        await documents.CreateTypeAsync(
            new CreateDocumentTypeRequest("CERT-45", "Certificado 45 dias", DocumentEntityType.Activo, true, true, true, 45, [AuthRoles.Planner], false, null, true),
            Admin,
            CancellationToken.None);
        db.ChangeTracker.Clear();
        var firstMatrix = await matrices.CreateVersionAsync(
            new CreateDocumentRequirementMatrixVersionRequest(
                "MATRIX-EQUIPO", "EQUIPO", null, referenceDate.AddDays(-1), "Version inicial",
                [new DocumentRequirementMatrixItemRequest("CERT-45", true, true, true, true, 45)]),
            Planner,
            CancellationToken.None);
        db.ChangeTracker.Clear();
        var document = await documents.CreateAsync(
            new CreateDocumentRequest(
                DocumentEntityType.Activo, "ACT-1", "CERT-45", referenceDate.AddMonths(-1), referenceDate.AddDays(45),
                "sharepoint://cert-45-v1.pdf", "https://sharepoint.example/cert-45-v1.pdf", true, true, true, "Carga inicial",
                ["ACT-1"], "cert-45-v1.pdf", "application/pdf", 1024, "sha256-cert-45-v1"),
            Admin,
            CancellationToken.None);
        await documents.ValidateAsync(document.DocumentoId, new ValidateDocumentRequest("Revision conforme"), Planner, CancellationToken.None);
        db.ChangeTracker.Clear();

        var firstRun = await engine.RunAsync(referenceDate, Planner.UserId, CancellationToken.None);
        var secondRun = await engine.RunAsync(referenceDate, Planner.UserId, CancellationToken.None);

        Assert.Equal(1, firstRun.OrdenesCreadas);
        Assert.Equal(1, firstRun.RequisitosCreados);
        Assert.Equal(0, secondRun.OrdenesCreadas);
        Assert.Equal(0, secondRun.RequisitosCreados);
        var workOrderNumber = Assert.Single(firstRun.NumerosOT);
        Assert.Contains(workOrderNumber, secondRun.NumerosOT);
        Assert.Equal(1, await db.WorkOrders.CountAsync(order => order.DocumentaryMatrixVersionId != null));

        var requirement = await db.DocumentaryWorkOrderRequirements.AsNoTracking().SingleAsync();
        Assert.Equal(Guid.Parse(firstMatrix.Id), requirement.MatrixVersionId);
        Assert.Equal(Guid.Parse(document.DocumentoId), requirement.OriginDocumentId);
        Assert.NotNull(requirement.OriginDocumentVersionId);
        Assert.Equal("POR_VENCER", requirement.Status);

        var detail = await workOrders.GetByIdAsync(workOrderNumber, Admin, CancellationToken.None);
        Assert.NotNull(detail?.ProgresoDocumental);
        Assert.Single(detail!.ProgresoDocumental!.Requisitos);
        Assert.Equal("CERT-45", detail.ProgresoDocumental.Requisitos.Single().TipoDocumentoCodigo);

        var secondMatrix = await matrices.CreateVersionAsync(
            new CreateDocumentRequirementMatrixVersionRequest(
                "MATRIX-EQUIPO", "EQUIPO", null, referenceDate.AddDays(1), "Nueva politica documental",
                [new DocumentRequirementMatrixItemRequest("CERT-45", true, false, false, true, 30)]),
            Planner,
            CancellationToken.None);
        var allMatrices = await matrices.ListAsync(true, Planner, CancellationToken.None);
        var historical = allMatrices.Single(matrix => matrix.Id == firstMatrix.Id);
        Assert.Equal("REEMPLAZADA", historical.Estado);
        Assert.Equal(referenceDate, historical.VigenciaHasta);
        Assert.True(historical.Requisitos.Single().BloqueaDisponibilidad);
        Assert.Equal(2, secondMatrix.NumeroVersion);
        Assert.Equal(Guid.Parse(firstMatrix.Id), requirement.MatrixVersionId);
    }

    [Fact]
    public void ExpiredAndRejectedDocuments_AreInvalid_AndOnlyBlockingRequirementsBlockAvailability()
    {
        var today = new DateOnly(2026, 7, 21);
        var expiredBlocking = DocumentComplianceCalculator.Evaluate("Vigente", today.AddDays(-1), 45, true, true, today);
        var expiredNonBlocking = DocumentComplianceCalculator.Evaluate("Vigente", today.AddDays(-1), 45, true, false, today);
        var rejected = DocumentComplianceCalculator.Evaluate("Rechazado", today.AddDays(30), 45, true, true, today);

        Assert.Equal(DocumentLifecycleStatus.Vencido, expiredBlocking.Status);
        Assert.False(expiredBlocking.IsCompliant);
        Assert.True(expiredBlocking.BlocksAvailability);
        Assert.False(expiredNonBlocking.IsCompliant);
        Assert.False(expiredNonBlocking.BlocksAvailability);
        Assert.Equal(DocumentLifecycleStatus.Rechazado, rejected.Status);
        Assert.False(rejected.IsCompliant);
    }
}
