using System.Data;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.Documents;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceCMMS.Infrastructure.Documents;

public sealed class DocumentRequirementMatrixService(CmmsDbContext db) : IDocumentRequirementMatrixService
{
    public async Task<IReadOnlyCollection<DocumentRequirementMatrixResponse>> ListAsync(bool incluirHistoricas, UserAccessContext user, CancellationToken ct)
    {
        EnsureView(user);
        var query = db.DocumentRequirementMatrices.AsNoTracking().Include(x => x.AssetType).Include(x => x.EquipmentFamily).Include(x => x.Items).ThenInclude(x => x.DocumentType).AsQueryable();
        if (!incluirHistoricas) query = query.Where(x => x.Status == "VIGENTE");
        return (await query.OrderBy(x => x.Code).ThenByDescending(x => x.VersionNumber).ToListAsync(ct)).Select(Map).ToArray();
    }

    public async Task<DocumentRequirementMatrixResponse> CreateVersionAsync(CreateDocumentRequirementMatrixVersionRequest request, UserAccessContext user, CancellationToken ct)
    {
        EnsureManage(user);
        if (string.IsNullOrWhiteSpace(request.Codigo) || string.IsNullOrWhiteSpace(request.TipoActivoCodigo) || string.IsNullOrWhiteSpace(request.MotivoCambio)) throw new DomainException("Codigo, tipo de activo y motivo de cambio son obligatorios.");
        if (request.Requisitos.Count == 0) throw new DomainException("La matriz debe contener al menos un requisito.");
        if (request.Requisitos.GroupBy(x => x.TipoDocumentoCodigo, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1)) throw new DomainException("Un tipo documental no puede repetirse en la misma matriz.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var code = request.Codigo.Trim().ToUpperInvariant();
        var typeCode = request.TipoActivoCodigo.Trim().ToUpperInvariant();
        var type = await db.AssetTypes.SingleOrDefaultAsync(x => x.Code == typeCode && x.IsActive, ct) ?? throw new DomainException("Tipo de activo inexistente.");
        EquipmentFamilyEntity? family = null;
        if (!string.IsNullOrWhiteSpace(request.FamiliaEquipoCodigo))
        {
            var familyCode = request.FamiliaEquipoCodigo.Trim().ToUpperInvariant();
            family = await db.EquipmentFamilies.SingleOrDefaultAsync(x => x.Code == familyCode && x.IsActive, ct) ?? throw new DomainException("Familia de equipo inexistente.");
            if (family.AssetTypeId != type.Id) throw new DomainException("La familia no pertenece al tipo de activo.");
        }
        var versions = await db.DocumentRequirementMatrices.Where(x => x.Code == code).OrderByDescending(x => x.VersionNumber).ToListAsync(ct);
        var current = versions.FirstOrDefault(x => x.Status == "VIGENTE" && x.ValidTo == null);
        if (current is not null)
        {
            if (request.VigenciaDesde <= current.ValidFrom) throw new DomainException("La nueva vigencia debe comenzar despues de la version vigente.");
            current.ValidTo = request.VigenciaDesde.AddDays(-1); current.Status = "REEMPLAZADA"; current.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }
        var matrix = new DocumentRequirementMatrixEntity { Code = code, VersionNumber = versions.Count == 0 ? 1 : versions.Max(x => x.VersionNumber) + 1, ValidFrom = request.VigenciaDesde, Status = "VIGENTE", AssetTypeId = type.Id, EquipmentFamilyId = family?.Id, CreatedByUserId = user.UserId, ChangeReason = request.MotivoCambio.Trim() };
        foreach (var item in request.Requisitos)
        {
            var docCode = item.TipoDocumentoCodigo.Trim().ToUpperInvariant();
            var docType = await db.DocumentTypes.SingleOrDefaultAsync(x => x.Code == docCode && x.IsActive, ct) ?? throw new DomainException($"Tipo documental '{item.TipoDocumentoCodigo}' inexistente.");
            matrix.Items.Add(new DocumentRequirementMatrixItemEntity { DocumentTypeId = docType.Id, IsMandatory = item.Obligatorio, IsCritical = item.Critico, BlocksAvailability = item.BloqueaDisponibilidad, RequiresExpirationDate = item.RequiereFechaVencimiento, AlertDays = Math.Max(0, item.DiasAnticipacion) });
        }
        db.DocumentRequirementMatrices.Add(matrix); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        matrix.AssetType = type; matrix.EquipmentFamily = family;
        foreach (var item in matrix.Items) item.DocumentType = await db.DocumentTypes.AsNoTracking().SingleAsync(x => x.Id == item.DocumentTypeId, ct);
        return Map(matrix);
    }

    private static DocumentRequirementMatrixResponse Map(DocumentRequirementMatrixEntity x) => new(x.Id.ToString("D"), x.Code, x.VersionNumber, x.AssetType.Code, x.EquipmentFamily?.Code, x.ValidFrom, x.ValidTo, x.Status, x.CreatedByUserId, x.ChangeReason, x.Items.OrderBy(i => i.DocumentType.Code).Select(i => new DocumentRequirementMatrixItemResponse(i.Id.ToString("D"), i.DocumentType.Code, i.IsMandatory, i.IsCritical, i.BlocksAvailability, i.RequiresExpirationDate, i.AlertDays)).ToArray());
    private static void EnsureView(UserAccessContext user) { if (user.Roles.Any(r => r is AuthRoles.Admin or AuthRoles.Planner or AuthRoles.MaintenanceSupervisor or AuthRoles.Management)) return; throw new UnauthorizedAccessException("No tiene permiso para ver matrices documentales."); }
    private static void EnsureManage(UserAccessContext user) { if (user.Permissions.Contains(AuthPermissions.ManageDocumentRequirements, StringComparer.OrdinalIgnoreCase) || user.Roles.Contains(AuthRoles.Planner, StringComparer.OrdinalIgnoreCase)) return; throw new UnauthorizedAccessException("Solo Planificacion puede versionar matrices documentales."); }
}
