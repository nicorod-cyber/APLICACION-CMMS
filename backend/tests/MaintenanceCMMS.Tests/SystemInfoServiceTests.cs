using MaintenanceCMMS.Application.System;
using MaintenanceCMMS.Domain.System;
using Xunit;

namespace MaintenanceCMMS.Tests;

public sealed class SystemInfoServiceTests
{
    [Fact]
    public void GetInfo_ReturnsConfiguredProductMetadata()
    {
        var service = new SystemInfoService();

        var info = service.GetInfo("Excel", "Test");

        Assert.Equal(ProductInfo.Name, info.Name);
        Assert.Equal(ProductInfo.Description, info.Description);
        Assert.Equal("Excel", info.DataProvider);
        Assert.Equal("Test", info.Environment);
    }
}
