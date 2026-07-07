using System.Globalization;
using System.Text.Json;
using MaintenanceCMMS.Application.Abstractions.Data;
using MaintenanceCMMS.Application.Assets;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Domain.Enums;

namespace MaintenanceCMMS.Infrastructure.Assets;

public sealed class AssetService : IAssetService
{
    private const string AssetsSchema = "activos";
    private const string FaenasSchema = "faenas";
    private const string LocationsSchema = "ubicaciones_tecnicas";
    private const string DocumentsSchema = "documentos";
    private const string WorkOrdersSchema = "ordenes_trabajo";
    private const string SparePartsSchema = "repuestos";
    private const string StateEventsSchema = "asset_state_events";

    private static readonly string[] RequiredTechnicalFields =
    [
        "Nombre",
        "FaenaCodigo",
        "TipoActivo",
        "Familia",
        "Marca",
        "Modelo",
        "NumeroSerie",
        "Propiedad",
        "Criticidad",
        "EstadoDocumental",
        "EstadoOperacional"
    ];

    private readonly IDataProvider _dataProvider;
    private readonly IAuditService _auditService;
    private readonly IAuthorizationPolicyService _authorizationPolicyService;

    public AssetService(
        IDataProvider dataProvider,
        IAuditService auditService,
        IAuthorizationPolicyService authorizationPolicyService)
    {
        _dataProvider = dataProvider;
        _auditService = auditService;
        _authorizationPolicyService = authorizationPolicyService;
    }

    public async Task<IReadOnlyCollection<AssetSummary>> ListAsync(
        AssetListQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(query.FaenaCodigo) &&
            !_authorizationPolicyService.CanViewFaena(user, query.FaenaCodigo))
        {
            throw new UnauthorizedAccessException("El usuario no tiene acceso a la faena solicitada.");
        }

        var rows = await ReadVisibleAssetRowsAsync(user, cancellationToken);
        var documents = await ReadDocumentRowsAsync(cancellationToken);

        return rows
            .Select(row => ToSummary(row, documents))
            .Where(asset => Matches(query, asset))
            .OrderBy(asset => asset.Codigo, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<AssetDetail?> GetByIdAsync(
        string codigo,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        var rows = await _dataProvider.ReadRowsAsync(AssetsSchema, cancellationToken);
        var row = FindAssetRow(rows, codigo);
        if (row is null)
        {
            return null;
        }

        EnsureCanViewAsset(user, row);

        var documents = await ReadDocumentRowsAsync(cancellationToken);
        var workOrders = await ReadWorkOrdersAsync(codigo, cancellationToken);
        var spareParts = await ReadCompatibleSparePartsAsync(row, cancellationToken);

        return ToDetail(row, documents, workOrders, spareParts);
    }

    public async Task<AssetDetail> CreateAsync(
        CreateAssetRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanMaintainAssets(user);
        ValidateRequired(request.Codigo, nameof(request.Codigo));
        ValidateRequired(request.Nombre, nameof(request.Nombre));
        ValidateRequired(request.FaenaCodigo, nameof(request.FaenaCodigo));
        ValidateRequired(request.TipoActivo, nameof(request.TipoActivo));
        EnsureCanUseFaena(user, request.FaenaCodigo);

        await ValidateReferencesAsync(request.FaenaCodigo, request.UbicacionTecnicaCodigo, cancellationToken);

        var rows = (await _dataProvider.ReadRowsAsync(AssetsSchema, cancellationToken)).ToList();
        var normalizedCode = NormalizeCode(request.Codigo);
        if (rows.Any(row => string.Equals(NormalizeCode(row.GetValue("Codigo")), normalizedCode, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainException($"Ya existe un activo con codigo '{request.Codigo}'.");
        }

        var now = DateTimeOffset.UtcNow;
        var rowToCreate = BuildAssetRow(
            request.Codigo,
            request.Nombre,
            request.FaenaCodigo,
            request.TipoActivo,
            request.Estado,
            request.UbicacionTecnicaCodigo,
            request.Familia,
            request.Marca,
            request.Modelo,
            request.Patente,
            request.NumeroSerie,
            request.Propiedad,
            request.Criticidad,
            request.EstadoDocumental,
            request.EstadoOperacional,
            request.TechnicalFields,
            request.FichaValidada,
            now,
            now);

        rows.Add(rowToCreate);
        await _dataProvider.SaveRowsAsync(AssetsSchema, rows, cancellationToken);

        await RecordAssetAuditAsync(
            user,
            "Created",
            request.Codigo,
            request.FaenaCodigo,
            null,
            Serialize(rowToCreate),
            "Activo creado",
            cancellationToken);

        var created = await GetByIdAsync(request.Codigo, user, cancellationToken);
        return created ?? throw new InvalidOperationException("No fue posible leer el activo creado.");
    }

    public async Task<AssetDetail?> UpdateAsync(
        string codigo,
        UpdateAssetRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanMaintainAssets(user);
        ValidateRequired(request.Nombre, nameof(request.Nombre));
        ValidateRequired(request.FaenaCodigo, nameof(request.FaenaCodigo));
        ValidateRequired(request.TipoActivo, nameof(request.TipoActivo));

        var rows = (await _dataProvider.ReadRowsAsync(AssetsSchema, cancellationToken)).ToList();
        var index = FindAssetIndex(rows, codigo);
        if (index < 0)
        {
            return null;
        }

        var existing = rows[index];
        EnsureCanViewAsset(user, existing);

        var previousFaena = existing.GetValue("FaenaCodigo")?.Trim() ?? string.Empty;
        if (!string.Equals(previousFaena, request.FaenaCodigo.Trim(), StringComparison.OrdinalIgnoreCase) &&
            !_authorizationPolicyService.CanChangeAssetFaena(user))
        {
            throw new UnauthorizedAccessException("Cambiar la faena del activo requiere permiso especial.");
        }

        EnsureCanUseFaena(user, request.FaenaCodigo);
        await ValidateReferencesAsync(request.FaenaCodigo, request.UbicacionTecnicaCodigo, cancellationToken);

        var assetCode = existing.GetValue("Codigo")?.Trim() ?? codigo.Trim();
        var createdAt = ParseDateTime(existing.GetValue("FechaAlta")) ?? DateTimeOffset.UtcNow;
        var updated = BuildAssetRow(
            assetCode,
            request.Nombre,
            request.FaenaCodigo,
            request.TipoActivo,
            request.Estado,
            request.UbicacionTecnicaCodigo,
            request.Familia,
            request.Marca,
            request.Modelo,
            request.Patente,
            request.NumeroSerie,
            request.Propiedad,
            request.Criticidad,
            request.EstadoDocumental,
            request.EstadoOperacional,
            request.TechnicalFields,
            request.FichaValidada ?? ParseBool(existing.GetValue("FichaValidada")),
            createdAt,
            DateTimeOffset.UtcNow);

        rows[index] = updated;
        await _dataProvider.SaveRowsAsync(AssetsSchema, rows, cancellationToken);

        await RecordAssetAuditAsync(
            user,
            "Updated",
            assetCode,
            request.FaenaCodigo,
            Serialize(existing),
            Serialize(updated),
            request.Reason ?? "Activo actualizado",
            cancellationToken);

        return await GetByIdAsync(assetCode, user, cancellationToken);
    }

    public async Task<AssetStateEventResponse?> AddStateEventAsync(
        string codigo,
        CreateAssetStateEventRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanChangeState(user);
        ValidateRequired(request.Reason, nameof(request.Reason));

        var rows = (await _dataProvider.ReadRowsAsync(AssetsSchema, cancellationToken)).ToList();
        var index = FindAssetIndex(rows, codigo);
        if (index < 0)
        {
            return null;
        }

        var existing = rows[index];
        EnsureCanViewAsset(user, existing);

        var previousStatus = ParseStatus(existing.GetValue("Estado"));
        var next = WithValues(existing, new Dictionary<string, string?>
        {
            ["Estado"] = request.Status.ToString(),
            ["EstadoOperacional"] = DefaultOperationalState(request.Status),
            ["FechaActualizacion"] = DateTimeOffset.UtcNow.UtcDateTime.ToString("O")
        });

        rows[index] = next;
        await _dataProvider.SaveRowsAsync(AssetsSchema, rows, cancellationToken);

        var eventResponse = new AssetStateEventResponse(
            Guid.NewGuid().ToString("D"),
            existing.GetValue("Codigo")?.Trim() ?? codigo.Trim(),
            previousStatus,
            request.Status,
            request.OccurredAtUtc ?? DateTimeOffset.UtcNow,
            request.Reason.Trim(),
            user.UserId);

        var eventRows = (await _dataProvider.ReadRowsAsync(StateEventsSchema, cancellationToken)).ToList();
        eventRows.Add(ToStateEventRow(eventResponse));
        await _dataProvider.SaveRowsAsync(StateEventsSchema, eventRows, cancellationToken);

        await RecordAssetAuditAsync(
            user,
            "StateChanged",
            eventResponse.ActivoCodigo,
            existing.GetValue("FaenaCodigo"),
            previousStatus.ToString(),
            request.Status.ToString(),
            request.Reason,
            cancellationToken);

        return eventResponse;
    }

    public async Task<IReadOnlyCollection<AssetHistoryEntry>> GetHistoryAsync(
        string codigo,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        var asset = await GetByIdAsync(codigo, user, cancellationToken);
        if (asset is null)
        {
            return [];
        }

        var audit = await _auditService.QueryAsync(new AuditQuery(
            Module: AuditModules.Assets,
            EntityName: "Asset",
            Take: 500), cancellationToken);

        var stateEvents = (await _dataProvider.ReadRowsAsync(StateEventsSchema, cancellationToken))
            .Where(row => string.Equals(row.GetValue("ActivoCodigo"), asset.Codigo, StringComparison.OrdinalIgnoreCase))
            .Select(row => new AssetHistoryEntry(
                row.GetValue("EventoId")?.Trim() ?? Guid.NewGuid().ToString("D"),
                ParseDateTime(row.GetValue("FechaEvento")) ?? DateTimeOffset.MinValue,
                "StateChanged",
                "EventosEstado",
                row.GetValue("UsuarioId")?.Trim() ?? string.Empty,
                row.GetValue("EstadoAnterior"),
                row.GetValue("Estado"),
                row.GetValue("Motivo")));

        var auditEntries = audit.Items
            .Where(entry => string.Equals(entry.EntityId, asset.Codigo, StringComparison.OrdinalIgnoreCase))
            .Select(entry => new AssetHistoryEntry(
                entry.AuditId,
                entry.OccurredAtUtc,
                entry.Action,
                "Auditoria",
                entry.UserId,
                entry.PreviousValue,
                entry.NewValue,
                entry.Detail ?? entry.Reason));

        return auditEntries
            .Concat(stateEvents)
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<AssetDocumentResponse>> GetDocumentsAsync(
        string codigo,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        var asset = await GetByIdAsync(codigo, user, cancellationToken);
        if (asset is null)
        {
            return [];
        }

        var rows = await ReadDocumentRowsAsync(cancellationToken);
        return rows
            .Where(row => IsAssetDocument(row, asset.Codigo))
            .Select(ToDocumentResponse)
            .OrderBy(document => document.TipoDocumento, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<AssetCostSummary?> GetCostsAsync(
        string codigo,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        var asset = await GetByIdAsync(codigo, user, cancellationToken);
        if (asset is null)
        {
            return null;
        }

        if (!_authorizationPolicyService.CanViewCosts(user))
        {
            throw new UnauthorizedAccessException("El usuario no tiene permiso para ver costos.");
        }

        return new AssetCostSummary(asset.Codigo, 0, "CLP", []);
    }

    public async Task<AssetAvailabilityResponse?> GetAvailabilityAsync(
        string codigo,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        var asset = await GetByIdAsync(codigo, user, cancellationToken);
        if (asset is null)
        {
            return null;
        }

        var documents = await GetDocumentsAsync(codigo, user, cancellationToken);
        var blockers = new List<string>();
        if (!asset.DisponibleDocumentalmente)
        {
            blockers.Add("Documento critico vencido");
        }

        var operationallyAvailable = asset.Estado == AssetStatus.Active &&
                                      !asset.EstadoOperacional.Contains("no disponible", StringComparison.OrdinalIgnoreCase) &&
                                      !asset.EstadoOperacional.Contains("mantenimiento", StringComparison.OrdinalIgnoreCase) &&
                                      !asset.EstadoOperacional.Contains("retirado", StringComparison.OrdinalIgnoreCase);

        if (!operationallyAvailable)
        {
            blockers.Add($"Estado operacional: {asset.EstadoOperacional}");
        }

        var expiredDocuments = documents
            .Where(document => document.BloqueaDisponibilidad)
            .Select(document => $"Documento vencido: {document.TipoDocumento}")
            .ToArray();
        blockers.AddRange(expiredDocuments);

        var available = operationallyAvailable && asset.DisponibleDocumentalmente;

        return new AssetAvailabilityResponse(
            asset.Codigo,
            available,
            operationallyAvailable,
            asset.DisponibleDocumentalmente,
            asset.EstadoOperacional,
            asset.EstadoDocumental,
            blockers.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            available ? 100 : 0);
    }

    private async Task<IReadOnlyCollection<DataRow>> ReadVisibleAssetRowsAsync(
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        var rows = await _dataProvider.ReadRowsAsync(AssetsSchema, cancellationToken);
        if (_authorizationPolicyService.CanAdminister(user))
        {
            return rows;
        }

        return rows
            .Where(row => _authorizationPolicyService.CanViewFaena(user, row.GetValue("FaenaCodigo") ?? string.Empty))
            .ToArray();
    }

    private async Task<IReadOnlyCollection<DataRow>> ReadDocumentRowsAsync(CancellationToken cancellationToken)
    {
        return await _dataProvider.ReadRowsAsync(DocumentsSchema, cancellationToken);
    }

    private async Task<IReadOnlyCollection<AssetWorkOrderSummary>> ReadWorkOrdersAsync(
        string codigo,
        CancellationToken cancellationToken)
    {
        var rows = await _dataProvider.ReadRowsAsync(WorkOrdersSchema, cancellationToken);
        return rows
            .Where(row => string.Equals(row.GetValue("ActivoCodigo"), codigo, StringComparison.OrdinalIgnoreCase))
            .Select(row => new AssetWorkOrderSummary(
                row.GetValue("NumeroOT")?.Trim() ?? string.Empty,
                row.GetValue("Estado")?.Trim() ?? string.Empty,
                row.GetValue("TipoMantenimiento")?.Trim() ?? string.Empty,
                EmptyToNull(row.GetValue("Descripcion")),
                ParseDateOnly(row.GetValue("FechaProgramada"))))
            .Where(workOrder => !string.IsNullOrWhiteSpace(workOrder.NumeroOT))
            .OrderByDescending(workOrder => workOrder.FechaProgramada)
            .ToArray();
    }

    private async Task<IReadOnlyCollection<CompatibleSparePartSummary>> ReadCompatibleSparePartsAsync(
        DataRow asset,
        CancellationToken cancellationToken)
    {
        var family = asset.GetValue("Familia");
        if (string.IsNullOrWhiteSpace(family))
        {
            return [];
        }

        var spareParts = await _dataProvider.ReadRowsAsync(SparePartsSchema, cancellationToken);
        return spareParts
            .Where(row => string.Equals(row.GetValue("Familia"), family, StringComparison.OrdinalIgnoreCase))
            .Select(row => new CompatibleSparePartSummary(
                row.GetValue("Codigo")?.Trim() ?? string.Empty,
                row.GetValue("Descripcion")?.Trim() ?? string.Empty,
                EmptyToNull(row.GetValue("Familia")),
                EmptyToNull(row.GetValue("UnidadMedida"))))
            .Where(part => !string.IsNullOrWhiteSpace(part.Codigo))
            .OrderBy(part => part.Codigo, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool Matches(AssetListQuery query, AssetSummary asset)
    {
        return (string.IsNullOrWhiteSpace(query.FaenaCodigo) ||
                string.Equals(asset.FaenaCodigo, query.FaenaCodigo, StringComparison.OrdinalIgnoreCase)) &&
               (!query.Estado.HasValue || asset.Estado == query.Estado.Value) &&
               (string.IsNullOrWhiteSpace(query.Familia) ||
                string.Equals(asset.Familia, query.Familia, StringComparison.OrdinalIgnoreCase)) &&
               (string.IsNullOrWhiteSpace(query.Criticidad) ||
                string.Equals(asset.Criticidad, query.Criticidad, StringComparison.OrdinalIgnoreCase));
    }

    private static AssetSummary ToSummary(DataRow row, IReadOnlyCollection<DataRow> documents)
    {
        var codigo = row.GetValue("Codigo")?.Trim() ?? string.Empty;
        var effectiveDocumentState = EffectiveDocumentState(row, documents);
        var completeness = CalculateCompleteness(row, effectiveDocumentState);

        return new AssetSummary(
            codigo,
            row.GetValue("Nombre")?.Trim() ?? string.Empty,
            row.GetValue("FaenaCodigo")?.Trim() ?? string.Empty,
            row.GetValue("TipoActivo")?.Trim() ?? string.Empty,
            ParseStatus(row.GetValue("Estado")),
            EmptyToNull(row.GetValue("UbicacionTecnicaCodigo")),
            EmptyToNull(row.GetValue("Familia")),
            EmptyToNull(row.GetValue("Marca")),
            EmptyToNull(row.GetValue("Modelo")),
            EmptyToNull(row.GetValue("Patente")),
            EmptyToNull(row.GetValue("NumeroSerie")),
            EmptyToNull(row.GetValue("Propiedad")),
            EmptyToNull(row.GetValue("Criticidad")),
            effectiveDocumentState,
            EffectiveOperationalState(row),
            completeness,
            !HasCriticalExpiredDocument(codigo, documents),
            ParseBool(row.GetValue("FichaValidada")));
    }

    private static AssetDetail ToDetail(
        DataRow row,
        IReadOnlyCollection<DataRow> documents,
        IReadOnlyCollection<AssetWorkOrderSummary> workOrders,
        IReadOnlyCollection<CompatibleSparePartSummary> spareParts)
    {
        var summary = ToSummary(row, documents);
        return new AssetDetail(
            summary.Codigo,
            summary.Nombre,
            summary.FaenaCodigo,
            summary.TipoActivo,
            summary.Estado,
            summary.UbicacionTecnicaCodigo,
            summary.Familia,
            summary.Marca,
            summary.Modelo,
            summary.Patente,
            summary.NumeroSerie,
            summary.Propiedad,
            summary.Criticidad,
            summary.EstadoDocumental,
            summary.EstadoOperacional,
            summary.CompletitudFicha,
            summary.DisponibleDocumentalmente,
            summary.FichaValidada,
            ParseDateTime(row.GetValue("FechaAlta")),
            ParseDateTime(row.GetValue("FechaActualizacion")),
            BuildTechnicalFields(row),
            workOrders,
            spareParts);
    }

    private static DataRow BuildAssetRow(
        string codigo,
        string nombre,
        string faenaCodigo,
        string tipoActivo,
        AssetStatus estado,
        string? ubicacionTecnicaCodigo,
        string? familia,
        string? marca,
        string? modelo,
        string? patente,
        string? numeroSerie,
        string? propiedad,
        string? criticidad,
        string? estadoDocumental,
        string? estadoOperacional,
        IReadOnlyDictionary<string, string?>? technicalFields,
        bool fichaValidada,
        DateTimeOffset fechaAlta,
        DateTimeOffset fechaActualizacion)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Codigo"] = codigo.Trim(),
            ["Nombre"] = nombre.Trim(),
            ["FaenaCodigo"] = faenaCodigo.Trim(),
            ["TipoActivo"] = tipoActivo.Trim(),
            ["Estado"] = estado.ToString(),
            ["UbicacionTecnicaCodigo"] = EmptyToNull(ubicacionTecnicaCodigo),
            ["Familia"] = EmptyToNull(familia),
            ["Marca"] = EmptyToNull(marca),
            ["Modelo"] = EmptyToNull(modelo),
            ["Patente"] = EmptyToNull(patente),
            ["NumeroSerie"] = EmptyToNull(numeroSerie),
            ["Propiedad"] = NormalizeCatalogValue(propiedad, "Propio"),
            ["Criticidad"] = NormalizeCatalogValue(criticidad, "Media"),
            ["EstadoDocumental"] = NormalizeCatalogValue(estadoDocumental, "Pendiente"),
            ["EstadoOperacional"] = NormalizeCatalogValue(estadoOperacional, DefaultOperationalState(estado)),
            ["FichaTecnicaJson"] = SerializeTechnicalFields(technicalFields),
            ["FichaValidada"] = fichaValidada ? "true" : "false",
            ["FechaAlta"] = fechaAlta.UtcDateTime.ToString("O"),
            ["FechaActualizacion"] = fechaActualizacion.UtcDateTime.ToString("O")
        };

        var row = new DataRow(values);
        var completeness = CalculateCompleteness(row, values["EstadoDocumental"] ?? "Pendiente");
        values["CompletitudFicha"] = completeness.Percentage.ToString(CultureInfo.InvariantCulture);
        return new DataRow(values);
    }

    private static DataRow WithValues(DataRow existing, IReadOnlyDictionary<string, string?> nextValues)
    {
        var values = new Dictionary<string, string?>(existing.Values, StringComparer.OrdinalIgnoreCase);
        foreach (var item in nextValues)
        {
            values[item.Key] = item.Value;
        }

        var row = new DataRow(values);
        values["CompletitudFicha"] = CalculateCompleteness(row, values["EstadoDocumental"] ?? "Pendiente")
            .Percentage
            .ToString(CultureInfo.InvariantCulture);

        return new DataRow(values);
    }

    private async Task ValidateReferencesAsync(
        string faenaCodigo,
        string? ubicacionTecnicaCodigo,
        CancellationToken cancellationToken)
    {
        if (!await CodeExistsAsync(FaenasSchema, "Codigo", faenaCodigo, cancellationToken))
        {
            throw new DomainException("La faena indicada no existe.");
        }

        if (!string.IsNullOrWhiteSpace(ubicacionTecnicaCodigo))
        {
            var locationExists = await CodeExistsAsync(LocationsSchema, "Codigo", ubicacionTecnicaCodigo, cancellationToken);
            var matchesFaenaLocation = await FaenaDeclaresLocationAsync(faenaCodigo, ubicacionTecnicaCodigo, cancellationToken);
            if (!locationExists && !matchesFaenaLocation)
            {
                throw new DomainException("La ubicacion tecnica indicada no existe.");
            }
        }
    }

    private async Task EnsureCodeExistsAsync(
        string schemaName,
        string columnName,
        string value,
        string message,
        CancellationToken cancellationToken)
    {
        var rows = await _dataProvider.ReadRowsAsync(schemaName, cancellationToken);
        if (!rows.Any(row => string.Equals(row.GetValue(columnName), value, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainException(message);
        }
    }

    private async Task<bool> CodeExistsAsync(
        string schemaName,
        string columnName,
        string value,
        CancellationToken cancellationToken)
    {
        var rows = await _dataProvider.ReadRowsAsync(schemaName, cancellationToken);
        return rows.Any(row => string.Equals(row.GetValue(columnName), value, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<bool> FaenaDeclaresLocationAsync(
        string faenaCodigo,
        string ubicacionTecnicaCodigo,
        CancellationToken cancellationToken)
    {
        var rows = await _dataProvider.ReadRowsAsync(FaenasSchema, cancellationToken);
        var faena = rows.FirstOrDefault(row => string.Equals(row.GetValue("Codigo"), faenaCodigo, StringComparison.OrdinalIgnoreCase));
        var declaredLocation = FirstNonEmpty(faena, "UbicacionTecnicaCodigo", "Ubicación Técnica", "UbicaciÃ³n TÃ©cnica");
        return string.Equals(declaredLocation, ubicacionTecnicaCodigo, StringComparison.OrdinalIgnoreCase);
    }

    private async Task RecordAssetAuditAsync(
        UserAccessContext user,
        string action,
        string assetCode,
        string? faenaCodigo,
        string? previousValue,
        string? newValue,
        string? detail,
        CancellationToken cancellationToken)
    {
        await _auditService.RecordAsync(new AuditEventRequest(
            user.UserId,
            action,
            AuditModules.Assets,
            "Asset",
            assetCode,
            previousValue,
            newValue,
            faenaCodigo,
            action.Equals("StateChanged", StringComparison.OrdinalIgnoreCase) ? AuditSeverity.High : AuditSeverity.Medium,
            Detail: detail), cancellationToken);
    }

    private void EnsureCanMaintainAssets(UserAccessContext user)
    {
        if (!_authorizationPolicyService.CanAdminister(user) &&
            !user.Roles.Contains(AuthRoles.Planner, StringComparer.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("El usuario no tiene permiso para mantener activos.");
        }
    }

    private void EnsureCanChangeState(UserAccessContext user)
    {
        if (!_authorizationPolicyService.CanAdminister(user) &&
            !user.Roles.Contains(AuthRoles.Planner, StringComparer.OrdinalIgnoreCase) &&
            !user.Roles.Contains(AuthRoles.MaintenanceSupervisor, StringComparer.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("El usuario no tiene permiso para cambiar estados de activos.");
        }
    }

    private void EnsureCanUseFaena(UserAccessContext user, string faenaCodigo)
    {
        if (!_authorizationPolicyService.CanViewFaena(user, faenaCodigo))
        {
            throw new UnauthorizedAccessException("El usuario no tiene acceso a la faena indicada.");
        }
    }

    private void EnsureCanViewAsset(UserAccessContext user, DataRow row)
    {
        var faenaCodigo = row.GetValue("FaenaCodigo")?.Trim() ?? string.Empty;
        if (!_authorizationPolicyService.CanViewFaena(user, faenaCodigo))
        {
            throw new UnauthorizedAccessException("El usuario no tiene acceso al activo solicitado.");
        }
    }

    private static DataRow? FindAssetRow(IReadOnlyCollection<DataRow> rows, string codigo)
    {
        return rows.FirstOrDefault(row =>
            string.Equals(NormalizeCode(row.GetValue("Codigo")), NormalizeCode(codigo), StringComparison.OrdinalIgnoreCase));
    }

    private static int FindAssetIndex(IReadOnlyList<DataRow> rows, string codigo)
    {
        for (var index = 0; index < rows.Count; index++)
        {
            if (string.Equals(NormalizeCode(rows[index].GetValue("Codigo")), NormalizeCode(codigo), StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static AssetStatus ParseStatus(string? value)
    {
        return Enum.TryParse<AssetStatus>(value, ignoreCase: true, out var status)
            ? status
            : AssetStatus.Active;
    }

    private static AssetDocumentResponse ToDocumentResponse(DataRow row)
    {
        var expiresOn = ParseDateOnly(row.GetValue("FechaVencimiento"));
        var isCritical = ParseBool(row.GetValue("Critico"));
        var explicitlyBlocks = ParseBool(row.GetValue("BloqueaDisponibilidad"));
        var isHistorical = ParseBool(row.GetValue("EsHistorico"));
        var rawStatus = row.GetValue("Estado")?.Trim() ?? string.Empty;
        var expired = expiresOn.HasValue && expiresOn.Value < DateOnly.FromDateTime(DateTime.UtcNow);
        var terminalStatus = rawStatus.Equals("Anulado", StringComparison.OrdinalIgnoreCase) ||
                             rawStatus.Equals("Reemplazado", StringComparison.OrdinalIgnoreCase);
        var blocksAvailability = !isHistorical && !terminalStatus && expired && (isCritical || explicitlyBlocks);
        var effectiveStatus = terminalStatus
            ? rawStatus
            : blocksAvailability
                ? "Vencido"
                : rawStatus;

        return new AssetDocumentResponse(
            row.GetValue("EntidadTipo")?.Trim() ?? string.Empty,
            row.GetValue("EntidadCodigo")?.Trim() ?? string.Empty,
            row.GetValue("TipoDocumento")?.Trim() ?? string.Empty,
            effectiveStatus,
            expiresOn,
            EmptyToNull(row.GetValue("ArchivoKey")),
            isCritical,
            expired,
            blocksAvailability);
    }

    private static DataRow ToStateEventRow(AssetStateEventResponse stateEvent)
    {
        return new DataRow(new Dictionary<string, string?>
        {
            ["EventoId"] = stateEvent.EventoId,
            ["ActivoCodigo"] = stateEvent.ActivoCodigo,
            ["EstadoAnterior"] = stateEvent.EstadoAnterior.ToString(),
            ["Estado"] = stateEvent.Estado.ToString(),
            ["FechaEvento"] = stateEvent.FechaEvento.UtcDateTime.ToString("O"),
            ["Motivo"] = stateEvent.Motivo,
            ["UsuarioId"] = stateEvent.UsuarioId
        });
    }

    private static bool IsAssetDocument(DataRow row, string codigo)
    {
        return string.Equals(row.GetValue("EntidadTipo"), "Activo", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(row.GetValue("EntidadCodigo"), codigo, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasCriticalExpiredDocument(string codigo, IReadOnlyCollection<DataRow> documents)
    {
        return documents.Any(row => IsAssetDocument(row, codigo) && ToDocumentResponse(row).BloqueaDisponibilidad);
    }

    private static string EffectiveDocumentState(DataRow row, IReadOnlyCollection<DataRow> documents)
    {
        var codigo = row.GetValue("Codigo")?.Trim() ?? string.Empty;
        if (HasCriticalExpiredDocument(codigo, documents))
        {
            return "Vencido";
        }

        return NormalizeCatalogValue(row.GetValue("EstadoDocumental"), "Pendiente");
    }

    private static string EffectiveOperationalState(DataRow row)
    {
        return NormalizeCatalogValue(row.GetValue("EstadoOperacional"), DefaultOperationalState(ParseStatus(row.GetValue("Estado"))));
    }

    private static string DefaultOperationalState(AssetStatus status)
    {
        return status switch
        {
            AssetStatus.Draft => "Borrador",
            AssetStatus.Active => "Operativo",
            AssetStatus.InMaintenance => "En mantenimiento",
            AssetStatus.Unavailable => "No disponible",
            AssetStatus.Retired => "Retirado",
            _ => "Operativo"
        };
    }

    private static AssetCompleteness CalculateCompleteness(DataRow row, string effectiveDocumentState)
    {
        var missing = new List<string>();
        foreach (var field in RequiredTechnicalFields)
        {
            var value = field.Equals("EstadoDocumental", StringComparison.OrdinalIgnoreCase)
                ? effectiveDocumentState
                : row.GetValue(field);

            if (string.IsNullOrWhiteSpace(value))
            {
                missing.Add(field);
            }
        }

        var completed = RequiredTechnicalFields.Length - missing.Count;
        var percentage = (int)Math.Round((decimal)completed / RequiredTechnicalFields.Length * 100, MidpointRounding.AwayFromZero);
        var state = percentage >= 100 ? "Completa" : completed == 0 ? "Pendiente" : "Parcial";

        return new AssetCompleteness(RequiredTechnicalFields.Length, completed, percentage, state, missing);
    }

    private static IReadOnlyDictionary<string, string?> BuildTechnicalFields(DataRow row)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["TipoActivo"] = EmptyToNull(row.GetValue("TipoActivo")),
            ["Familia"] = EmptyToNull(row.GetValue("Familia")),
            ["Marca"] = EmptyToNull(row.GetValue("Marca")),
            ["Modelo"] = EmptyToNull(row.GetValue("Modelo")),
            ["Patente"] = EmptyToNull(row.GetValue("Patente")),
            ["NumeroSerie"] = EmptyToNull(row.GetValue("NumeroSerie")),
            ["Propiedad"] = EmptyToNull(row.GetValue("Propiedad")),
            ["Criticidad"] = EmptyToNull(row.GetValue("Criticidad")),
            ["UbicacionTecnicaCodigo"] = EmptyToNull(row.GetValue("UbicacionTecnicaCodigo"))
        };

        foreach (var item in ParseTechnicalFields(row.GetValue("FichaTecnicaJson")))
        {
            values[item.Key] = item.Value;
        }

        return values
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, string?> ParseTechnicalFields(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new Dictionary<string, string?>();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string?>>(value) ?? new Dictionary<string, string?>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, string?>();
        }
    }

    private static string? SerializeTechnicalFields(IReadOnlyDictionary<string, string?>? fields)
    {
        if (fields is null || fields.Count == 0)
        {
            return null;
        }

        var clean = fields
            .Where(item => !string.IsNullOrWhiteSpace(item.Key) && !string.IsNullOrWhiteSpace(item.Value))
            .ToDictionary(item => item.Key.Trim(), item => item.Value?.Trim(), StringComparer.OrdinalIgnoreCase);

        return clean.Count == 0 ? null : JsonSerializer.Serialize(clean);
    }

    private static string Serialize(DataRow row)
    {
        return JsonSerializer.Serialize(row.Values);
    }

    private static string NormalizeCode(string? value)
    {
        return value?.Trim().ToUpperInvariant() ?? string.Empty;
    }

    private static string NormalizeCatalogValue(string? value, string defaultValue)
    {
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? FirstNonEmpty(DataRow? row, params string[] columns)
    {
        if (row is null)
        {
            return null;
        }

        foreach (var column in columns)
        {
            var value = EmptyToNull(row.GetValue(column));
            if (value is not null)
            {
                return value;
            }
        }

        return null;
    }

    private static void ValidateRequired(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"El campo {fieldName} es obligatorio.");
        }
    }

    private static bool ParseBool(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               (value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) ||
                value.Trim().Equals("si", StringComparison.OrdinalIgnoreCase) ||
                value.Trim().Equals("1", StringComparison.OrdinalIgnoreCase));
    }

    private static DateOnly? ParseDateOnly(string? value)
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
}
