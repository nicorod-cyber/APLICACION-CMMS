using System.Globalization;
using System.Text.Json;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.Documents;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceCMMS.Infrastructure.Documents;

public sealed class DocumentService : IDocumentService
{
    private readonly CmmsDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly IAuthorizationPolicyService _authorizationPolicyService;

    public DocumentService(
        CmmsDbContext dbContext,
        IAuditService auditService,
        IAuthorizationPolicyService authorizationPolicyService)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _authorizationPolicyService = authorizationPolicyService;
    }

    public async Task<IReadOnlyCollection<DocumentTypeResponse>> ListTypesAsync(CancellationToken cancellationToken)
    {
        var types = await _dbContext.DocumentTypes
            .AsNoTracking()
            .OrderBy(type => type.Name)
            .ToArrayAsync(cancellationToken);
        return types.Select(ToTypeResponse).ToArray();
    }

    public async Task<DocumentTypeResponse> CreateTypeAsync(
        CreateDocumentTypeRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanConfigure(user);
        ValidateRequired(request.Codigo, nameof(request.Codigo));
        ValidateRequired(request.Nombre, nameof(request.Nombre));

        var code = NormalizeCode(request.Codigo);
        if (await _dbContext.DocumentTypes.AnyAsync(type => type.Code == code, cancellationToken))
        {
            throw new DomainException($"Ya existe el tipo documental '{request.Codigo}'.");
        }

        var entity = new DocumentTypeEntity
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = request.Nombre.Trim(),
            AppliesTo = request.AplicaA?.ToString(),
            IsMandatory = request.Obligatorio,
            IsCritical = request.Critico,
            BlocksAvailability = request.BloqueaDisponibilidad,
            AlertDays = Math.Max(0, request.PlazoAlertaDias),
            ResponsibleRoles = JoinList(request.RolesResponsables ?? []),
            RequiresAlertPdf = request.RequierePdfAlerta,
            HtmlTemplateCode = EmptyToNull(request.PlantillaHtmlCodigo),
            IsActive = request.Activo,
            CreatedByUserId = user.UserId
        };

        _dbContext.DocumentTypes.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(user, "document_type.created", entity.Code, null, Serialize(entity), "Tipo documental creado", cancellationToken);

        return ToTypeResponse(entity);
    }

    public async Task<DocumentTypeResponse?> UpdateTypeAsync(
        string code,
        UpdateDocumentTypeRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanConfigure(user);
        ValidateRequired(request.Nombre, nameof(request.Nombre));
        DomainGuard.AgainstEmpty(request.Reason ?? string.Empty, "reason");

        var normalized = NormalizeCode(code);
        var entity = await _dbContext.DocumentTypes.FirstOrDefaultAsync(type => type.Code == normalized, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var previous = Serialize(entity);
        entity.Name = request.Nombre.Trim();
        entity.AppliesTo = request.AplicaA?.ToString();
        entity.IsMandatory = request.Obligatorio;
        entity.IsCritical = request.Critico;
        entity.BlocksAvailability = request.BloqueaDisponibilidad;
        entity.AlertDays = Math.Max(0, request.PlazoAlertaDias);
        entity.ResponsibleRoles = JoinList(request.RolesResponsables ?? []);
        entity.RequiresAlertPdf = request.RequierePdfAlerta;
        entity.HtmlTemplateCode = EmptyToNull(request.PlantillaHtmlCodigo);
        entity.IsActive = request.Activo;
        entity.UpdatedByUserId = user.UserId;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(user, "document_type.updated", entity.Code, previous, Serialize(entity), request.Reason, cancellationToken);

        return ToTypeResponse(entity);
    }

    public async Task<IReadOnlyCollection<DocumentResponse>> ListAsync(
        DocumentQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanViewFaenaFilter(query.FaenaCodigo, user);
        var documents = await BaseDocumentsQuery().ToArrayAsync(cancellationToken);
        return documents
            .Where(document => MatchesEntity(document, query, user))
            .Select(ToDocumentResponse)
            .Where(document => MatchesResponse(document, query))
            .OrderBy(document => document.EntidadTipo)
            .ThenBy(document => document.EntidadCodigo, StringComparer.OrdinalIgnoreCase)
            .ThenBy(document => document.TipoDocumento, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<DocumentResponse?> GetByIdAsync(
        string id,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        var document = await FindDocumentAsync(id, tracking: false, cancellationToken);
        if (document is null)
        {
            return null;
        }

        var response = ToDocumentResponse(document);
        EnsureCanViewDocument(document, user);
        return response;
    }

    public async Task<DocumentResponse> CreateAsync(
        CreateDocumentRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanManage(user);
        ValidateRequired(request.EntidadCodigo, nameof(request.EntidadCodigo));
        ValidateRequired(request.TipoDocumento, nameof(request.TipoDocumento));

        var type = await ResolveActiveTypeAsync(request.TipoDocumento, cancellationToken);
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var document = new DocumentEntity
        {
            Id = Guid.NewGuid(),
            Code = $"DOC-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..31],
            Title = $"{type.Name} {request.EntidadCodigo}".Trim(),
            DocumentTypeId = type.Id,
            DocumentType = type,
            Status = HasFile(request.ArchivoKey, request.SharePointUrl)
                ? DocumentLifecycleStatus.PendienteValidacion.ToString()
                : DocumentLifecycleStatus.PendienteCarga.ToString(),
            IssueDate = request.FechaEmision,
            ExpiresOn = request.FechaVencimiento,
            IsCurrent = true,
            IsAnnulled = false,
            CreatedByUserId = user.UserId,
            IsCritical = request.Critico ?? type.IsCritical,
            IsMandatory = request.Obligatorio ?? type.IsMandatory,
            BlocksAvailability = request.BloqueaDisponibilidad ?? type.BlocksAvailability,
            ChangeReason = request.Reason
        };

        _dbContext.Documents.Add(document);
        await AssignInitialEntityAsync(document, request, user, now, cancellationToken);

        if (HasFile(request.ArchivoKey, request.SharePointUrl))
        {
            var file = CreateFile(request.ArchivoKey, request.SharePointUrl, request.NombreOriginal, request.TipoMime, request.TamanoBytes, request.Checksum, user);
            _dbContext.Files.Add(file);
            _dbContext.DocumentVersions.Add(new DocumentVersionEntity
            {
                Id = Guid.NewGuid(),
                DocumentId = document.Id,
                Document = document,
                VersionNumber = 1,
                VersionCode = "1",
                FileId = file.Id,
                File = file,
                UploadedAtUtc = now,
                UploadedByUserId = user.UserId,
                Observations = request.Reason,
                IsCurrent = true,
                Status = "vigente",
                IssueDate = request.FechaEmision,
                ExpiresOn = request.FechaVencimiento,
                ValidationStatus = DocumentLifecycleStatus.PendienteValidacion.ToString(),
                CorrectionResponsibleUserId = user.UserId,
                CorrectionStatus = "PENDIENTE_REVISION"
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await RecordAuditAsync(user, "document.created", document.Id.ToString("D"), null, Serialize(document), request.Reason ?? "Documento cargado", cancellationToken);

        return ToDocumentResponse((await FindDocumentAsync(document.Id.ToString("D"), tracking: false, cancellationToken))!);
    }

    public async Task<DocumentResponse?> UpdateAsync(
        string id,
        UpdateDocumentRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanManage(user);
        DomainGuard.AgainstEmpty(request.Reason ?? string.Empty, "reason");

        var document = await FindDocumentAsync(id, tracking: true, cancellationToken);
        if (document is null)
        {
            return null;
        }

        var previous = Serialize(document);
        var existingResponse = ToDocumentResponse(document);
        EnsureCanViewDocument(document, user);
        EnsureCanChangeDocument(existingResponse);
        EnsureExpiryCanChange(existingResponse, request.FechaVencimiento, user);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        document.IssueDate = request.FechaEmision;
        document.ExpiresOn = request.FechaVencimiento;
        document.IsCritical = request.Critico ?? document.IsCritical;
        document.IsMandatory = request.Obligatorio ?? document.IsMandatory;
        document.BlocksAvailability = request.BloqueaDisponibilidad ?? document.BlocksAvailability;
        document.UpdatedAtUtc = DateTimeOffset.UtcNow;
        document.UpdatedByUserId = user.UserId;
        document.ChangeReason = request.Reason;

        if (HasFile(request.ArchivoKey, request.SharePointUrl))
        {
            await AddNewVersionAsync(document, request.ArchivoKey, request.SharePointUrl, null, null, null, null, request.Reason, user, cancellationToken);
            document.Status = DocumentLifecycleStatus.PendienteValidacion.ToString();
            document.ValidatedAtUtc = null;
            document.ValidatedByUserId = null;
            document.RejectedAtUtc = null;
            document.RejectedByUserId = null;
            document.RejectReason = null;
        }
        else if (document.Versions.Count == 0)
        {
            document.Status = DocumentLifecycleStatus.PendienteCarga.ToString();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await RecordAuditAsync(user, "document.updated", id, previous, Serialize(document), request.Reason, cancellationToken);

        return ToDocumentResponse((await FindDocumentAsync(id, tracking: false, cancellationToken))!);
    }

    public async Task<DocumentResponse?> ValidateAsync(
        string id,
        ValidateDocumentRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanValidate(user);
        var document = await FindDocumentAsync(id, tracking: true, cancellationToken);
        if (document is null)
        {
            return null;
        }

        var response = ToDocumentResponse(document);
        EnsureCanViewDocument(document, user);
        if (document.Versions.All(version => !version.IsCurrent))
        {
            throw new DomainException("No se puede validar un documento sin archivo o enlace.");
        }

        var currentVersion = document.Versions.Single(version => version.IsCurrent);
        if (!string.Equals(currentVersion.ValidationStatus, DocumentLifecycleStatus.PendienteValidacion.ToString(), StringComparison.OrdinalIgnoreCase)) throw new DomainException("Solo una version pendiente de validacion puede ser validada.");
        var previous = Serialize(document);
        var validatedAt = DateTimeOffset.UtcNow;
        document.Status = DocumentLifecycleStatus.Vigente.ToString();
        document.ValidatedByUserId = user.UserId;
        document.ValidatedAtUtc = validatedAt;
        currentVersion.ValidationStatus = DocumentLifecycleStatus.Vigente.ToString();
        currentVersion.ValidatedByUserId = user.UserId;
        currentVersion.ValidatedAtUtc = validatedAt;
        currentVersion.RejectedByUserId = null;
        currentVersion.RejectedAtUtc = null;
        currentVersion.RejectReason = null;
        currentVersion.CorrectionStatus = "CERRADO_VALIDADO";
        document.RejectedByUserId = null;
        document.RejectedAtUtc = null;
        document.RejectReason = null;
        document.ExpiryDateValidated = true;
        document.UpdatedAtUtc = DateTimeOffset.UtcNow;
        document.UpdatedByUserId = user.UserId;
        document.ChangeReason = request.Comments;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(user, "document.validated", id, previous, Serialize(document), request.Comments ?? "Documento validado", cancellationToken);

        return ToDocumentResponse((await FindDocumentAsync(id, tracking: false, cancellationToken))!);
    }

    public async Task<DocumentResponse?> RejectAsync(
        string id,
        RejectDocumentRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanValidate(user);
        DomainGuard.AgainstEmpty(request.Reason, nameof(request.Reason));
        var document = await FindDocumentAsync(id, tracking: true, cancellationToken);
        if (document is null)
        {
            return null;
        }

        var response = ToDocumentResponse(document);
        EnsureCanViewDocument(document, user);
        var currentVersion = document.Versions.SingleOrDefault(version => version.IsCurrent) ?? throw new DomainException("No existe una version vigente para rechazar.");
        if (!string.Equals(currentVersion.ValidationStatus, DocumentLifecycleStatus.PendienteValidacion.ToString(), StringComparison.OrdinalIgnoreCase)) throw new DomainException("Solo una version pendiente de validacion puede ser rechazada.");
        var previous = Serialize(document);
        var rejectedAt = DateTimeOffset.UtcNow;
        document.Status = DocumentLifecycleStatus.Rechazado.ToString();
        document.RejectedByUserId = user.UserId;
        document.RejectedAtUtc = rejectedAt;
        document.RejectReason = request.Reason;
        currentVersion.ValidationStatus = DocumentLifecycleStatus.Rechazado.ToString();
        currentVersion.RejectedByUserId = user.UserId;
        currentVersion.RejectedAtUtc = rejectedAt;
        currentVersion.RejectReason = request.Reason;
        currentVersion.CorrectionResponsibleUserId = currentVersion.UploadedByUserId;
        currentVersion.CorrectionStatus = "OBSERVADO";
        currentVersion.CorrectionObservation = request.Reason;
        currentVersion.CorrectionCycleId ??= Guid.NewGuid();
        document.UpdatedAtUtc = DateTimeOffset.UtcNow;
        document.UpdatedByUserId = user.UserId;
        document.ChangeReason = request.Reason;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(user, "document.rejected", id, previous, Serialize(document), request.Reason, cancellationToken);

        return ToDocumentResponse((await FindDocumentAsync(id, tracking: false, cancellationToken))!);
    }

    public async Task<DocumentResponse?> ReplaceAsync(
        string id,
        ReplaceDocumentRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanManage(user);
        DomainGuard.AgainstEmpty(request.Reason, nameof(request.Reason));
        var document = await FindDocumentAsync(id, tracking: true, cancellationToken);
        if (document is null)
        {
            return null;
        }

        var response = ToDocumentResponse(document);
        EnsureCanViewDocument(document, user);
        var previous = Serialize(document);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        document.IssueDate = request.FechaEmision;
        document.ExpiresOn = request.FechaVencimiento;
        document.Status = HasFile(request.ArchivoKey, request.SharePointUrl)
            ? DocumentLifecycleStatus.PendienteValidacion.ToString()
            : DocumentLifecycleStatus.PendienteCarga.ToString();
        document.ValidatedAtUtc = null;
        document.ValidatedByUserId = null;
        document.RejectedAtUtc = null;
        document.RejectedByUserId = null;
        document.RejectReason = null;
        document.ExpiryDateValidated = false;
        document.UpdatedAtUtc = DateTimeOffset.UtcNow;
        document.UpdatedByUserId = user.UserId;
        document.ChangeReason = request.Reason;

        if (HasFile(request.ArchivoKey, request.SharePointUrl))
        {
            await AddNewVersionAsync(document, request.ArchivoKey, request.SharePointUrl, null, null, null, null, request.Reason, user, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await RecordAuditAsync(user, "document.replaced", id, previous, Serialize(document), request.Reason, cancellationToken);

        return ToDocumentResponse((await FindDocumentAsync(id, tracking: false, cancellationToken))!);
    }

    public async Task<IReadOnlyCollection<DocumentVersionResponse>> ListVersionsAsync(
        string id,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        var document = await FindDocumentAsync(id, tracking: false, cancellationToken);
        if (document is null)
        {
            return [];
        }

        EnsureCanViewDocument(document, user);
        return document.Versions
            .OrderBy(version => version.VersionNumber)
            .Select(version => new DocumentVersionResponse(
                version.Id.ToString("D"),
                document.Id.ToString("D"),
                version.VersionNumber,
                version.VersionCode,
                version.FileId.ToString("D"),
                version.File.FileKey,
                version.File.LogicalUri,
                version.UploadedAtUtc,
                version.UploadedByUserId,
                version.Observations,
                version.IsCurrent,
                version.IssueDate,
                version.ExpiresOn,
                version.ValidationStatus,
                version.ValidatedByUserId,
                version.ValidatedAtUtc,
                version.RejectedByUserId,
                version.RejectedAtUtc,
                version.RejectReason,
                version.ReplacesVersionId?.ToString("D"),
                version.CorrectionResponsibleUserId,
                version.CorrectionStatus,
                version.CorrectionObservation,
                version.CorrectionCycleId?.ToString("D")))
            .ToArray();
    }

    public async Task<DocumentResponse?> AssignAssetsAsync(
        string id,
        AssignDocumentAssetsRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanManage(user);
        var document = await FindDocumentAsync(id, tracking: true, cancellationToken);
        if (document is null)
        {
            return null;
        }

        EnsureCanViewDocument(document, user);
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        foreach (var assetCode in request.ActivoCodigos.Where(code => !string.IsNullOrWhiteSpace(code)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var asset = await ResolveAssetAsync(assetCode, user, cancellationToken);
            if (document.Assets.Any(link => link.AssetId == asset.Id && link.IsActive))
            {
                continue;
            }

            _dbContext.DocumentAssets.Add(new DocumentAssetEntity
            {
                Id = Guid.NewGuid(),
                DocumentId = document.Id,
                AssetId = asset.Id,
                IsActive = true,
                AssignedAtUtc = DateTimeOffset.UtcNow,
                AssignedByUserId = user.UserId
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await RecordAuditAsync(user, "document.asset.assigned", id, null, JsonSerializer.Serialize(request.ActivoCodigos), request.Reason ?? "Asociacion de activos", cancellationToken);

        return ToDocumentResponse((await FindDocumentAsync(id, tracking: false, cancellationToken))!);
    }

    public async Task<DocumentResponse?> UnassignAssetAsync(
        string id,
        string assetCode,
        UnassignDocumentAssetRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanManage(user);
        DomainGuard.AgainstEmpty(request.Reason, nameof(request.Reason));
        var document = await FindDocumentAsync(id, tracking: true, cancellationToken);
        if (document is null)
        {
            return null;
        }

        EnsureCanViewDocument(document, user);
        var link = document.Assets.FirstOrDefault(item => item.IsActive && SameCode(item.Asset.Code, assetCode));
        if (link is null)
        {
            return ToDocumentResponse(document);
        }

        link.IsActive = false;
        link.UnassignedAtUtc = DateTimeOffset.UtcNow;
        link.UnassignedByUserId = user.UserId;
        link.UnassignedReason = request.Reason;
        link.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(user, "document.asset.unassigned", id, assetCode, null, request.Reason, cancellationToken);

        return ToDocumentResponse((await FindDocumentAsync(id, tracking: false, cancellationToken))!);
    }

    public async Task<DocumentResponse?> AnnulAsync(
        string id,
        AnnulDocumentRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanManage(user);
        DomainGuard.AgainstEmpty(request.Reason, nameof(request.Reason));
        var document = await FindDocumentAsync(id, tracking: true, cancellationToken);
        if (document is null)
        {
            return null;
        }

        EnsureCanViewDocument(document, user);
        var previous = Serialize(document);
        document.Status = DocumentLifecycleStatus.Anulado.ToString();
        document.IsAnnulled = true;
        document.IsCurrent = false;
        document.IsHistorical = true;
        document.AnnulledByUserId = user.UserId;
        document.AnnulledAtUtc = DateTimeOffset.UtcNow;
        document.AnnulReason = request.Reason;
        document.UpdatedAtUtc = DateTimeOffset.UtcNow;
        document.UpdatedByUserId = user.UserId;
        document.ChangeReason = request.Reason;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(user, "document.annulled", id, previous, Serialize(document), request.Reason, cancellationToken);

        return ToDocumentResponse((await FindDocumentAsync(id, tracking: false, cancellationToken))!);
    }

    public async Task<IReadOnlyCollection<DocumentResponse>> GetExpiredAsync(
        string? faenaCodigo,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        return await ListAsync(new DocumentQuery(FaenaCodigo: faenaCodigo, Estado: DocumentLifecycleStatus.Vencido), user, cancellationToken);
    }

    public async Task<IReadOnlyCollection<DocumentResponse>> GetExpiringAsync(
        string? faenaCodigo,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        return await ListAsync(new DocumentQuery(FaenaCodigo: faenaCodigo, Estado: DocumentLifecycleStatus.PorVencer), user, cancellationToken);
    }

    public async Task<IReadOnlyCollection<DocumentMatrixRow>> GetMatrixAsync(
        string? faenaCodigo,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanViewFaenaFilter(faenaCodigo, user);
        var types = await _dbContext.DocumentTypes.AsNoTracking().Where(type => type.IsActive).ToArrayAsync(cancellationToken);
        var assets = await _dbContext.Assets.AsNoTracking().Include(asset => asset.Faena).ToArrayAsync(cancellationToken);
        var documents = await BaseDocumentsQuery().ToArrayAsync(cancellationToken);
        var responses = documents.Select(ToDocumentResponse).Where(item => !item.EsHistorico).ToArray();

        var rows = new List<DocumentMatrixRow>();
        foreach (var asset in assets.Where(asset => CanViewFaena(asset.Faena.Code, user) && (string.IsNullOrWhiteSpace(faenaCodigo) || SameCode(asset.Faena.Code, faenaCodigo))))
        {
            foreach (var type in types.Where(type => AppliesTo(type, DocumentEntityType.Activo)))
            {
                var document = responses
                    .Where(item => item.EntidadTipo == DocumentEntityType.Activo &&
                                   (item.EntidadCodigos?.Contains(asset.Code, StringComparer.OrdinalIgnoreCase) ?? SameCode(item.EntidadCodigo, asset.Code)) &&
                                   SameCode(item.TipoDocumento, type.Code))
                    .OrderByDescending(item => item.FechaCargaUtc)
                    .FirstOrDefault();

                rows.Add(new DocumentMatrixRow(
                    DocumentEntityType.Activo,
                    asset.Code,
                    asset.Name,
                    type.Code,
                    type.IsMandatory,
                    type.BlocksAvailability,
                    document?.Estado ?? DocumentLifecycleStatus.PendienteCarga,
                    document?.DocumentoId,
                    document?.FechaVencimiento,
                    document?.BloqueaDisponibilidadActual ?? false));
            }
        }

        return rows;
    }

    public async Task<DocumentDashboardSummary> GetSummaryAsync(
        string? faenaCodigo,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        var documents = await ListAsync(new DocumentQuery(FaenaCodigo: faenaCodigo, IncludeHistorical: true), user, cancellationToken);
        return new DocumentDashboardSummary(
            documents.Count,
            documents.Count(item => item.Estado == DocumentLifecycleStatus.Vigente),
            documents.Count(item => item.Estado == DocumentLifecycleStatus.PorVencer),
            documents.Count(item => item.Estado == DocumentLifecycleStatus.Vencido),
            documents.Count(item => item.Estado == DocumentLifecycleStatus.PendienteCarga),
            documents.Count(item => item.Estado == DocumentLifecycleStatus.PendienteValidacion),
            documents.Count(item => item.Estado == DocumentLifecycleStatus.Rechazado),
            documents.Count(item => item.Estado == DocumentLifecycleStatus.Reemplazado),
            documents.Count(item => item.Estado == DocumentLifecycleStatus.Anulado),
            documents.Count(item => item.BloqueaDisponibilidadActual));
    }

    private IQueryable<DocumentEntity> BaseDocumentsQuery()
    {
        return _dbContext.Documents
            .AsSplitQuery()
            .Include(document => document.DocumentType)
            .Include(document => document.Versions).ThenInclude(version => version.File)
            .Include(document => document.Assets).ThenInclude(link => link.Asset).ThenInclude(asset => asset.Faena)
            .Include(document => document.Faenas).ThenInclude(link => link.Faena)
            .Include(document => document.WorkOrders).ThenInclude(link => link.WorkOrder).ThenInclude(workOrder => workOrder.Faena);
    }
    private async Task<DocumentEntity?> FindDocumentAsync(string id, bool tracking, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var documentId))
        {
            return null;
        }

        var query = BaseDocumentsQuery();
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(document => document.Id == documentId, cancellationToken);
    }

    private async Task<DocumentTypeEntity> ResolveActiveTypeAsync(string code, CancellationToken cancellationToken)
    {
        var normalized = NormalizeCode(code);
        var type = await _dbContext.DocumentTypes.FirstOrDefaultAsync(item => item.Code == normalized, cancellationToken);
        if (type is null)
        {
            throw new DomainException($"No existe el tipo documental '{code}'.");
        }

        if (!type.IsActive)
        {
            throw new DomainException($"El tipo documental '{code}' esta inactivo y no puede asignarse a nuevos documentos.");
        }

        return type;
    }

    private async Task AssignInitialEntityAsync(
        DocumentEntity document,
        CreateDocumentRequest request,
        UserAccessContext user,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var entityCodes = (request.EntidadCodigos is { Count: > 0 } ? request.EntidadCodigos : [request.EntidadCodigo])
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (request.EntidadTipo == DocumentEntityType.Activo)
        {
            foreach (var assetCode in entityCodes)
            {
                var asset = await ResolveAssetAsync(assetCode, user, cancellationToken);
                _dbContext.DocumentAssets.Add(new DocumentAssetEntity
                {
                    Id = Guid.NewGuid(),
                    DocumentId = document.Id,
                    Document = document,
                    AssetId = asset.Id,
                    Asset = asset,
                    IsActive = true,
                    AssignedAtUtc = now,
                    AssignedByUserId = user.UserId
                });
            }

            return;
        }

        if (request.EntidadTipo == DocumentEntityType.OT)
        {
            foreach (var workOrderNumber in entityCodes)
            {
                var workOrder = await ResolveWorkOrderAsync(workOrderNumber, user, cancellationToken);
                _dbContext.DocumentWorkOrders.Add(new DocumentWorkOrderEntity
                {
                    Id = Guid.NewGuid(),
                    DocumentId = document.Id,
                    Document = document,
                    WorkOrderId = workOrder.Id,
                    WorkOrder = workOrder,
                    IsActive = true,
                    AssignedAtUtc = now,
                    AssignedByUserId = user.UserId
                });
            }

            return;
        }

        foreach (var faenaCode in entityCodes)
        {
            var faena = await ResolveFaenaAsync(faenaCode, user, cancellationToken);
            _dbContext.DocumentFaenas.Add(new DocumentFaenaEntity
            {
                Id = Guid.NewGuid(),
                DocumentId = document.Id,
                Document = document,
                FaenaId = faena.Id,
                Faena = faena,
                IsActive = true,
                AssignedAtUtc = now,
                AssignedByUserId = user.UserId
            });
        }
    }
    private async Task<AssetEntity> ResolveAssetAsync(string code, UserAccessContext user, CancellationToken cancellationToken)
    {
        var normalized = code.Trim();
        var asset = await _dbContext.Assets
            .Include(item => item.Faena)
            .FirstOrDefaultAsync(item => item.Code == normalized, cancellationToken);
        if (asset is null)
        {
            throw new DomainException($"No existe el activo '{code}'.");
        }

        if (!CanViewFaena(asset.Faena.Code, user))
        {
            throw new UnauthorizedAccessException("El usuario no tiene acceso al activo solicitado.");
        }

        return asset;
    }

    private async Task<FaenaEntity> ResolveFaenaAsync(string code, UserAccessContext user, CancellationToken cancellationToken)
    {
        var normalized = code.Trim();
        var faena = await _dbContext.Faenas.FirstOrDefaultAsync(item => item.Code == normalized, cancellationToken);
        if (faena is null)
        {
            throw new DomainException($"No existe la faena '{code}'.");
        }

        if (!CanViewFaena(faena.Code, user))
        {
            throw new UnauthorizedAccessException("El usuario no tiene acceso a la faena solicitada.");
        }

        return faena;
    }

    private async Task<WorkOrderEntity> ResolveWorkOrderAsync(string number, UserAccessContext user, CancellationToken cancellationToken)
    {
        var normalized = number.Trim();
        var workOrder = await _dbContext.WorkOrders
            .Include(item => item.Faena)
            .FirstOrDefaultAsync(item => item.WorkOrderNumber == normalized, cancellationToken);
        if (workOrder is null)
        {
            throw new DomainException($"No existe la OT '{number}'.");
        }

        if (!CanViewFaena(workOrder.Faena.Code, user))
        {
            throw new UnauthorizedAccessException("El usuario no tiene acceso a la OT solicitada.");
        }

        return workOrder;
    }

    private async Task AddNewVersionAsync(
        DocumentEntity document,
        string? fileKey,
        string? sharePointUrl,
        string? originalName,
        string? mimeType,
        long? sizeBytes,
        string? checksum,
        string? observations,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        var replacedVersion = document.Versions.SingleOrDefault(version => version.IsCurrent);
        foreach (var version in document.Versions.Where(version => version.IsCurrent))
        {
            version.IsCurrent = false;
            version.Status = "historico";
            version.UpdatedAtUtc = DateTimeOffset.UtcNow;
            if (string.Equals(version.ValidationStatus, DocumentLifecycleStatus.Rechazado.ToString(), StringComparison.OrdinalIgnoreCase)) version.CorrectionStatus = "CORREGIDO_NUEVA_VERSION";
        }

        var file = CreateFile(fileKey, sharePointUrl, originalName, mimeType, sizeBytes, checksum, user);
        var nextVersion = document.Versions.Count == 0 ? 1 : document.Versions.Max(version => version.VersionNumber) + 1;
        _dbContext.Files.Add(file);
        _dbContext.DocumentVersions.Add(new DocumentVersionEntity
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            VersionNumber = nextVersion,
            VersionCode = nextVersion.ToString(CultureInfo.InvariantCulture),
            FileId = file.Id,
            File = file,
            UploadedAtUtc = DateTimeOffset.UtcNow,
            UploadedByUserId = user.UserId,
            Observations = observations,
            IsCurrent = true,
            Status = "vigente",
            IssueDate = document.IssueDate,
            ExpiresOn = document.ExpiresOn,
            ValidationStatus = DocumentLifecycleStatus.PendienteValidacion.ToString(),
            ReplacesVersionId = replacedVersion?.Id,
            CorrectionResponsibleUserId = user.UserId,
            CorrectionStatus = "PENDIENTE_REVISION",
            CorrectionCycleId = replacedVersion?.CorrectionCycleId ?? Guid.NewGuid()
        });

        await RecordAuditAsync(user, "document.version.created", document.Id.ToString("D"), null, Serialize(file), observations ?? "Nueva version documental", cancellationToken);
    }

    private static FileMetadataEntity CreateFile(
        string? fileKey,
        string? sharePointUrl,
        string? originalName,
        string? mimeType,
        long? sizeBytes,
        string? checksum,
        UserAccessContext user)
    {
        var key = EmptyToNull(fileKey) ?? EmptyToNull(sharePointUrl) ?? Guid.NewGuid().ToString("N");
        var uri = EmptyToNull(sharePointUrl) ?? key;
        var fileName = EmptyToNull(originalName) ?? Path.GetFileName(key) ?? key;
        var provider = uri.StartsWith("http", StringComparison.OrdinalIgnoreCase) || uri.StartsWith("sharepoint:", StringComparison.OrdinalIgnoreCase)
            ? "ManualLink"
            : "LocalSimulation";

        return new FileMetadataEntity
        {
            Id = Guid.NewGuid(),
            FileKey = key,
            FileName = fileName,
            StoredFileName = fileName,
            Extension = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant(),
            Provider = provider,
            StorageMode = provider,
            Purpose = "Document",
            Module = "Documents",
            EntityType = "Document",
            EntityId = key,
            LogicalUri = uri,
            LogicalPath = key,
            MimeType = EmptyToNull(mimeType),
            SizeBytes = sizeBytes,
            Checksum = EmptyToNull(checksum),
            Status = "Stored",
            FileVersion = 1,
            AuthorUserId = user.UserId,
            MetadataJson = JsonSerializer.Serialize(new { binaryStoredInPostgreSql = false })
        };
    }
    private static DocumentTypeResponse ToTypeResponse(DocumentTypeEntity entity)
    {
        return new DocumentTypeResponse(
            entity.Code,
            entity.Name,
            ParseEntityTypeNullable(entity.AppliesTo),
            entity.IsMandatory,
            entity.IsCritical,
            entity.BlocksAvailability,
            entity.AlertDays,
            SplitList(entity.ResponsibleRoles),
            entity.RequiresAlertPdf,
            entity.HtmlTemplateCode,
            entity.IsActive);
    }

    private static DocumentResponse ToDocumentResponse(DocumentEntity entity)
    {
        var currentVersion = entity.Versions.OrderByDescending(version => version.IsCurrent).ThenByDescending(version => version.VersionNumber).FirstOrDefault();
        var activeAssets = entity.Assets.Where(link => link.IsActive).Select(link => link.Asset.Code).OrderBy(code => code, StringComparer.OrdinalIgnoreCase).ToArray();
        var activeWorkOrders = entity.WorkOrders.Where(link => link.IsActive).Select(link => link.WorkOrder.WorkOrderNumber).OrderBy(code => code, StringComparer.OrdinalIgnoreCase).ToArray();
        var activeFaenas = entity.Faenas.Where(link => link.IsActive).Select(link => link.Faena.Code).OrderBy(code => code, StringComparer.OrdinalIgnoreCase).ToArray();
        var entityType = activeAssets.Length > 0 ? DocumentEntityType.Activo : activeWorkOrders.Length > 0 ? DocumentEntityType.OT : DocumentEntityType.Faena;
        var entityCodes = activeAssets.Length > 0 ? activeAssets : activeWorkOrders.Length > 0 ? activeWorkOrders : activeFaenas;
        var rawStatus = ParseLifecycle(entity.Status);
        var compliance = DocumentComplianceCalculator.Evaluate(rawStatus.ToString(), currentVersion?.ExpiresOn ?? entity.ExpiresOn, entity.DocumentType.AlertDays, currentVersion is not null, entity.BlocksAvailability);
        var effectiveStatus = compliance.Status;
        var blocksNow = compliance.BlocksAvailability;

        return new DocumentResponse(
            entity.Id.ToString("D"),
            entityType,
            entityCodes.FirstOrDefault() ?? string.Empty,
            entity.DocumentType.Code,
            effectiveStatus,
            currentVersion?.IssueDate ?? entity.IssueDate,
            currentVersion?.ExpiresOn ?? entity.ExpiresOn,
            currentVersion?.File.FileKey,
            currentVersion?.File.LogicalUri,
            entity.IsCritical,
            entity.IsMandatory,
            entity.BlocksAvailability,
            entity.IsHistorical,
            entity.ExpiryDateValidated,
            entity.ValidatedByUserId,
            entity.ValidatedAtUtc,
            entity.RejectedByUserId,
            entity.RejectedAtUtc,
            entity.RejectReason,
            entity.ReplacesDocumentId?.ToString("D"),
            entity.ReplacedByDocumentId?.ToString("D"),
            entity.AnnulledByUserId,
            entity.AnnulledAtUtc,
            entity.AnnulReason,
            currentVersion?.UploadedAtUtc ?? entity.CreatedAtUtc,
            currentVersion?.UploadedByUserId ?? entity.CreatedByUserId,
            compliance.DaysToExpire,
            blocksNow,
            entityCodes,
            currentVersion?.VersionNumber,
            currentVersion?.FileId.ToString("D"));
    }

    private static DocumentLifecycleStatus ResolveStatus(DocumentLifecycleStatus rawStatus, DateOnly? expiresOn, int alertDays, bool hasCurrentFile) =>
        DocumentComplianceCalculator.Evaluate(rawStatus.ToString(), expiresOn, alertDays, hasCurrentFile, false).Status;

    private static bool MatchesEntity(DocumentEntity document, DocumentQuery query, UserAccessContext user)
    {
        var activeAssets = document.Assets.Where(link => link.IsActive).ToArray();
        var activeFaenas = document.Faenas.Where(link => link.IsActive).ToArray();
        var activeWorkOrders = document.WorkOrders.Where(link => link.IsActive).ToArray();
        var faenaCodes = activeAssets
            .Select(link => link.Asset.Faena.Code)
            .Concat(activeFaenas.Select(link => link.Faena.Code))
            .Concat(activeWorkOrders.Select(link => link.WorkOrder.Faena.Code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (faenaCodes.Length > 0 && !faenaCodes.Any(code => CanViewFaena(code, user)))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.FaenaCodigo) && !faenaCodes.Any(code => SameCode(code, query.FaenaCodigo)))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.EntidadCodigo))
        {
            var entityCodes = activeAssets.Select(link => link.Asset.Code)
                .Concat(activeFaenas.Select(link => link.Faena.Code))
                .Concat(activeWorkOrders.Select(link => link.WorkOrder.WorkOrderNumber));
            if (!entityCodes.Any(code => SameCode(code, query.EntidadCodigo)))
            {
                return false;
            }
        }

        return true;
    }
    private static bool MatchesResponse(DocumentResponse document, DocumentQuery query)
    {
        if (!query.IncludeHistorical && document.EsHistorico)
        {
            return false;
        }

        if (document.Estado is DocumentLifecycleStatus.Reemplazado or DocumentLifecycleStatus.Anulado && !query.IncludeHistorical)
        {
            return false;
        }

        if (query.EntidadTipo.HasValue && document.EntidadTipo != query.EntidadTipo.Value)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.EntidadCodigo) &&
            !(document.EntidadCodigos?.Contains(query.EntidadCodigo, StringComparer.OrdinalIgnoreCase) ?? SameCode(document.EntidadCodigo, query.EntidadCodigo)))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.TipoDocumento) && !SameCode(document.TipoDocumento, query.TipoDocumento))
        {
            return false;
        }

        return !query.Estado.HasValue || document.Estado == query.Estado.Value;
    }

    private static bool AppliesTo(DocumentTypeEntity type, DocumentEntityType entityType)
    {
        return string.IsNullOrWhiteSpace(type.AppliesTo) || ParseEntityTypeNullable(type.AppliesTo) == entityType;
    }

    private static void EnsureCanViewDocument(DocumentEntity document, UserAccessContext user)
    {
        var faenaCodes = document.Assets.Where(link => link.IsActive)
            .Select(link => link.Asset.Faena.Code)
            .Concat(document.Faenas.Where(link => link.IsActive).Select(link => link.Faena.Code))
            .Concat(document.WorkOrders.Where(link => link.IsActive).Select(link => link.WorkOrder.Faena.Code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (faenaCodes.Length > 0 && !faenaCodes.Any(code => CanViewFaena(code, user)))
        {
            throw new UnauthorizedAccessException("El usuario no tiene acceso al documento solicitado.");
        }
    }
    private void EnsureCanManage(UserAccessContext user)
    {
        if (!_authorizationPolicyService.CanManageDocuments(user))
        {
            throw new UnauthorizedAccessException("El usuario no tiene permiso para gestionar documentos.");
        }
    }

    private void EnsureCanValidate(UserAccessContext user)
    {
        if (!_authorizationPolicyService.CanValidateDocuments(user))
        {
            throw new UnauthorizedAccessException("El usuario no tiene permiso para validar documentos.");
        }
    }

    private void EnsureCanConfigure(UserAccessContext user)
    {
        if (!_authorizationPolicyService.CanConfigureDocumentTypes(user))
        {
            throw new UnauthorizedAccessException("El usuario no tiene permiso para configurar tipos documentales.");
        }
    }

    private void EnsureCanViewFaenaFilter(string? faenaCode, UserAccessContext user)
    {
        if (!string.IsNullOrWhiteSpace(faenaCode) && !CanViewFaena(faenaCode, user))
        {
            throw new UnauthorizedAccessException("El usuario no tiene acceso a la faena solicitada.");
        }
    }

    private void EnsureExpiryCanChange(DocumentResponse document, DateOnly? nextExpiry, UserAccessContext user)
    {
        if (!document.FechaVencimientoValidada || document.FechaVencimiento == nextExpiry)
        {
            return;
        }

        if (!_authorizationPolicyService.CanChangeValidatedDocumentExpiry(user))
        {
            throw new DomainException("La fecha de vencimiento validada queda bloqueada y requiere permiso, motivo y auditoria.");
        }
    }

    private static void EnsureCanChangeDocument(DocumentResponse document)
    {
        if (document.Estado is DocumentLifecycleStatus.Reemplazado or DocumentLifecycleStatus.Anulado)
        {
            throw new DomainException("No se puede modificar un documento reemplazado o anulado.");
        }
    }

    private static bool CanViewFaena(string? faenaCode, UserAccessContext user)
    {
        return user.Roles.Contains(AuthRoles.Admin, StringComparer.OrdinalIgnoreCase) ||
               user.Permissions.Contains(AuthPermissions.Administration, StringComparer.OrdinalIgnoreCase) ||
               string.IsNullOrWhiteSpace(faenaCode) ||
               user.Faenas.Contains(faenaCode, StringComparer.OrdinalIgnoreCase);
    }

    private async Task RecordAuditAsync(
        UserAccessContext user,
        string action,
        string entityId,
        string? previousValue,
        string? newValue,
        string? reason,
        CancellationToken cancellationToken)
    {
        await _auditService.RecordAsync(new AuditEventRequest(
            user.UserId,
            action,
            AuditModules.Documents,
            "Document",
            entityId,
            previousValue,
            newValue,
            Severity: action.Contains("validated", StringComparison.OrdinalIgnoreCase) ||
                      action.Contains("replaced", StringComparison.OrdinalIgnoreCase) ||
                      action.Contains("annulled", StringComparison.OrdinalIgnoreCase)
                ? AuditSeverity.High
                : AuditSeverity.Medium,
            Reason: reason,
            Detail: reason), cancellationToken);
    }

    private static string Serialize(object entity)
    {
        return JsonSerializer.Serialize(entity, new JsonSerializerOptions
        {
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
        });
    }

    private static string NormalizeCode(string value)
    {
        return value.Trim().ToUpperInvariant();
    }

    private static bool SameCode(string? left, string? right)
    {
        return string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasFile(string? fileKey, string? sharePointUrl)
    {
        return !string.IsNullOrWhiteSpace(fileKey) || !string.IsNullOrWhiteSpace(sharePointUrl);
    }

    private static void ValidateRequired(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"El campo {fieldName} es obligatorio.");
        }
    }

    private static IReadOnlyCollection<string> SplitList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? JoinList(IEnumerable<string> values)
    {
        var clean = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return clean.Length == 0 ? null : string.Join(';', clean);
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static int? CalculateDaysToExpire(DateOnly? expiresOn)
    {
        return expiresOn.HasValue
            ? expiresOn.Value.DayNumber - DateOnly.FromDateTime(DateTime.UtcNow).DayNumber
            : null;
    }

    private static DocumentEntityType? ParseEntityTypeNullable(string? value)
    {
        return Enum.TryParse<DocumentEntityType>(value, ignoreCase: true, out var result)
            ? result
            : null;
    }

    private static DocumentLifecycleStatus ParseLifecycle(string? value)
    {
        return Enum.TryParse<DocumentLifecycleStatus>(value, ignoreCase: true, out var result)
            ? result
            : DocumentLifecycleStatus.PendienteCarga;
    }
}
