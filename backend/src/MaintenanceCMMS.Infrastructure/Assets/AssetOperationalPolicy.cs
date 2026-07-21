using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;

namespace MaintenanceCMMS.Infrastructure.Assets;

public static class AssetOperationalPolicy
{
    public const string DecommissionedStateCode = "DADO_DE_BAJA";

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedTransitions =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["OPERATIVO_FAENA"] = Set("ALERTA_FAENA", "FUERA_SERVICIO_FAENA", "FUERA_SERVICIO_TALLER", DecommissionedStateCode),
            ["ALERTA_FAENA"] = Set("OPERATIVO_FAENA", "FUERA_SERVICIO_FAENA", "FUERA_SERVICIO_TALLER", DecommissionedStateCode),
            ["FUERA_SERVICIO_FAENA"] = Set("OPERATIVO_FAENA", "ALERTA_FAENA", "FUERA_SERVICIO_TALLER", DecommissionedStateCode),
            ["FUERA_SERVICIO_TALLER"] = Set("OPERATIVO_FAENA", "ALERTA_FAENA", "FUERA_SERVICIO_FAENA", DecommissionedStateCode),
            [DecommissionedStateCode] = Set()
        };

    public static bool IsDecommissioned(AssetEntity asset) =>
        string.Equals(asset.OperationalState?.Code, DecommissionedStateCode, StringComparison.OrdinalIgnoreCase) ||
        asset.DecommissioningDate is { } decommissionedOn && decommissionedOn <= DateOnly.FromDateTime(DateTime.UtcNow);

    public static void EnsureCanStartOperation(AssetEntity asset, string operation)
    {
        if (IsDecommissioned(asset))
        {
            throw new DomainException($"El activo '{asset.Code}' esta dado de baja y no puede utilizarse para {operation}.");
        }
    }

    public static void EnsureTransitionAllowed(string previousCode, string nextCode)
    {
        if (string.Equals(previousCode, nextCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("El nuevo estado operacional debe ser distinto del estado vigente.");
        }

        if (!AllowedTransitions.TryGetValue(previousCode, out var allowed) || !allowed.Contains(nextCode))
        {
            throw new DomainException($"La transicion operacional {previousCode} -> {nextCode} no esta permitida.");
        }
    }

    public static int Severity(AssetOperationalStateEntity state) => state.Severity != 0
        ? state.Severity
        : state.Code.ToUpperInvariant() switch
        {
            "OPERATIVO_FAENA" => 0,
            "ALERTA_FAENA" => 25,
            "FUERA_SERVICIO_FAENA" => 100,
            "FUERA_SERVICIO_TALLER" => 100,
            DecommissionedStateCode => 200,
            _ => 50
        };

    private static IReadOnlySet<string> Set(params string[] values) =>
        new HashSet<string>(values, StringComparer.OrdinalIgnoreCase);
}
