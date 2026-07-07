namespace MaintenanceCMMS.Application.System;

public sealed record SystemInfoResponse(
    string Name,
    string Description,
    string Version,
    string Environment,
    string DataProvider,
    DateTimeOffset ServerTimeUtc);

