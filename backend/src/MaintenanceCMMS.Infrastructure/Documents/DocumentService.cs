using System.Globalization;
using System.Text.Json;
using MaintenanceCMMS.Application.Abstractions.Data;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.Documents;
using MaintenanceCMMS.Domain.Common;

namespace MaintenanceCMMS.Infrastructure.Documents;

public sealed class DocumentService : IDocumentService
{
    private const string DocumentTypesSchema = "document_types";
    private const string DocumentsSchema = "documentos";
    private const string AssetsSchema = "activos";
    private const string FaenasSchema = "faenas";
    private const string WorkOrdersSchema = "ordenes_trabajo";

    private readonly IDataProvider _dataProvider;
    private readonly IAuditService _auditService;
    private readonly IAuthorizationPolicyService _authorizationPolicyService;

    public DocumentService(
        IDataProvider dataProvider,
        IAuditService auditService,
        IAuthorizationPolicyService authorizationPolicyService)
    {
        _dataProvider = dataProvider;
        _auditService = auditService;
        _authorizationPolicyService = authorizationPolicyService;
    }

    public async Task<IReadOnlyCollection<DocumentTypeResponse>> ListTypesAsync(CancellationToken cancellationToken)
    {
        var rows = await _dataProvider.ReadRowsAsync(DocumentTypesSchema, cancellationToken);
        return rows
            .Select(ToTypeResponse)
            .Where(type => !string.IsNullOrWhiteSpace(type.Codigo))
            .OrderBy(type => type.Nombre, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<DocumentTypeResponse> CreateTypeAsync(
        CreateDocumentTypeRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanConfigure(user);
        ValidateRequired(request.Codigo, nameof(request.Codigo));
        ValidateRequired(request.Nombre, nameof(request.Nombre));

        var rows = (await _dataProvider.ReadRowsAsync(DocumentTypesSchema, cancellationToken)).ToList();
        if (rows.Any(row => SameCode(row.GetValue("Codigo"), request.Codigo)))
        {
            throw new DomainException($"Ya existe el tipo documental '{request.Codigo}'.");
        }

        var rowToCreate = TypeRow(
            request.Codigo,
            request.Nombre,
            request.AplicaA,
            request.Obligatorio,
            request.Critico,
            request.BloqueaDisponibilidad,
            request.PlazoAlertaDias,
            request.RolesResponsables ?? [],
            request.RequierePdfAlerta,
            request.PlantillaHtmlCodigo,
            request.Activo);

        rows.Add(rowToCreate);
        await _dataProvider.SaveRowsAsync(DocumentTypesSchema, rows, cancellationToken);
        await RecordAuditAsync(user, "document_type.created", request.Codigo, null, Serialize(rowToCreate), "Tipo documental creado", cancellationToken);

        return ToTypeResponse(rowToCreate);
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

        var rows = (await _dataProvider.ReadRowsAsync(DocumentTypesSchema, cancellationToken)).ToList();
        var index = FindIndex(rows, "Codigo", code);
        if (index < 0)
        {
            return null;
        }

        var existing = rows[index];
        var updated = TypeRow(
            existing.GetValue("Codigo") ?? code,
            request.Nombre,
            request.AplicaA,
            request.Obligatorio,
            request.Critico,
            request.BloqueaDisponibilidad,
            request.PlazoAlertaDias,
            request.RolesResponsables ?? [],
            request.RequierePdfAlerta,
            request.PlantillaHtmlCodigo,
            request.Activo);

        rows[index] = updated;
        await _dataProvider.SaveRowsAsync(DocumentTypesSchema, rows, cancellationToken);
        await RecordAuditAsync(user, "document_type.updated", code, Serialize(existing), Serialize(updated), request.Reason, cancellationToken);

        return ToTypeResponse(updated);
    }

    public async Task<IReadOnlyCollection<DocumentResponse>> ListAsync(
        DocumentQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        var context = await LoadContextAsync(cancellationToken);
        EnsureCanViewFaenaFilter(query.FaenaCodigo, user);

        return context.DocumentRows
            .Select(row => ToDocumentResponse(row, context.TypesByCode))
            .Where(document => Matches(document, query, context, user))
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
        var context = await LoadContextAsync(cancellationToken);
        var row = FindDocumentById(context.DocumentRows, id);
        if (row is null)
        {
            return null;
        }

        var document = ToDocumentResponse(row, context.TypesByCode);
        EnsureCanViewDocument(document, context, user);
        return document;
    }

    public async Task<DocumentResponse> CreateAsync(
        CreateDocumentRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanManage(user);
        ValidateRequired(request.EntidadCodigo, nameof(request.EntidadCodigo));
        ValidateRequired(request.TipoDocumento, nameof(request.TipoDocumento));

        var context = await LoadContextAsync(cancellationToken);
        await ValidateEntityAsync(request.EntidadTipo, request.EntidadCodigo, user, context, cancellationToken);

        var type = ResolveType(context.TypesByCode, request.TipoDocumento);
        var id = Guid.NewGuid().ToString("D");
        var row = DocumentRow(
            id,
            request.EntidadTipo,
            request.EntidadCodigo,
            request.TipoDocumento,
            HasFile(request.ArchivoKey, request.SharePointUrl) ? DocumentLifecycleStatus.PendienteValidacion : DocumentLifecycleStatus.PendienteCarga,
            request.FechaEmision,
            request.FechaVencimiento,
            request.ArchivoKey,
            request.SharePointUrl,
            request.Critico ?? type?.Critico ?? false,
            request.Obligatorio ?? type?.Obligatorio ?? false,
            request.BloqueaDisponibilidad ?? type?.BloqueaDisponibilidad ?? false,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            false,
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            user.UserId,
            request.Reason,
            false);

        var rows = context.DocumentRows.ToList();
        rows.Add(row);
        await _dataProvider.SaveRowsAsync(DocumentsSchema, rows, cancellationToken);
        await RecordAuditAsync(user, "document.created", id, null, Serialize(row), request.Reason ?? "Documento cargado", cancellationToken);

        return ToDocumentResponse(row, context.TypesByCode);
    }

    public async Task<DocumentResponse?> UpdateAsync(
        string id,
        UpdateDocumentRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanManage(user);
        DomainGuard.AgainstEmpty(request.Reason ?? string.Empty, "reason");

        var context = await LoadContextAsync(cancellationToken);
        var rows = context.DocumentRows.ToList();
        var index = FindDocumentIndex(rows, id);
        if (index < 0)
        {
            return null;
        }

        var existing = rows[index];
        var existingDocument = ToDocumentResponse(existing, context.TypesByCode);
        EnsureCanViewDocument(existingDocument, context, user);
        EnsureCanChangeDocument(existingDocument);
        EnsureExpiryCanChange(existingDocument, request.FechaVencimiento, user);

        var updated = DocumentRow(
            existingDocument.DocumentoId,
            existingDocument.EntidadTipo,
            existingDocument.EntidadCodigo,
            existingDocument.TipoDocumento,
            HasFile(request.ArchivoKey, request.SharePointUrl) ? DocumentLifecycleStatus.PendienteValidacion : DocumentLifecycleStatus.PendienteCarga,
            request.FechaEmision,
            request.FechaVencimiento,
            request.ArchivoKey,
            request.SharePointUrl,
            request.Critico ?? existingDocument.Critico,
            request.Obligatorio ?? existingDocument.Obligatorio,
            request.BloqueaDisponibilidad ?? existingDocument.BloqueaDisponibilidad,
            null,
            null,
            null,
            null,
            null,
            existingDocument.ReemplazaDocumentoId,
            existingDocument.ReemplazadoPorDocumentoId,
            existingDocument.EsHistorico,
            existingDocument.AnuladoPor,
            existingDocument.AnuladoEnUtc,
            existingDocument.MotivoAnulacion,
            existingDocument.FechaCargaUtc,
            existingDocument.CargadoPor,
            request.Reason,
            false);

        rows[index] = updated;
        await _dataProvider.SaveRowsAsync(DocumentsSchema, rows, cancellationToken);
        await RecordAuditAsync(user, "document.updated", id, Serialize(existing), Serialize(updated), request.Reason, cancellationToken);

        return ToDocumentResponse(updated, context.TypesByCode);
    }

    public async Task<DocumentResponse?> ValidateAsync(
        string id,
        ValidateDocumentRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanValidate(user);
        var context = await LoadContextAsync(cancellationToken);
        var rows = context.DocumentRows.ToList();
        var index = FindDocumentIndex(rows, id);
        if (index < 0)
        {
            return null;
        }

        var existing = rows[index];
        var document = ToDocumentResponse(existing, context.TypesByCode);
        EnsureCanViewDocument(document, context, user);
        if (!HasFile(document.ArchivoKey, document.SharePointUrl))
        {
            throw new DomainException("No se puede validar un documento sin archivo o enlace.");
        }

        var updated = WithValues(existing, new Dictionary<string, string?>
        {
            ["Estado"] = DocumentLifecycleStatus.Vigente.ToString(),
            ["ValidadoPor"] = user.UserId,
            ["ValidadoEnUtc"] = DateTimeOffset.UtcNow.UtcDateTime.ToString("O"),
            ["RechazadoPor"] = null,
            ["RechazadoEnUtc"] = null,
            ["MotivoRechazo"] = null,
            ["FechaVencimientoValidada"] = "true",
            ["MotivoCambio"] = request.Comments
        });

        rows[index] = updated;
        await _dataProvider.SaveRowsAsync(DocumentsSchema, rows, cancellationToken);
        await RecordAuditAsync(user, "document.validated", id, Serialize(existing), Serialize(updated), request.Comments ?? "Documento validado", cancellationToken);

        return ToDocumentResponse(updated, context.TypesByCode);
    }

    public async Task<DocumentResponse?> RejectAsync(
        string id,
        RejectDocumentRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanValidate(user);
        DomainGuard.AgainstEmpty(request.Reason, nameof(request.Reason));
        var context = await LoadContextAsync(cancellationToken);
        var rows = context.DocumentRows.ToList();
        var index = FindDocumentIndex(rows, id);
        if (index < 0)
        {
            return null;
        }

        var existing = rows[index];
        var document = ToDocumentResponse(existing, context.TypesByCode);
        EnsureCanViewDocument(document, context, user);

        var updated = WithValues(existing, new Dictionary<string, string?>
        {
            ["Estado"] = DocumentLifecycleStatus.Rechazado.ToString(),
            ["RechazadoPor"] = user.UserId,
            ["RechazadoEnUtc"] = DateTimeOffset.UtcNow.UtcDateTime.ToString("O"),
            ["MotivoRechazo"] = request.Reason,
            ["MotivoCambio"] = request.Reason
        });

        rows[index] = updated;
        await _dataProvider.SaveRowsAsync(DocumentsSchema, rows, cancellationToken);
        await RecordAuditAsync(user, "document.rejected", id, Serialize(existing), Serialize(updated), request.Reason, cancellationToken);

        return ToDocumentResponse(updated, context.TypesByCode);
    }

    public async Task<DocumentResponse?> ReplaceAsync(
        string id,
        ReplaceDocumentRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanManage(user);
        DomainGuard.AgainstEmpty(request.Reason, nameof(request.Reason));
        var context = await LoadContextAsync(cancellationToken);
        var rows = context.DocumentRows.ToList();
        var index = FindDocumentIndex(rows, id);
        if (index < 0)
        {
            return null;
        }

        var existing = rows[index];
        var document = ToDocumentResponse(existing, context.TypesByCode);
        EnsureCanViewDocument(document, context, user);

        var replacementId = Guid.NewGuid().ToString("D");
        var old = WithValues(existing, new Dictionary<string, string?>
        {
            ["Estado"] = DocumentLifecycleStatus.Reemplazado.ToString(),
            ["ReemplazadoPorDocumentoId"] = replacementId,
            ["EsHistorico"] = "true",
            ["MotivoCambio"] = request.Reason
        });

        var replacement = DocumentRow(
            replacementId,
            document.EntidadTipo,
            document.EntidadCodigo,
            document.TipoDocumento,
            HasFile(request.ArchivoKey, request.SharePointUrl) ? DocumentLifecycleStatus.PendienteValidacion : DocumentLifecycleStatus.PendienteCarga,
            request.FechaEmision,
            request.FechaVencimiento,
            request.ArchivoKey,
            request.SharePointUrl,
            document.Critico,
            document.Obligatorio,
            document.BloqueaDisponibilidad,
            null,
            null,
            null,
            null,
            null,
            document.DocumentoId,
            null,
            false,
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            user.UserId,
            request.Reason,
            false);

        rows[index] = old;
        rows.Add(replacement);
        await _dataProvider.SaveRowsAsync(DocumentsSchema, rows, cancellationToken);
        await RecordAuditAsync(user, "document.replaced", id, Serialize(existing), Serialize(replacement), request.Reason, cancellationToken);

        return ToDocumentResponse(replacement, context.TypesByCode);
    }

    public async Task<DocumentResponse?> AnnulAsync(
        string id,
        AnnulDocumentRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanManage(user);
        DomainGuard.AgainstEmpty(request.Reason, nameof(request.Reason));
        var context = await LoadContextAsync(cancellationToken);
        var rows = context.DocumentRows.ToList();
        var index = FindDocumentIndex(rows, id);
        if (index < 0)
        {
            return null;
        }

        var existing = rows[index];
        var document = ToDocumentResponse(existing, context.TypesByCode);
        EnsureCanViewDocument(document, context, user);

        var updated = WithValues(existing, new Dictionary<string, string?>
        {
            ["Estado"] = DocumentLifecycleStatus.Anulado.ToString(),
            ["AnuladoPor"] = user.UserId,
            ["AnuladoEnUtc"] = DateTimeOffset.UtcNow.UtcDateTime.ToString("O"),
            ["MotivoAnulacion"] = request.Reason,
            ["EsHistorico"] = "true",
            ["MotivoCambio"] = request.Reason
        });

        rows[index] = updated;
        await _dataProvider.SaveRowsAsync(DocumentsSchema, rows, cancellationToken);
        await RecordAuditAsync(user, "document.annulled", id, Serialize(existing), Serialize(updated), request.Reason, cancellationToken);

        return ToDocumentResponse(updated, context.TypesByCode);
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
        var context = await LoadContextAsync(cancellationToken);
        var documents = context.DocumentRows
            .Select(row => ToDocumentResponse(row, context.TypesByCode))
            .Where(document => !document.EsHistorico && document.Estado is not (DocumentLifecycleStatus.Reemplazado or DocumentLifecycleStatus.Anulado))
            .ToArray();
        var rows = new List<DocumentMatrixRow>();
        var activeTypes = context.TypesByCode.Values.Where(type => type.Activo).ToArray();

        foreach (var asset in context.AssetRows)
        {
            var assetCode = asset.GetValue("Codigo")?.Trim();
            var assetFaena = asset.GetValue("FaenaCodigo")?.Trim();
            if (string.IsNullOrWhiteSpace(assetCode) ||
                !CanViewFaena(assetFaena, user) ||
                (!string.IsNullOrWhiteSpace(faenaCodigo) && !string.Equals(assetFaena, faenaCodigo, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            foreach (var type in activeTypes.Where(type => AppliesTo(type, DocumentEntityType.Activo)))
            {
                var document = LatestFor(documents, DocumentEntityType.Activo, assetCode, type.Codigo);
                rows.Add(new DocumentMatrixRow(
                    DocumentEntityType.Activo,
                    assetCode,
                    asset.GetValue("Nombre")?.Trim() ?? assetCode,
                    type.Codigo,
                    type.Obligatorio,
                    type.BloqueaDisponibilidad,
                    document?.Estado ?? DocumentLifecycleStatus.PendienteCarga,
                    document?.DocumentoId,
                    document?.FechaVencimiento,
                    document?.BloqueaDisponibilidadActual ?? false));
            }
        }

        foreach (var faena in context.FaenaRows)
        {
            var code = faena.GetValue("Codigo")?.Trim();
            if (string.IsNullOrWhiteSpace(code) ||
                !CanViewFaena(code, user) ||
                (!string.IsNullOrWhiteSpace(faenaCodigo) && !string.Equals(code, faenaCodigo, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            foreach (var type in activeTypes.Where(type => AppliesTo(type, DocumentEntityType.Faena)))
            {
                var document = LatestFor(documents, DocumentEntityType.Faena, code, type.Codigo);
                rows.Add(new DocumentMatrixRow(
                    DocumentEntityType.Faena,
                    code,
                    faena.GetValue("Nombre")?.Trim() ?? code,
                    type.Codigo,
                    type.Obligatorio,
                    type.BloqueaDisponibilidad,
                    document?.Estado ?? DocumentLifecycleStatus.PendienteCarga,
                    document?.DocumentoId,
                    document?.FechaVencimiento,
                    document?.BloqueaDisponibilidadActual ?? false));
            }
        }

        return rows
            .OrderBy(row => row.EntidadTipo)
            .ThenBy(row => row.EntidadCodigo, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.TipoDocumento, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<DocumentDashboardSummary> GetSummaryAsync(
        string? faenaCodigo,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        var documents = await ListAsync(new DocumentQuery(FaenaCodigo: faenaCodigo), user, cancellationToken);
        return new DocumentDashboardSummary(
            documents.Count,
            documents.Count(document => document.Estado == DocumentLifecycleStatus.Vigente),
            documents.Count(document => document.Estado == DocumentLifecycleStatus.PorVencer),
            documents.Count(document => document.Estado == DocumentLifecycleStatus.Vencido),
            documents.Count(document => document.Estado == DocumentLifecycleStatus.PendienteCarga),
            documents.Count(document => document.Estado == DocumentLifecycleStatus.PendienteValidacion),
            documents.Count(document => document.Estado == DocumentLifecycleStatus.Rechazado),
            documents.Count(document => document.Estado == DocumentLifecycleStatus.Reemplazado),
            documents.Count(document => document.Estado == DocumentLifecycleStatus.Anulado),
            documents.Count(document => document.BloqueaDisponibilidadActual));
    }

    private async Task<DocumentContext> LoadContextAsync(CancellationToken cancellationToken)
    {
        var typeRows = await _dataProvider.ReadRowsAsync(DocumentTypesSchema, cancellationToken);
        var types = typeRows
            .Select(ToTypeResponse)
            .Where(type => !string.IsNullOrWhiteSpace(type.Codigo))
            .ToDictionary(type => type.Codigo, type => type, StringComparer.OrdinalIgnoreCase);

        return new DocumentContext(
            types,
            await _dataProvider.ReadRowsAsync(DocumentsSchema, cancellationToken),
            await _dataProvider.ReadRowsAsync(AssetsSchema, cancellationToken),
            await _dataProvider.ReadRowsAsync(FaenasSchema, cancellationToken),
            await _dataProvider.ReadRowsAsync(WorkOrdersSchema, cancellationToken));
    }

    private async Task ValidateEntityAsync(
        DocumentEntityType entityType,
        string entityCode,
        UserAccessContext user,
        DocumentContext context,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        var faena = ResolveEntityFaena(entityType, entityCode, context);
        if (faena is null)
        {
            throw new DomainException($"La entidad documental {entityType}/{entityCode} no existe.");
        }

        if (!CanViewFaena(faena, user))
        {
            throw new UnauthorizedAccessException("El usuario no tiene acceso a la faena de la entidad documental.");
        }
    }

    private static DocumentTypeResponse ToTypeResponse(DataRow row)
    {
        var alertDays = ParseInt(row.GetValue("PlazoAlertaDias"), 30);
        return new DocumentTypeResponse(
            row.GetValue("Codigo")?.Trim() ?? string.Empty,
            row.GetValue("Nombre")?.Trim() ?? row.GetValue("Codigo")?.Trim() ?? string.Empty,
            ParseEntityTypeNullable(row.GetValue("AplicaA")),
            ParseBool(row.GetValue("Obligatorio")),
            ParseBool(row.GetValue("Critico")),
            ParseBool(row.GetValue("BloqueaDisponibilidad")),
            Math.Max(0, alertDays),
            SplitList(row.GetValue("RolesResponsables")),
            ParseBool(row.GetValue("RequierePdfAlerta")),
            EmptyToNull(row.GetValue("PlantillaHtmlCodigo")),
            ParseBool(row.GetValue("Activo"), defaultValue: true));
    }

    private static DataRow TypeRow(
        string code,
        string name,
        DocumentEntityType? appliesTo,
        bool mandatory,
        bool critical,
        bool blocksAvailability,
        int alertDays,
        IReadOnlyCollection<string> responsibleRoles,
        bool requiresAlertPdf,
        string? htmlTemplateCode,
        bool active)
    {
        return new DataRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Codigo"] = code.Trim(),
            ["Nombre"] = name.Trim(),
            ["AplicaA"] = appliesTo?.ToString(),
            ["Obligatorio"] = mandatory ? "true" : "false",
            ["Critico"] = critical ? "true" : "false",
            ["BloqueaDisponibilidad"] = blocksAvailability ? "true" : "false",
            ["PlazoAlertaDias"] = Math.Max(0, alertDays).ToString(CultureInfo.InvariantCulture),
            ["RolesResponsables"] = JoinList(responsibleRoles),
            ["RequierePdfAlerta"] = requiresAlertPdf ? "true" : "false",
            ["PlantillaHtmlCodigo"] = EmptyToNull(htmlTemplateCode),
            ["Activo"] = active ? "true" : "false"
        });
    }

    private static DocumentResponse ToDocumentResponse(
        DataRow row,
        IReadOnlyDictionary<string, DocumentTypeResponse> typesByCode)
    {
        var typeCode = row.GetValue("TipoDocumento")?.Trim() ?? string.Empty;
        typesByCode.TryGetValue(typeCode, out var type);
        var rawStatus = ParseLifecycle(row.GetValue("Estado"));
        var expiresOn = ParseDate(row.GetValue("FechaVencimiento"));
        var issueDate = ParseDate(row.GetValue("FechaEmision"));
        var alertDays = type?.PlazoAlertaDias ?? 30;
        var critical = ParseBool(row.GetValue("Critico"), type?.Critico ?? false);
        var mandatory = ParseBool(row.GetValue("Obligatorio"), type?.Obligatorio ?? false);
        var blocksAvailability = ParseBool(row.GetValue("BloqueaDisponibilidad"), type?.BloqueaDisponibilidad ?? false);
        var historical = ParseBool(row.GetValue("EsHistorico"));
        var effectiveStatus = ResolveStatus(rawStatus, expiresOn, alertDays, row);
        var blocksNow = !historical &&
                        effectiveStatus == DocumentLifecycleStatus.Vencido &&
                        (critical || blocksAvailability) &&
                        rawStatus is not (DocumentLifecycleStatus.Anulado or DocumentLifecycleStatus.Reemplazado);

        return new DocumentResponse(
            EnsureId(row),
            ParseEntityType(row.GetValue("EntidadTipo")),
            row.GetValue("EntidadCodigo")?.Trim() ?? string.Empty,
            typeCode,
            effectiveStatus,
            issueDate,
            expiresOn,
            EmptyToNull(row.GetValue("ArchivoKey")),
            EmptyToNull(row.GetValue("SharePointUrl")),
            critical,
            mandatory,
            blocksAvailability,
            historical,
            ParseBool(row.GetValue("FechaVencimientoValidada")),
            EmptyToNull(row.GetValue("ValidadoPor")),
            ParseDateTime(row.GetValue("ValidadoEnUtc")),
            EmptyToNull(row.GetValue("RechazadoPor")),
            ParseDateTime(row.GetValue("RechazadoEnUtc")),
            EmptyToNull(row.GetValue("MotivoRechazo")),
            EmptyToNull(row.GetValue("ReemplazaDocumentoId")),
            EmptyToNull(row.GetValue("ReemplazadoPorDocumentoId")),
            EmptyToNull(row.GetValue("AnuladoPor")),
            ParseDateTime(row.GetValue("AnuladoEnUtc")),
            EmptyToNull(row.GetValue("MotivoAnulacion")),
            ParseDateTime(row.GetValue("FechaCargaUtc")) ?? DateTimeOffset.UtcNow,
            row.GetValue("CargadoPor")?.Trim() ?? "system",
            CalculateDaysToExpire(expiresOn),
            blocksNow);
    }

    private static DataRow DocumentRow(
        string id,
        DocumentEntityType entityType,
        string entityCode,
        string typeCode,
        DocumentLifecycleStatus status,
        DateOnly? issueDate,
        DateOnly? expiresOn,
        string? fileKey,
        string? sharePointUrl,
        bool critical,
        bool mandatory,
        bool blocksAvailability,
        string? validatedBy,
        DateTimeOffset? validatedAt,
        string? rejectedBy,
        DateTimeOffset? rejectedAt,
        string? rejectReason,
        string? replacesId,
        string? replacedById,
        bool historical,
        string? annulledBy,
        DateTimeOffset? annulledAt,
        string? annulReason,
        DateTimeOffset loadedAt,
        string loadedBy,
        string? changeReason,
        bool expiryValidated)
    {
        return new DataRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["DocumentoId"] = id,
            ["EntidadTipo"] = entityType.ToString(),
            ["EntidadCodigo"] = entityCode.Trim(),
            ["TipoDocumento"] = typeCode.Trim(),
            ["Estado"] = status.ToString(),
            ["FechaEmision"] = FormatDate(issueDate),
            ["FechaVencimiento"] = FormatDate(expiresOn),
            ["ArchivoKey"] = EmptyToNull(fileKey),
            ["SharePointUrl"] = EmptyToNull(sharePointUrl),
            ["Critico"] = critical ? "true" : "false",
            ["Obligatorio"] = mandatory ? "true" : "false",
            ["BloqueaDisponibilidad"] = blocksAvailability ? "true" : "false",
            ["ValidadoPor"] = EmptyToNull(validatedBy),
            ["ValidadoEnUtc"] = FormatDateTime(validatedAt),
            ["RechazadoPor"] = EmptyToNull(rejectedBy),
            ["RechazadoEnUtc"] = FormatDateTime(rejectedAt),
            ["MotivoRechazo"] = EmptyToNull(rejectReason),
            ["ReemplazaDocumentoId"] = EmptyToNull(replacesId),
            ["ReemplazadoPorDocumentoId"] = EmptyToNull(replacedById),
            ["EsHistorico"] = historical ? "true" : "false",
            ["AnuladoPor"] = EmptyToNull(annulledBy),
            ["AnuladoEnUtc"] = FormatDateTime(annulledAt),
            ["MotivoAnulacion"] = EmptyToNull(annulReason),
            ["FechaCargaUtc"] = loadedAt.UtcDateTime.ToString("O"),
            ["CargadoPor"] = loadedBy,
            ["MotivoCambio"] = EmptyToNull(changeReason),
            ["FechaVencimientoValidada"] = expiryValidated ? "true" : "false"
        });
    }

    private static DocumentLifecycleStatus ResolveStatus(
        DocumentLifecycleStatus rawStatus,
        DateOnly? expiresOn,
        int alertDays,
        DataRow row)
    {
        if (rawStatus is DocumentLifecycleStatus.Rechazado or DocumentLifecycleStatus.Reemplazado or DocumentLifecycleStatus.Anulado)
        {
            return rawStatus;
        }

        if (!HasFile(row.GetValue("ArchivoKey"), row.GetValue("SharePointUrl")))
        {
            return DocumentLifecycleStatus.PendienteCarga;
        }

        if (rawStatus == DocumentLifecycleStatus.PendienteValidacion)
        {
            return DocumentLifecycleStatus.PendienteValidacion;
        }

        if (!expiresOn.HasValue)
        {
            return DocumentLifecycleStatus.Vigente;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (expiresOn.Value < today)
        {
            return DocumentLifecycleStatus.Vencido;
        }

        if (expiresOn.Value <= today.AddDays(Math.Max(0, alertDays)))
        {
            return DocumentLifecycleStatus.PorVencer;
        }

        return DocumentLifecycleStatus.Vigente;
    }

    private static bool Matches(
        DocumentResponse document,
        DocumentQuery query,
        DocumentContext context,
        UserAccessContext user)
    {
        if (!query.IncludeHistorical && document.EsHistorico)
        {
            return false;
        }

        if (document.Estado is DocumentLifecycleStatus.Reemplazado or DocumentLifecycleStatus.Anulado && !query.IncludeHistorical)
        {
            return false;
        }

        if (!CanViewDocument(document, context, user))
        {
            return false;
        }

        if (query.EntidadTipo.HasValue && document.EntidadTipo != query.EntidadTipo.Value)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.EntidadCodigo) &&
            !string.Equals(document.EntidadCodigo, query.EntidadCodigo, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.TipoDocumento) &&
            !string.Equals(document.TipoDocumento, query.TipoDocumento, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.FaenaCodigo) &&
            !string.Equals(ResolveEntityFaena(document.EntidadTipo, document.EntidadCodigo, context), query.FaenaCodigo, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !query.Estado.HasValue || document.Estado == query.Estado.Value;
    }

    private static string? ResolveEntityFaena(
        DocumentEntityType entityType,
        string entityCode,
        DocumentContext context)
    {
        if (entityType == DocumentEntityType.Faena)
        {
            return context.FaenaRows.Any(row => SameCode(row.GetValue("Codigo"), entityCode)) ? entityCode : null;
        }

        if (entityType == DocumentEntityType.Activo)
        {
            return context.AssetRows.FirstOrDefault(row => SameCode(row.GetValue("Codigo"), entityCode))?.GetValue("FaenaCodigo");
        }

        var workOrder = context.WorkOrderRows.FirstOrDefault(row => SameCode(row.GetValue("NumeroOT"), entityCode));
        var assetCode = workOrder?.GetValue("ActivoCodigo");
        return string.IsNullOrWhiteSpace(assetCode)
            ? null
            : context.AssetRows.FirstOrDefault(row => SameCode(row.GetValue("Codigo"), assetCode))?.GetValue("FaenaCodigo");
    }

    private static string ResolveEntityName(
        DocumentEntityType entityType,
        string entityCode,
        DocumentContext context)
    {
        return entityType switch
        {
            DocumentEntityType.Activo => context.AssetRows.FirstOrDefault(row => SameCode(row.GetValue("Codigo"), entityCode))?.GetValue("Nombre") ?? entityCode,
            DocumentEntityType.Faena => context.FaenaRows.FirstOrDefault(row => SameCode(row.GetValue("Codigo"), entityCode))?.GetValue("Nombre") ?? entityCode,
            _ => entityCode
        };
    }

    private static bool CanViewDocument(DocumentResponse document, DocumentContext context, UserAccessContext user)
    {
        var faena = ResolveEntityFaena(document.EntidadTipo, document.EntidadCodigo, context);
        return CanViewFaena(faena, user);
    }

    private void EnsureCanViewDocument(DocumentResponse document, DocumentContext context, UserAccessContext user)
    {
        if (!CanViewDocument(document, context, user))
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

    private void EnsureExpiryCanChange(
        DocumentResponse document,
        DateOnly? nextExpiry,
        UserAccessContext user)
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

    private static DocumentTypeResponse? ResolveType(
        IReadOnlyDictionary<string, DocumentTypeResponse> types,
        string typeCode)
    {
        return types.TryGetValue(typeCode, out var type) ? type : null;
    }

    private static DocumentResponse? LatestFor(
        IReadOnlyCollection<DocumentResponse> documents,
        DocumentEntityType entityType,
        string entityCode,
        string typeCode)
    {
        return documents
            .Where(document =>
                document.EntidadTipo == entityType &&
                SameCode(document.EntidadCodigo, entityCode) &&
                SameCode(document.TipoDocumento, typeCode) &&
                !document.EsHistorico)
            .OrderByDescending(document => document.FechaCargaUtc)
            .FirstOrDefault();
    }

    private static bool AppliesTo(DocumentTypeResponse type, DocumentEntityType entityType)
    {
        return !type.AplicaA.HasValue || type.AplicaA.Value == entityType;
    }

    private void EnsureCanViewFaenaFilter(string? faenaCode, UserAccessContext user)
    {
        if (!string.IsNullOrWhiteSpace(faenaCode) && !CanViewFaena(faenaCode, user))
        {
            throw new UnauthorizedAccessException("El usuario no tiene acceso a la faena solicitada.");
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

    private static DataRow WithValues(DataRow row, IReadOnlyDictionary<string, string?> nextValues)
    {
        var values = new Dictionary<string, string?>(row.Values, StringComparer.OrdinalIgnoreCase);
        foreach (var item in nextValues)
        {
            values[item.Key] = item.Value;
        }

        return new DataRow(values);
    }

    private static DataRow? FindDocumentById(IReadOnlyCollection<DataRow> rows, string id)
    {
        return rows.FirstOrDefault(row => SameCode(EnsureId(row), id));
    }

    private static int FindDocumentIndex(IReadOnlyList<DataRow> rows, string id)
    {
        for (var index = 0; index < rows.Count; index++)
        {
            if (SameCode(EnsureId(rows[index]), id))
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindIndex(IReadOnlyList<DataRow> rows, string columnName, string value)
    {
        for (var index = 0; index < rows.Count; index++)
        {
            if (SameCode(rows[index].GetValue(columnName), value))
            {
                return index;
            }
        }

        return -1;
    }

    private static string EnsureId(DataRow row)
    {
        var id = row.GetValue("DocumentoId")?.Trim();
        if (!string.IsNullOrWhiteSpace(id))
        {
            return id;
        }

        return string.Join(
            "-",
            row.GetValue("EntidadTipo"),
            row.GetValue("EntidadCodigo"),
            row.GetValue("TipoDocumento")).Trim('-');
    }

    private static string Serialize(DataRow row)
    {
        return JsonSerializer.Serialize(row.Values);
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

    private static string? FormatDate(DateOnly? value)
    {
        return value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static string? FormatDateTime(DateTimeOffset? value)
    {
        return value?.UtcDateTime.ToString("O");
    }

    private static DateOnly? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result)
            ? result
            : null;
    }

    private static DateTimeOffset? ParseDateTime(string? value)
    {
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var result)
            ? result
            : null;
    }

    private static bool ParseBool(string? value, bool defaultValue = false)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Trim().Equals("si", StringComparison.OrdinalIgnoreCase) ||
               value.Trim().Equals("1", StringComparison.OrdinalIgnoreCase);
    }

    private static int ParseInt(string? value, int defaultValue)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : defaultValue;
    }

    private static int? CalculateDaysToExpire(DateOnly? expiresOn)
    {
        return expiresOn.HasValue
            ? expiresOn.Value.DayNumber - DateOnly.FromDateTime(DateTime.UtcNow).DayNumber
            : null;
    }

    private static DocumentEntityType ParseEntityType(string? value)
    {
        return Enum.TryParse<DocumentEntityType>(value, ignoreCase: true, out var result)
            ? result
            : DocumentEntityType.Activo;
    }

    private static DocumentEntityType? ParseEntityTypeNullable(string? value)
    {
        return Enum.TryParse<DocumentEntityType>(value, ignoreCase: true, out var result)
            ? result
            : null;
    }

    private static DocumentLifecycleStatus ParseLifecycle(string? value)
    {
        if (Enum.TryParse<DocumentLifecycleStatus>(value, ignoreCase: true, out var result))
        {
            return result;
        }

        return value?.Trim().ToLowerInvariant() switch
        {
            "validated" or "validado" or "vigente" => DocumentLifecycleStatus.Vigente,
            "expired" or "vencido" => DocumentLifecycleStatus.Vencido,
            "pendingvalidation" or "pending_validation" or "pendiente validacion" or "pendientevalidacion" => DocumentLifecycleStatus.PendienteValidacion,
            "rejected" or "rechazado" => DocumentLifecycleStatus.Rechazado,
            "replaced" or "reemplazado" => DocumentLifecycleStatus.Reemplazado,
            "annulled" or "anulado" => DocumentLifecycleStatus.Anulado,
            _ => DocumentLifecycleStatus.PendienteCarga
        };
    }

    private sealed record DocumentContext(
        IReadOnlyDictionary<string, DocumentTypeResponse> TypesByCode,
        IReadOnlyCollection<DataRow> DocumentRows,
        IReadOnlyCollection<DataRow> AssetRows,
        IReadOnlyCollection<DataRow> FaenaRows,
        IReadOnlyCollection<DataRow> WorkOrderRows);
}
