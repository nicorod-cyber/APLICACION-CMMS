using MaintenanceCMMS.Domain.System;

namespace MaintenanceCMMS.Application.System;

public sealed class SystemInfoService : ISystemInfoService
{
    public SystemInfoResponse GetInfo(string dataProvider, string environmentName)
    {
        return new SystemInfoResponse(
            ProductInfo.Name,
            ProductInfo.Description,
            "0.1.0",
            environmentName,
            dataProvider,
            DateTimeOffset.UtcNow);
    }
}

