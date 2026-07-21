using MaintenanceCMMS.Application.Documents;

namespace MaintenanceCMMS.Infrastructure.Documents;

public sealed record DocumentComplianceResult(
    DocumentLifecycleStatus Status,
    int? DaysToExpire,
    bool IsCompliant,
    bool BlocksAvailability,
    string? Observation);

public static class DocumentComplianceCalculator
{
    public static DocumentComplianceResult Evaluate(
        string? rawStatus,
        DateOnly? expiresOn,
        int alertDays,
        bool hasCurrentFile,
        bool blocksAvailability,
        DateOnly? today = null)
    {
        var date = today ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var parsed = Enum.TryParse<DocumentLifecycleStatus>(rawStatus, true, out var value)
            ? value
            : DocumentLifecycleStatus.PendienteValidacion;

        var effective = parsed;
        if (parsed is not DocumentLifecycleStatus.Rechazado and not DocumentLifecycleStatus.Reemplazado and not DocumentLifecycleStatus.Anulado)
        {
            if (!hasCurrentFile) effective = DocumentLifecycleStatus.PendienteCarga;
            else if (parsed == DocumentLifecycleStatus.PendienteValidacion) effective = DocumentLifecycleStatus.PendienteValidacion;
            else if (expiresOn is { } expiry && expiry < date) effective = DocumentLifecycleStatus.Vencido;
            else if (expiresOn is { } warning && warning <= date.AddDays(Math.Max(0, alertDays))) effective = DocumentLifecycleStatus.PorVencer;
            else effective = DocumentLifecycleStatus.Vigente;
        }

        var compliant = effective is DocumentLifecycleStatus.Vigente or DocumentLifecycleStatus.PorVencer;
        var days = expiresOn?.DayNumber - date.DayNumber;
        return new(
            effective,
            days,
            compliant,
            blocksAvailability && !compliant,
            compliant ? null : $"Documento {ToCode(effective)}.");
    }

    public static string ToCode(DocumentLifecycleStatus status) => status switch
    {
        DocumentLifecycleStatus.PorVencer => "POR_VENCER",
        DocumentLifecycleStatus.PendienteCarga => "PENDIENTE_CARGA",
        DocumentLifecycleStatus.PendienteValidacion => "PENDIENTE_VALIDACION",
        _ => status.ToString().ToUpperInvariant()
    };
}
