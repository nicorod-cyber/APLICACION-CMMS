namespace MaintenanceCMMS.Application.System;

public interface ISystemInfoService
{
    SystemInfoResponse GetInfo(string dataProvider, string environmentName);
}

