namespace MaintenanceCMMS.Application.Faenas;

public static class FaenaZones
{
    public const string ValidationMessage = "La zona debe ser uno de los siguientes valores: Zona 0, Zona 1, Zona 2, Zona 3 o Zona 4.";
    public const string CheckConstraintSql = "zona IS NULL OR zona IN ('Zona 0', 'Zona 1', 'Zona 2', 'Zona 3', 'Zona 4')";

    private static readonly HashSet<string> Values = new(StringComparer.Ordinal)
    {
        "Zona 0",
        "Zona 1",
        "Zona 2",
        "Zona 3",
        "Zona 4"
    };

    public static IReadOnlySet<string> All => Values;

    public static bool IsValid(string? value) => value is not null && Values.Contains(value);
}