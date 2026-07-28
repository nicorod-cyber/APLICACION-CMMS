using MaintenanceCMMS.Infrastructure.Assets;
using Xunit;

namespace MaintenanceCMMS.Tests;

public sealed class AssetOperationalPolicyTests
{
    [Theory]
    [InlineData("OPERATIVO", "FAENA", true)]
    [InlineData("OPERATIVO", "TALLER", false)]
    [InlineData("CON_ALERTA", "FAENA", true)]
    [InlineData("FUERA_SERVICIO", "FAENA", true)]
    [InlineData("PREVENTIVO", "TALLER", true)]
    [InlineData("CORRECTIVO", "TALLER", true)]
    [InlineData("DOCUMENTAL", "TALLER", true)]
    [InlineData("PREPARACION", "FAENA", true)]
    [InlineData("PREPARACION", "TALLER", true)]
    [InlineData("DADO_DE_BAJA", "FAENA", true)]
    [InlineData("DADO_DE_BAJA", "TALLER", true)]
    public void Validates_catalog_compatibility_with_physical_location(string state, string location, bool expected)
    {
        Assert.Equal(expected, AssetOperationalPolicy.IsCompatibleWithLocation(location, state));
    }

    [Theory]
    [InlineData("OPERATIVO", true)]
    [InlineData("CON_ALERTA", true)]
    [InlineData("PREPARACION", false)]
    [InlineData("DOCUMENTAL", false)]
    [InlineData("PREVENTIVO", false)]
    [InlineData("CORRECTIVO", false)]
    [InlineData("FUERA_SERVICIO", false)]
    public void Identifies_available_states(string state, bool expected) =>
        Assert.Equal(expected, AssetOperationalPolicy.IsAvailable(state));

    [Fact]
    public void Decommissioned_state_is_terminal_for_operational_transitions()
    {
        var error = Assert.Throws<MaintenanceCMMS.Domain.Common.DomainException>(() =>
            AssetOperationalPolicy.EnsureTransitionAllowed("DADO_DE_BAJA", "OPERATIVO"));

        Assert.Contains("dado de baja", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(AssetOperationalPolicy.IsExcludedFromOperationalUniverse("DADO_DE_BAJA"));
        Assert.False(AssetOperationalPolicy.AllowsMounting("DADO_DE_BAJA"));
        Assert.False(AssetOperationalPolicy.AllowsReadings("DADO_DE_BAJA"));
        Assert.False(AssetOperationalPolicy.AllowsWorkOrders("DADO_DE_BAJA"));
        Assert.False(AssetOperationalPolicy.AllowsNotices("DADO_DE_BAJA"));
    }
}
