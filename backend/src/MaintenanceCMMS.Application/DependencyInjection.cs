using MaintenanceCMMS.Application.System;
using Microsoft.Extensions.DependencyInjection;

namespace MaintenanceCMMS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<ISystemInfoService, SystemInfoService>();

        return services;
    }
}

