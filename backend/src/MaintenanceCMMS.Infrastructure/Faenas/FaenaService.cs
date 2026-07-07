using MaintenanceCMMS.Application.Abstractions.Data;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.Faenas;

namespace MaintenanceCMMS.Infrastructure.Faenas;

public sealed class FaenaService : IFaenaService
{
    private const string FaenasSchema = "faenas";

    private readonly IDataProvider _dataProvider;
    private readonly IAuthorizationPolicyService _authorizationPolicyService;

    public FaenaService(
        IDataProvider dataProvider,
        IAuthorizationPolicyService authorizationPolicyService)
    {
        _dataProvider = dataProvider;
        _authorizationPolicyService = authorizationPolicyService;
    }

    public async Task<IReadOnlyCollection<FaenaResponse>> ListAsync(
        FaenaQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        return (await _dataProvider.ReadRowsAsync(FaenasSchema, cancellationToken))
            .Select(ToResponse)
            .Where(item => query.IncludeInactive || item.Activa)
            .Where(item => _authorizationPolicyService.CanViewFaena(user, item.Codigo))
            .Where(item => MatchesSearch(item, query.Search))
            .OrderBy(item => item.Nombre, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Codigo, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static FaenaResponse ToResponse(DataRow row)
    {
        var estado = FirstNonEmpty(row, "Estado", "Activa") ?? "Activa";
        var metadata = row.Values
            .Where(item => !KnownColumns.Contains(item.Key))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);

        return new FaenaResponse(
            row.GetValue("Codigo")?.Trim() ?? string.Empty,
            row.GetValue("Nombre")?.Trim() ?? string.Empty,
            row.GetValue("Empresa")?.Trim() ?? string.Empty,
            EmptyToNull(row.GetValue("Descripcion")),
            FirstNonEmpty(row, "UbicacionTecnicaCodigo", "Ubicación Técnica"),
            FirstNonEmpty(row, "CentroCostos", "centro_costes"),
            FirstNonEmpty(row, "TipoFaena", "tipo_faena"),
            FirstNonEmpty(row, "Region", "region"),
            FirstNonEmpty(row, "Comuna", "comuna"),
            FirstNonEmpty(row, "Latitud", "latitud"),
            FirstNonEmpty(row, "Longitud", "longitud"),
            FirstNonEmpty(row, "Responsable", "responsable"),
            estado,
            !estado.Equals("Inactiva", StringComparison.OrdinalIgnoreCase) &&
            !estado.Equals("Inactivo", StringComparison.OrdinalIgnoreCase),
            metadata);
    }

    private static bool MatchesSearch(FaenaResponse item, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        var value = search.Trim();
        return Contains(item.Codigo, value) ||
               Contains(item.Nombre, value) ||
               Contains(item.Empresa, value) ||
               Contains(item.Region, value) ||
               Contains(item.Comuna, value) ||
               Contains(item.Responsable, value);
    }

    private static string? FirstNonEmpty(DataRow row, params string[] columns)
    {
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

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool Contains(string? value, string search)
    {
        return !string.IsNullOrWhiteSpace(value) && value.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private static readonly IReadOnlySet<string> KnownColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Codigo",
        "Nombre",
        "Empresa",
        "Descripcion",
        "UbicacionTecnicaCodigo",
        "Ubicación Técnica",
        "CentroCostos",
        "centro_costes",
        "TipoFaena",
        "tipo_faena",
        "Region",
        "region",
        "Comuna",
        "comuna",
        "Latitud",
        "latitud",
        "Longitud",
        "longitud",
        "Responsable",
        "responsable",
        "Estado"
    };
}
