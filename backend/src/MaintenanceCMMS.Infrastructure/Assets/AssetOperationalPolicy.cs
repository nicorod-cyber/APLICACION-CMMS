using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;

namespace MaintenanceCMMS.Infrastructure.Assets;

/// <summary>
/// Authoritative operational-state catalog and the rules that relate it to the
/// current physical location of an asset. Location is deliberately not part of
/// a state code.
/// </summary>
public static class AssetOperationalPolicy
{
    public const string DecommissionedStateCode = "DADO_DE_BAJA";

    public sealed record Definition(string Code, string Name, int Severity, IReadOnlySet<string> AllowedLocationTypes);

    private static readonly IReadOnlyDictionary<string, Definition> DefinitionsByCode =
        new Dictionary<string, Definition>(StringComparer.OrdinalIgnoreCase)
        {
            ["OPERATIVO"] = new("OPERATIVO", "Operativo", 0, Set("FAENA")),
            ["CON_ALERTA"] = new("CON_ALERTA", "Con alerta", 25, Set("FAENA")),
            ["PREPARACION"] = new("PREPARACION", "Preparación", 50, Set("FAENA", "TALLER")),
            ["DOCUMENTAL"] = new("DOCUMENTAL", "Documental", 60, Set("TALLER")),
            ["PREVENTIVO"] = new("PREVENTIVO", "Preventivo", 80, Set("TALLER")),
            ["CORRECTIVO"] = new("CORRECTIVO", "Correctivo", 100, Set("TALLER")),
            ["FUERA_SERVICIO"] = new("FUERA_SERVICIO", "F/S", 120, Set("FAENA")),
            [DecommissionedStateCode] = new(DecommissionedStateCode, "Dado de baja", 200, Set("FAENA", "TALLER"))
        };

    private static readonly IReadOnlyDictionary<string, string> LegacyCodes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["OPERATIVO_FAENA"] = "OPERATIVO",
            ["ALERTA_FAENA"] = "CON_ALERTA",
            ["FUERA_SERVICIO_FAENA"] = "FUERA_SERVICIO",
            ["FUERA_SERVICIO_TALLER"] = "CORRECTIVO",
            ["EN_PREPARACION"] = "PREPARACION"
        };

    public static IReadOnlyCollection<Definition> Definitions => DefinitionsByCode.Values.OrderBy(item => item.Severity).ToArray();

    public static string NormalizeLegacyCode(string? code)
    {
        var normalized = Normalize(code);
        return LegacyCodes.TryGetValue(normalized, out var replacement) ? replacement : normalized;
    }

    public static bool IsLegacyCode(string? code) => LegacyCodes.ContainsKey(Normalize(code));
    public static bool IsValidStateCode(string? code) => DefinitionsByCode.ContainsKey(Normalize(code));
    public static bool IsCompatibleWithLocation(string? locationType, string? stateCode) =>
        DefinitionsByCode.TryGetValue(Normalize(stateCode), out var state) && state.AllowedLocationTypes.Contains(Normalize(locationType));
    public static bool IsAvailable(string? stateCode) => Normalize(stateCode) is "OPERATIVO" or "CON_ALERTA";
    public static bool IsExcludedFromOperationalUniverse(string? stateCode) => Same(stateCode, DecommissionedStateCode);
    public static bool AllowsMounting(string? stateCode) => !IsExcludedFromOperationalUniverse(stateCode);
    public static bool AllowsReadings(string? stateCode) => !IsExcludedFromOperationalUniverse(stateCode);
    public static bool AllowsWorkOrders(string? stateCode) => !IsExcludedFromOperationalUniverse(stateCode);
    public static bool AllowsNotices(string? stateCode) => !IsExcludedFromOperationalUniverse(stateCode);

    public static IReadOnlyCollection<string> SelectableStateCodes(string locationType, bool includeTerminal = true) =>
        Definitions.Where(item => item.AllowedLocationTypes.Contains(Normalize(locationType)) && (includeTerminal || !IsExcludedFromOperationalUniverse(item.Code)))
            .Select(item => item.Code).ToArray();

    public static Definition GetDefinition(string? code) =>
        DefinitionsByCode.TryGetValue(Normalize(code), out var definition)
            ? definition
            : throw new DomainException($"El estado operacional '{code}' no pertenece al catálogo vigente.");

    public static bool IsDecommissioned(AssetEntity asset) =>
        IsExcludedFromOperationalUniverse(asset.OperationalState?.Code) ||
        asset.DecommissioningDate is { } decommissionedOn && decommissionedOn <= DateOnly.FromDateTime(DateTime.UtcNow);

    public static void EnsureCanStartOperation(AssetEntity asset, string operation)
    {
        if (IsDecommissioned(asset))
            throw new DomainException($"El activo '{asset.Code}' está dado de baja y no puede utilizarse para {operation}.");
    }

    public static void EnsureTransitionAllowed(string previousCode, string nextCode)
    {
        var previous = GetDefinition(previousCode);
        var next = GetDefinition(nextCode);
        if (Same(previous.Code, next.Code))
            throw new DomainException("El nuevo estado operacional debe ser distinto del estado vigente.");
        if (IsExcludedFromOperationalUniverse(previous.Code))
            throw new DomainException("Un activo dado de baja no puede cambiar de estado mediante el flujo operacional normal.");
    }

    public static void EnsureCompatibleWithPhysicalLocation(string assetCode, string? locationType, string stateCode, string? stateName = null)
    {
        var location = Normalize(locationType);
        if (location is not "FAENA" and not "TALLER")
            throw new DomainException($"El activo '{assetCode}' no tiene una ubicación física vigente; no es posible registrar un estado operacional.");
        var state = GetDefinition(stateCode);
        if (state.AllowedLocationTypes.Contains(location)) return;

        var allowed = Definitions.Where(item => item.AllowedLocationTypes.Contains(location)).Select(item => item.Name);
        var locationName = location == "FAENA" ? "Faena" : "Taller";
        throw new DomainException($"El estado '{stateName ?? state.Name}' no es compatible con la ubicación actual '{locationName}' del activo '{assetCode}'. Estados permitidos: {string.Join(", ", allowed)}.");
    }

    public static int Severity(AssetOperationalStateEntity state) =>
        DefinitionsByCode.TryGetValue(Normalize(state.Code), out var definition) ? definition.Severity : state.Severity;

    private static IReadOnlySet<string> Set(params string[] values) => new HashSet<string>(values, StringComparer.OrdinalIgnoreCase);
    private static string Normalize(string? value) => value?.Trim().ToUpperInvariant() ?? string.Empty;
    private static bool Same(string? left, string? right) => string.Equals(Normalize(left), Normalize(right), StringComparison.Ordinal);
}

public static class AssetReadingPolicy
{
    public static void EnsureCanRegister(AssetEntity asset, decimal value, DateTimeOffset? readAt, string operation)
    {
        if (!AssetOperationalPolicy.AllowsReadings(asset.OperationalState?.Code))
            throw new DomainException($"El activo '{asset.Code}' está dado de baja y no puede utilizarse para {operation}.");
        if (asset.UsageMeasurementType is not "HOROMETRO" and not "KILOMETRAJE") throw new DomainException("El activo no tiene un tipo de medición de uso válido.");
        if (value < 0) throw new DomainException("La lectura no puede ser negativa.");
        if (readAt is { } valueDate && valueDate > DateTimeOffset.UtcNow.AddMinutes(5)) throw new DomainException("La fecha de lectura no puede estar en el futuro.");
    }
}
