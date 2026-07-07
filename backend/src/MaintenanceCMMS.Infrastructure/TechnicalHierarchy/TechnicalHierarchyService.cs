using System.Globalization;
using System.Text;
using System.Text.Json;
using MaintenanceCMMS.Application.Abstractions.Data;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.TechnicalHierarchy;
using MaintenanceCMMS.Domain.Common;

namespace MaintenanceCMMS.Infrastructure.TechnicalHierarchy;

public sealed class TechnicalHierarchyService : ITechnicalHierarchyService
{
    private const string HierarchySchema = "sistemas_componentes";
    private const string AssetsSchema = "activos";
    private const string FaenasSchema = "faenas";
    private const string LocationsSchema = "ubicaciones_tecnicas";

    private readonly IDataProvider _dataProvider;
    private readonly IAuditService _auditService;
    private readonly IAuthorizationPolicyService _authorizationPolicyService;

    public TechnicalHierarchyService(
        IDataProvider dataProvider,
        IAuditService auditService,
        IAuthorizationPolicyService authorizationPolicyService)
    {
        _dataProvider = dataProvider;
        _auditService = auditService;
        _authorizationPolicyService = authorizationPolicyService;
    }

    public async Task<IReadOnlyCollection<TechnicalNodeResponse>> ListAsync(
        TechnicalHierarchyQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        await EnsureCanFilterByFaenaAsync(query.FaenaCodigo, user, cancellationToken);
        var rows = await _dataProvider.ReadRowsAsync(HierarchySchema, cancellationToken);
        var assets = await _dataProvider.ReadRowsAsync(AssetsSchema, cancellationToken);

        return BuildResponses(rows)
            .Where(node => MatchesQuery(node, query, rows, assets, user))
            .OrderBy(node => node.Ruta, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<TechnicalHierarchyTreeNode>> GetTreeAsync(
        TechnicalHierarchyQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        var nodes = await ListAsync(query, user, cancellationToken);
        return BuildTree(nodes);
    }

    public async Task<TechnicalNodeResponse?> GetByCodeAsync(
        string code,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        var rows = await _dataProvider.ReadRowsAsync(HierarchySchema, cancellationToken);
        var row = FindByCode(rows, code);
        if (row is null)
        {
            return null;
        }

        var assets = await _dataProvider.ReadRowsAsync(AssetsSchema, cancellationToken);
        var node = BuildResponses(rows).First(item => SameCode(item.Codigo, code));
        return CanViewNode(node, assets, user) ? node : throw new UnauthorizedAccessException("El usuario no tiene acceso al nodo solicitado.");
    }

    public async Task<TechnicalNodeResponse> CreateAsync(
        CreateTechnicalNodeRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanManage(user);
        ValidateRequired(request.Codigo, nameof(request.Codigo));
        ValidateRequired(request.Nombre, nameof(request.Nombre));

        var rows = (await _dataProvider.ReadRowsAsync(HierarchySchema, cancellationToken)).ToList();
        if (FindByCode(rows, request.Codigo) is not null)
        {
            throw new DomainException($"Ya existe un nodo tecnico con codigo '{request.Codigo}'.");
        }

        await ValidateReferencesAsync(
            rows,
            request.Nivel,
            request.CodigoPadre,
            request.FaenaCodigo,
            request.UbicacionTecnicaCodigo,
            request.ActivosAsignados ?? [],
            user,
            cancellationToken);

        var normalizedName = NormalizeName(request.Nombre);
        EnsureNoExactDuplicate(rows, request.Codigo, request.Nivel, request.CodigoPadre, normalizedName);

        var now = DateTimeOffset.UtcNow;
        var row = new DataRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Codigo"] = request.Codigo.Trim(),
            ["Nombre"] = request.Nombre.Trim(),
            ["Nivel"] = request.Nivel.ToString(),
            ["CodigoPadre"] = EmptyToNull(request.CodigoPadre),
            ["NombreNormalizado"] = normalizedName,
            ["FaenaCodigo"] = EmptyToNull(request.FaenaCodigo),
            ["UbicacionTecnicaCodigo"] = EmptyToNull(request.UbicacionTecnicaCodigo),
            ["FamiliasEquipo"] = JoinList(request.FamiliasEquipo ?? []),
            ["ActivosAsignados"] = JoinList(request.ActivosAsignados ?? []),
            ["AliasHistoricos"] = JoinList(request.AliasHistoricos ?? []),
            ["Obsoleto"] = "false",
            ["FusionadoEnCodigo"] = null,
            ["FechaCreacionUtc"] = now.UtcDateTime.ToString("O"),
            ["FechaActualizacionUtc"] = now.UtcDateTime.ToString("O")
        });

        rows.Add(row);
        await _dataProvider.SaveRowsAsync(HierarchySchema, rows, cancellationToken);
        await RecordAuditAsync(user, "Created", request.Codigo, null, Serialize(row), "Nodo tecnico creado", cancellationToken);

        return (await GetByCodeAsync(request.Codigo, user, cancellationToken))!;
    }

    public async Task<TechnicalNodeResponse?> UpdateAsync(
        string code,
        UpdateTechnicalNodeRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanManage(user);
        ValidateRequired(request.Nombre, nameof(request.Nombre));

        var rows = (await _dataProvider.ReadRowsAsync(HierarchySchema, cancellationToken)).ToList();
        var index = FindIndex(rows, code);
        if (index < 0)
        {
            return null;
        }

        var existing = rows[index];
        var level = ParseLevel(existing.GetValue("Nivel"));
        await ValidateReferencesAsync(
            rows,
            level,
            request.CodigoPadre,
            request.FaenaCodigo,
            request.UbicacionTecnicaCodigo,
            request.ActivosAsignados ?? SplitList(existing.GetValue("ActivosAsignados")),
            user,
            cancellationToken);

        var normalizedName = NormalizeName(request.Nombre);
        EnsureNoExactDuplicate(rows, code, level, request.CodigoPadre, normalizedName);

        var aliases = SplitList(existing.GetValue("AliasHistoricos")).ToList();
        var previousName = existing.GetValue("Nombre")?.Trim();
        if (!string.IsNullOrWhiteSpace(previousName) &&
            !string.Equals(previousName, request.Nombre.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            aliases.Add(previousName);
        }

        if (request.AliasHistoricos is not null)
        {
            aliases.AddRange(request.AliasHistoricos);
        }

        var updated = WithValues(existing, new Dictionary<string, string?>
        {
            ["Nombre"] = request.Nombre.Trim(),
            ["CodigoPadre"] = EmptyToNull(request.CodigoPadre),
            ["NombreNormalizado"] = normalizedName,
            ["FaenaCodigo"] = EmptyToNull(request.FaenaCodigo),
            ["UbicacionTecnicaCodigo"] = EmptyToNull(request.UbicacionTecnicaCodigo),
            ["FamiliasEquipo"] = JoinList(request.FamiliasEquipo ?? SplitList(existing.GetValue("FamiliasEquipo"))),
            ["ActivosAsignados"] = JoinList(request.ActivosAsignados ?? SplitList(existing.GetValue("ActivosAsignados"))),
            ["AliasHistoricos"] = JoinList(aliases),
            ["FechaActualizacionUtc"] = DateTimeOffset.UtcNow.UtcDateTime.ToString("O")
        });

        rows[index] = updated;
        await _dataProvider.SaveRowsAsync(HierarchySchema, rows, cancellationToken);
        await RecordAuditAsync(user, "Updated", code, Serialize(existing), Serialize(updated), request.Reason ?? "Nodo tecnico actualizado", cancellationToken);

        return await GetByCodeAsync(code, user, cancellationToken);
    }

    public async Task<TechnicalNodeResponse?> MarkObsoleteAsync(
        string code,
        MarkTechnicalNodeObsoleteRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanManage(user);

        var rows = (await _dataProvider.ReadRowsAsync(HierarchySchema, cancellationToken)).ToList();
        var index = FindIndex(rows, code);
        if (index < 0)
        {
            return null;
        }

        var existing = rows[index];
        var updated = WithValues(existing, new Dictionary<string, string?>
        {
            ["Obsoleto"] = "true",
            ["FechaActualizacionUtc"] = DateTimeOffset.UtcNow.UtcDateTime.ToString("O")
        });

        rows[index] = updated;
        await _dataProvider.SaveRowsAsync(HierarchySchema, rows, cancellationToken);
        await RecordAuditAsync(
            user,
            "MarkedObsolete",
            code,
            Serialize(existing),
            Serialize(updated),
            request.Reason ?? "Nodo tecnico marcado como obsoleto; no se elimina fisicamente.",
            cancellationToken);

        return await GetByCodeAsync(code, user, cancellationToken);
    }

    public async Task<IReadOnlyCollection<SimilarTechnicalNode>> DetectSimilarAsync(
        TechnicalHierarchyQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        var nodes = (await ListAsync(query with { IncludeObsolete = false }, user, cancellationToken)).ToArray();
        var result = new List<SimilarTechnicalNode>();

        for (var leftIndex = 0; leftIndex < nodes.Length; leftIndex++)
        {
            for (var rightIndex = leftIndex + 1; rightIndex < nodes.Length; rightIndex++)
            {
                var left = nodes[leftIndex];
                var right = nodes[rightIndex];
                if (left.Nivel != right.Nivel ||
                    !string.Equals(left.CodigoPadre, right.CodigoPadre, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var similarity = CalculateSimilarity(left.NombreNormalizado, right.NombreNormalizado);
                var aliasMatch = left.AliasHistoricos.Any(alias => NormalizeName(alias) == right.NombreNormalizado) ||
                                 right.AliasHistoricos.Any(alias => NormalizeName(alias) == left.NombreNormalizado);

                if (left.NombreNormalizado == right.NombreNormalizado || aliasMatch || similarity >= 0.82m)
                {
                    result.Add(new SimilarTechnicalNode(
                        left,
                        right,
                        decimal.Round(similarity, 3),
                        aliasMatch ? "Alias historico coincidente" : "Nombre normalizado similar"));
                }
            }
        }

        return result;
    }

    public async Task<TechnicalNodeResponse?> MergeAsync(
        MergeTechnicalNodesRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanManage(user);
        ValidateRequired(request.SourceCode, nameof(request.SourceCode));
        ValidateRequired(request.TargetCode, nameof(request.TargetCode));
        ValidateRequired(request.Reason, nameof(request.Reason));

        if (SameCode(request.SourceCode, request.TargetCode))
        {
            throw new DomainException("El nodo origen y destino deben ser distintos.");
        }

        var rows = (await _dataProvider.ReadRowsAsync(HierarchySchema, cancellationToken)).ToList();
        var sourceIndex = FindIndex(rows, request.SourceCode);
        var targetIndex = FindIndex(rows, request.TargetCode);
        if (sourceIndex < 0 || targetIndex < 0)
        {
            return null;
        }

        var source = rows[sourceIndex];
        var target = rows[targetIndex];
        if (ParseLevel(source.GetValue("Nivel")) != ParseLevel(target.GetValue("Nivel")))
        {
            throw new DomainException("Solo se pueden fusionar nodos del mismo nivel.");
        }

        var targetFamilies = SplitList(target.GetValue("FamiliasEquipo")).Concat(SplitList(source.GetValue("FamiliasEquipo")));
        var targetAssets = SplitList(target.GetValue("ActivosAsignados")).Concat(SplitList(source.GetValue("ActivosAsignados")));
        var targetAliases = SplitList(target.GetValue("AliasHistoricos"))
            .Concat(SplitList(source.GetValue("AliasHistoricos")))
            .Concat([source.GetValue("Codigo"), source.GetValue("Nombre")])
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Select(alias => alias!);

        var now = DateTimeOffset.UtcNow.UtcDateTime.ToString("O");
        target = WithValues(target, new Dictionary<string, string?>
        {
            ["FamiliasEquipo"] = JoinList(targetFamilies),
            ["ActivosAsignados"] = JoinList(targetAssets),
            ["AliasHistoricos"] = JoinList(targetAliases),
            ["FechaActualizacionUtc"] = now
        });

        source = WithValues(source, new Dictionary<string, string?>
        {
            ["Obsoleto"] = "true",
            ["FusionadoEnCodigo"] = target.GetValue("Codigo"),
            ["FechaActualizacionUtc"] = now
        });

        for (var index = 0; index < rows.Count; index++)
        {
            if (index == sourceIndex || index == targetIndex)
            {
                continue;
            }

            if (SameCode(rows[index].GetValue("CodigoPadre"), request.SourceCode))
            {
                rows[index] = WithValues(rows[index], new Dictionary<string, string?>
                {
                    ["CodigoPadre"] = target.GetValue("Codigo"),
                    ["FechaActualizacionUtc"] = now
                });
            }
        }

        rows[targetIndex] = target;
        rows[sourceIndex] = source;

        await _dataProvider.SaveRowsAsync(HierarchySchema, rows, cancellationToken);
        await RecordAuditAsync(
            user,
            "Merged",
            request.TargetCode,
            JsonSerializer.Serialize(new { Source = request.SourceCode, Target = request.TargetCode }),
            Serialize(target),
            request.Reason,
            cancellationToken);

        return await GetByCodeAsync(request.TargetCode, user, cancellationToken);
    }

    public async Task<IReadOnlyCollection<TechnicalNodeResponse>> AssignFamiliesAsync(
        BulkFamilyAssignmentRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanManage(user);
        if (request.NodeCodes.Count == 0)
        {
            throw new DomainException("Debe indicar al menos un nodo.");
        }

        var rows = (await _dataProvider.ReadRowsAsync(HierarchySchema, cancellationToken)).ToList();
        var changed = new List<string>();

        foreach (var code in request.NodeCodes)
        {
            var index = FindIndex(rows, code);
            if (index < 0)
            {
                continue;
            }

            var families = request.Append
                ? SplitList(rows[index].GetValue("FamiliasEquipo")).Concat(request.Families)
                : request.Families;

            rows[index] = WithValues(rows[index], new Dictionary<string, string?>
            {
                ["FamiliasEquipo"] = JoinList(families),
                ["FechaActualizacionUtc"] = DateTimeOffset.UtcNow.UtcDateTime.ToString("O")
            });
            changed.Add(rows[index].GetValue("Codigo") ?? code);
        }

        await _dataProvider.SaveRowsAsync(HierarchySchema, rows, cancellationToken);
        await RecordAuditAsync(user, "AssignedFamilies", string.Join(";", changed), null, JsonSerializer.Serialize(request), "Asignacion masiva de familias", cancellationToken);

        var visible = await ListAsync(new TechnicalHierarchyQuery(IncludeObsolete: true), user, cancellationToken);
        return visible.Where(node => changed.Contains(node.Codigo, StringComparer.OrdinalIgnoreCase)).ToArray();
    }

    public async Task<TechnicalNodeResponse?> AssignAssetsAsync(
        string code,
        AssetAssignmentRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanManage(user);
        var rows = (await _dataProvider.ReadRowsAsync(HierarchySchema, cancellationToken)).ToList();
        var index = FindIndex(rows, code);
        if (index < 0)
        {
            return null;
        }

        await ValidateAssetCodesAsync(request.AssetCodes, user, cancellationToken);
        var assets = request.Append
            ? SplitList(rows[index].GetValue("ActivosAsignados")).Concat(request.AssetCodes)
            : request.AssetCodes;

        var existing = rows[index];
        var updated = WithValues(existing, new Dictionary<string, string?>
        {
            ["ActivosAsignados"] = JoinList(assets),
            ["FechaActualizacionUtc"] = DateTimeOffset.UtcNow.UtcDateTime.ToString("O")
        });

        rows[index] = updated;
        await _dataProvider.SaveRowsAsync(HierarchySchema, rows, cancellationToken);
        await RecordAuditAsync(user, "AssignedAssets", code, Serialize(existing), Serialize(updated), "Asignacion de activos", cancellationToken);

        return await GetByCodeAsync(code, user, cancellationToken);
    }

    private async Task ValidateReferencesAsync(
        IReadOnlyCollection<DataRow> rows,
        TechnicalHierarchyLevel level,
        string? parentCode,
        string? faenaCode,
        string? locationCode,
        IReadOnlyCollection<string> assetCodes,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        ValidateParent(rows, level, parentCode);

        if (!string.IsNullOrWhiteSpace(faenaCode))
        {
            if (!_authorizationPolicyService.CanViewFaena(user, faenaCode))
            {
                throw new UnauthorizedAccessException("El usuario no tiene acceso a la faena indicada.");
            }

            await EnsureCodeExistsAsync(FaenasSchema, "Codigo", faenaCode, "La faena indicada no existe.", cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(locationCode))
        {
            await EnsureCodeExistsAsync(LocationsSchema, "Codigo", locationCode, "La ubicacion tecnica indicada no existe.", cancellationToken);
        }

        await ValidateAssetCodesAsync(assetCodes, user, cancellationToken);
    }

    private async Task ValidateAssetCodesAsync(
        IReadOnlyCollection<string> assetCodes,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        if (assetCodes.Count == 0)
        {
            return;
        }

        var assets = await _dataProvider.ReadRowsAsync(AssetsSchema, cancellationToken);
        foreach (var assetCode in assetCodes.Where(code => !string.IsNullOrWhiteSpace(code)))
        {
            var asset = assets.FirstOrDefault(row => SameCode(row.GetValue("Codigo"), assetCode));
            if (asset is null)
            {
                throw new DomainException($"El activo '{assetCode}' no existe.");
            }

            var faenaCode = asset.GetValue("FaenaCodigo") ?? string.Empty;
            if (!_authorizationPolicyService.CanViewFaena(user, faenaCode))
            {
                throw new UnauthorizedAccessException($"El usuario no tiene acceso al activo '{assetCode}'.");
            }
        }
    }

    private async Task EnsureCanFilterByFaenaAsync(
        string? faenaCode,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(faenaCode))
        {
            return;
        }

        if (!_authorizationPolicyService.CanViewFaena(user, faenaCode))
        {
            throw new UnauthorizedAccessException("El usuario no tiene acceso a la faena solicitada.");
        }

        await EnsureCodeExistsAsync(FaenasSchema, "Codigo", faenaCode, "La faena indicada no existe.", cancellationToken);
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

    private static void ValidateParent(
        IReadOnlyCollection<DataRow> rows,
        TechnicalHierarchyLevel level,
        string? parentCode)
    {
        if (level == TechnicalHierarchyLevel.Sistema)
        {
            if (!string.IsNullOrWhiteSpace(parentCode))
            {
                throw new DomainException("Un sistema no debe tener nodo padre.");
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(parentCode))
        {
            throw new DomainException($"El nivel {level} requiere CodigoPadre.");
        }

        var parent = FindByCode(rows, parentCode);
        if (parent is null)
        {
            throw new DomainException("El nodo padre indicado no existe.");
        }

        if (ParseBool(parent.GetValue("Obsoleto")))
        {
            throw new DomainException("No se puede asociar a un nodo padre obsoleto.");
        }

        var expectedParentLevel = level switch
        {
            TechnicalHierarchyLevel.Subsistema => TechnicalHierarchyLevel.Sistema,
            TechnicalHierarchyLevel.Componente => TechnicalHierarchyLevel.Subsistema,
            TechnicalHierarchyLevel.Subcomponente => TechnicalHierarchyLevel.Componente,
            _ => TechnicalHierarchyLevel.Sistema
        };

        if (ParseLevel(parent.GetValue("Nivel")) != expectedParentLevel)
        {
            throw new DomainException($"El padre de {level} debe ser {expectedParentLevel}.");
        }
    }

    private static IReadOnlyCollection<TechnicalNodeResponse> BuildResponses(IReadOnlyCollection<DataRow> rows)
    {
        var lookup = rows
            .Where(row => !string.IsNullOrWhiteSpace(row.GetValue("Codigo")))
            .ToDictionary(row => NormalizeCode(row.GetValue("Codigo")), row => row, StringComparer.OrdinalIgnoreCase);

        return rows
            .Where(row => !string.IsNullOrWhiteSpace(row.GetValue("Codigo")))
            .Select(row => ToResponse(row, rows, lookup))
            .ToArray();
    }

    private static TechnicalNodeResponse ToResponse(
        DataRow row,
        IReadOnlyCollection<DataRow> rows,
        IReadOnlyDictionary<string, DataRow> lookup)
    {
        var code = row.GetValue("Codigo")?.Trim() ?? string.Empty;
        var level = ParseLevel(row.GetValue("Nivel"));
        var children = rows.Any(child => SameCode(child.GetValue("CodigoPadre"), code) && !ParseBool(child.GetValue("Obsoleto")));
        var assignedAssets = SplitList(row.GetValue("ActivosAsignados"));
        var families = SplitList(row.GetValue("FamiliasEquipo"));
        var aliases = SplitList(row.GetValue("AliasHistoricos"));

        return new TechnicalNodeResponse(
            code,
            row.GetValue("Nombre")?.Trim() ?? string.Empty,
            row.GetValue("NombreNormalizado")?.Trim() ?? NormalizeName(row.GetValue("Nombre")),
            level,
            EmptyToNull(row.GetValue("CodigoPadre")),
            EmptyToNull(row.GetValue("FaenaCodigo")),
            EmptyToNull(row.GetValue("UbicacionTecnicaCodigo")),
            families,
            assignedAssets,
            aliases,
            ParseBool(row.GetValue("Obsoleto")),
            EmptyToNull(row.GetValue("FusionadoEnCodigo")),
            ParseDate(row.GetValue("FechaCreacionUtc")),
            ParseDate(row.GetValue("FechaActualizacionUtc")),
            BuildPath(row, lookup),
            children,
            children || assignedAssets.Count > 0);
    }

    private static string BuildPath(DataRow row, IReadOnlyDictionary<string, DataRow> lookup)
    {
        var parts = new Stack<string>();
        var current = row;
        var guard = 0;

        while (guard < 20)
        {
            parts.Push(current.GetValue("Nombre")?.Trim() ?? current.GetValue("Codigo")?.Trim() ?? string.Empty);
            var parentCode = current.GetValue("CodigoPadre");
            if (string.IsNullOrWhiteSpace(parentCode) || !lookup.TryGetValue(NormalizeCode(parentCode), out var parent))
            {
                break;
            }

            current = parent;
            guard++;
        }

        return string.Join(" / ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static IReadOnlyCollection<TechnicalHierarchyTreeNode> BuildTree(IReadOnlyCollection<TechnicalNodeResponse> nodes)
    {
        var byParent = nodes
            .GroupBy(node => NormalizeCode(node.CodigoPadre))
            .ToDictionary(group => group.Key, group => group.OrderBy(node => node.Nombre, StringComparer.OrdinalIgnoreCase).ToArray());
        var availableCodes = nodes.Select(node => NormalizeCode(node.Codigo)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var roots = nodes
            .Where(node => string.IsNullOrWhiteSpace(node.CodigoPadre) || !availableCodes.Contains(NormalizeCode(node.CodigoPadre)))
            .OrderBy(node => node.Nombre, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return roots.Select(Build).ToArray();

        TechnicalHierarchyTreeNode Build(TechnicalNodeResponse node)
        {
            var children = byParent.TryGetValue(NormalizeCode(node.Codigo), out var directChildren)
                ? directChildren.Select(Build).ToArray()
                : [];

            return new TechnicalHierarchyTreeNode(node, children);
        }
    }

    private static bool MatchesQuery(
        TechnicalNodeResponse node,
        TechnicalHierarchyQuery query,
        IReadOnlyCollection<DataRow> rows,
        IReadOnlyCollection<DataRow> assets,
        UserAccessContext user)
    {
        if (!query.IncludeObsolete && node.Obsoleto)
        {
            return false;
        }

        if (query.Nivel.HasValue && node.Nivel != query.Nivel.Value)
        {
            return false;
        }

        if (!CanViewNode(node, assets, user))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.FaenaCodigo) && !NodeMatchesFaena(node, assets, query.FaenaCodigo))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.Familia) &&
            !node.FamiliasEquipo.Contains(query.Familia, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.SistemaCodigo) &&
            !SameCode(ResolveSystemCode(node.Codigo, rows), query.SistemaCodigo))
        {
            return false;
        }

        return true;
    }

    private static bool CanViewNode(
        TechnicalNodeResponse node,
        IReadOnlyCollection<DataRow> assets,
        UserAccessContext user)
    {
        if (user.Roles.Contains(AuthRoles.Admin, StringComparer.OrdinalIgnoreCase) ||
            user.Permissions.Contains(AuthPermissions.Administration, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(node.FaenaCodigo))
        {
            return user.Faenas.Contains(node.FaenaCodigo, StringComparer.OrdinalIgnoreCase);
        }

        var assignedAssetFaenas = assets
            .Where(asset => node.ActivosAsignados.Contains(asset.GetValue("Codigo") ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            .Select(asset => asset.GetValue("FaenaCodigo") ?? string.Empty)
            .Where(faena => !string.IsNullOrWhiteSpace(faena))
            .ToArray();

        return assignedAssetFaenas.Length == 0 || assignedAssetFaenas.Any(faena => user.Faenas.Contains(faena, StringComparer.OrdinalIgnoreCase));
    }

    private static bool NodeMatchesFaena(
        TechnicalNodeResponse node,
        IReadOnlyCollection<DataRow> assets,
        string faenaCode)
    {
        return string.Equals(node.FaenaCodigo, faenaCode, StringComparison.OrdinalIgnoreCase) ||
               assets.Any(asset =>
                   node.ActivosAsignados.Contains(asset.GetValue("Codigo") ?? string.Empty, StringComparer.OrdinalIgnoreCase) &&
                   string.Equals(asset.GetValue("FaenaCodigo"), faenaCode, StringComparison.OrdinalIgnoreCase));
    }

    private static string? ResolveSystemCode(string code, IReadOnlyCollection<DataRow> rows)
    {
        var lookup = rows.ToDictionary(row => NormalizeCode(row.GetValue("Codigo")), row => row, StringComparer.OrdinalIgnoreCase);
        var currentCode = NormalizeCode(code);
        var guard = 0;

        while (guard < 20 && lookup.TryGetValue(currentCode, out var current))
        {
            if (ParseLevel(current.GetValue("Nivel")) == TechnicalHierarchyLevel.Sistema)
            {
                return current.GetValue("Codigo");
            }

            currentCode = NormalizeCode(current.GetValue("CodigoPadre"));
            guard++;
        }

        return null;
    }

    private static void EnsureNoExactDuplicate(
        IReadOnlyCollection<DataRow> rows,
        string code,
        TechnicalHierarchyLevel level,
        string? parentCode,
        string normalizedName)
    {
        var duplicate = rows.Any(row =>
            !SameCode(row.GetValue("Codigo"), code) &&
            !ParseBool(row.GetValue("Obsoleto")) &&
            ParseLevel(row.GetValue("Nivel")) == level &&
            SameCode(row.GetValue("CodigoPadre"), parentCode) &&
            string.Equals(row.GetValue("NombreNormalizado") ?? NormalizeName(row.GetValue("Nombre")), normalizedName, StringComparison.OrdinalIgnoreCase));

        if (duplicate)
        {
            throw new DomainException("Ya existe un nodo tecnico con el mismo nombre normalizado en el mismo nivel y padre.");
        }
    }

    private async Task RecordAuditAsync(
        UserAccessContext user,
        string action,
        string entityId,
        string? previousValue,
        string? newValue,
        string? detail,
        CancellationToken cancellationToken)
    {
        await _auditService.RecordAsync(new AuditEventRequest(
            user.UserId,
            action,
            AuditModules.TechnicalHierarchy,
            "TechnicalHierarchy",
            entityId,
            previousValue,
            newValue,
            Severity: action.Equals("Merged", StringComparison.OrdinalIgnoreCase) ? AuditSeverity.High : AuditSeverity.Medium,
            Detail: detail), cancellationToken);
    }

    private void EnsureCanManage(UserAccessContext user)
    {
        if (!_authorizationPolicyService.CanManageTechnicalHierarchy(user))
        {
            throw new UnauthorizedAccessException("El usuario no tiene permiso para gestionar jerarquia tecnica.");
        }
    }

    public static string NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        var previousWasSpace = false;

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToUpperInvariant(character));
                previousWasSpace = false;
                continue;
            }

            if (!previousWasSpace)
            {
                builder.Append(' ');
                previousWasSpace = true;
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC).Trim();
    }

    private static decimal CalculateSimilarity(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) && string.IsNullOrWhiteSpace(right))
        {
            return 1;
        }

        var maxLength = Math.Max(left.Length, right.Length);
        if (maxLength == 0)
        {
            return 1;
        }

        var distance = LevenshteinDistance(left, right);
        return 1 - ((decimal)distance / maxLength);
    }

    private static int LevenshteinDistance(string left, string right)
    {
        var distances = new int[left.Length + 1, right.Length + 1];
        for (var i = 0; i <= left.Length; i++)
        {
            distances[i, 0] = i;
        }

        for (var j = 0; j <= right.Length; j++)
        {
            distances[0, j] = j;
        }

        for (var i = 1; i <= left.Length; i++)
        {
            for (var j = 1; j <= right.Length; j++)
            {
                var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                distances[i, j] = Math.Min(
                    Math.Min(distances[i - 1, j] + 1, distances[i, j - 1] + 1),
                    distances[i - 1, j - 1] + cost);
            }
        }

        return distances[left.Length, right.Length];
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

    private static DataRow? FindByCode(IReadOnlyCollection<DataRow> rows, string? code)
    {
        return rows.FirstOrDefault(row => SameCode(row.GetValue("Codigo"), code));
    }

    private static int FindIndex(IReadOnlyList<DataRow> rows, string? code)
    {
        for (var index = 0; index < rows.Count; index++)
        {
            if (SameCode(rows[index].GetValue("Codigo"), code))
            {
                return index;
            }
        }

        return -1;
    }

    private static TechnicalHierarchyLevel ParseLevel(string? value)
    {
        return Enum.TryParse<TechnicalHierarchyLevel>(value, ignoreCase: true, out var level)
            ? level
            : TechnicalHierarchyLevel.Sistema;
    }

    private static string Serialize(DataRow row)
    {
        return JsonSerializer.Serialize(row.Values);
    }

    private static string NormalizeCode(string? value)
    {
        return value?.Trim().ToUpperInvariant() ?? string.Empty;
    }

    private static bool SameCode(string? left, string? right)
    {
        return string.Equals(NormalizeCode(left), NormalizeCode(right), StringComparison.OrdinalIgnoreCase);
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

    private static string? JoinList(IEnumerable<string?> values)
    {
        var clean = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return clean.Length == 0 ? null : string.Join(';', clean);
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool ParseBool(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               (value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) ||
                value.Trim().Equals("si", StringComparison.OrdinalIgnoreCase) ||
                value.Trim().Equals("1", StringComparison.OrdinalIgnoreCase));
    }

    private static DateTimeOffset? ParseDate(string? value)
    {
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var result)
            ? result
            : null;
    }
}
