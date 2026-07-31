using System.Globalization;
using System.Text;

namespace MaintenanceCMMS.Infrastructure.Documents;

/// <summary>Canonical regulatory categories used by the equipment overview.</summary>
public enum RegulatoryDocumentCategory
{
    TechnicalReview,
    Sernageomin,
    Dgmn,
    FireSuppression
}

public static class RegulatoryDocumentCategories
{
    public static readonly RegulatoryDocumentCategory[] All = Enum.GetValues<RegulatoryDocumentCategory>();

    public static RegulatoryDocumentCategory? Classify(string? code, string? name)
    {
        var values = new[] { Normalize(code), Normalize(name) };
        return values.Any(value => value is "REVISIONTECNICA" or "REVTEC") ? RegulatoryDocumentCategory.TechnicalReview
            : values.Any(value => value is "SERNAGEOMIN" or "SNGM") ? RegulatoryDocumentCategory.Sernageomin
            : values.Any(value => value is "DGMN" or "DIRECCIONGENERALDEMOVILIZACIONNACIONAL") ? RegulatoryDocumentCategory.Dgmn
            : values.Any(value => value is "SUPRESIONINCENDIO" or "SUPRESIONDEINCENDIO" or "SISTEMASUPRESIONINCENDIO" or "FIRESUPPRESSION") ? RegulatoryDocumentCategory.FireSuppression
            : null;
    }

    public static string Code(RegulatoryDocumentCategory category) => category.ToString();

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(character))
                builder.Append(char.ToUpperInvariant(character));
        return builder.ToString();
    }
}
