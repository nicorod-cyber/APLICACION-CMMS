using MaintenanceCMMS.Infrastructure.Documents;
using Xunit;

namespace MaintenanceCMMS.Tests;

public sealed class RegulatoryDocumentCategoriesTests
{
    [Theory]
    [InlineData("REVISION_TECNICA")]
    [InlineData("REV-TEC")]
    [InlineData("REVTEC")]
    public void TechnicalReviewAliases_AreClassified(string code) =>
        Assert.Equal(RegulatoryDocumentCategory.TechnicalReview, RegulatoryDocumentCategories.Classify(code, null));

    [Theory]
    [InlineData("SERNAGEOMIN", RegulatoryDocumentCategory.Sernageomin)]
    [InlineData("SNGM", RegulatoryDocumentCategory.Sernageomin)]
    [InlineData("DGMN", RegulatoryDocumentCategory.Dgmn)]
    [InlineData("SUPRESION_DE_INCENDIO", RegulatoryDocumentCategory.FireSuppression)]
    public void RegulatoryAliases_AreClassified(string code, RegulatoryDocumentCategory expected) =>
        Assert.Equal(expected, RegulatoryDocumentCategories.Classify(code, null));

    [Fact]
    public void AccentsSpacesAndNames_AreNormalized() =>
        Assert.Equal(RegulatoryDocumentCategory.TechnicalReview, RegulatoryDocumentCategories.Classify(null, "Revisión técnica"));
}