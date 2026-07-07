using MaintenanceCMMS.Application.Abstractions.Data;
using MaintenanceCMMS.Application.Alerts;
using MaintenanceCMMS.Application.Assets;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.Documents;
using MaintenanceCMMS.Application.Faenas;
using MaintenanceCMMS.Application.Governance;
using MaintenanceCMMS.Application.Imports;
using MaintenanceCMMS.Application.Inventory;
using MaintenanceCMMS.Application.MaterialRequests;
using MaintenanceCMMS.Application.Procurement;
using MaintenanceCMMS.Application.Storage;
using MaintenanceCMMS.Application.TechnicalHierarchy;
using MaintenanceCMMS.Application.WorkNotifications;
using MaintenanceCMMS.Application.WorkOrders;
using MaintenanceCMMS.Domain.Enums;
using MaintenanceCMMS.Infrastructure.Alerts;
using MaintenanceCMMS.Infrastructure.Auditing;
using MaintenanceCMMS.Infrastructure.Assets;
using MaintenanceCMMS.Infrastructure.Data;
using MaintenanceCMMS.Infrastructure.Data.Excel;
using MaintenanceCMMS.Infrastructure.Data.Sql;
using MaintenanceCMMS.Infrastructure.Documents;
using MaintenanceCMMS.Infrastructure.Faenas;
using MaintenanceCMMS.Infrastructure.Governance;
using MaintenanceCMMS.Infrastructure.Imports;
using MaintenanceCMMS.Infrastructure.Inventory;
using MaintenanceCMMS.Infrastructure.MaterialRequests;
using MaintenanceCMMS.Infrastructure.Procurement;
using MaintenanceCMMS.Infrastructure.Options;
using MaintenanceCMMS.Infrastructure.Security;
using MaintenanceCMMS.Infrastructure.SharePoint;
using MaintenanceCMMS.Infrastructure.TechnicalHierarchy;
using MaintenanceCMMS.Infrastructure.WorkNotifications;
using MaintenanceCMMS.Infrastructure.WorkOrders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MaintenanceCMMS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var dataProviderSettings = ResolveDataProviderSettings(configuration);

        services.AddSingleton(dataProviderSettings);
        services.Configure<DataProviderSettings>(options =>
        {
            options.Provider = dataProviderSettings.Provider;
            options.ExcelPath = dataProviderSettings.ExcelPath;
            options.SqlServerConnectionString = dataProviderSettings.SqlServerConnectionString;
            options.PostgreSqlConnectionString = dataProviderSettings.PostgreSqlConnectionString;
        });

        services.Configure<DataProviderOptions>(configuration.GetSection("DataProviders"));
        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        services.Configure<AuthSeedOptions>(configuration.GetSection("Auth:SeedAdmin"));
        services.Configure<SharePointOptions>(configuration.GetSection("SharePoint"));
        services.Configure<MailOptions>(configuration.GetSection("Mail"));
        services.Configure<PdfOptions>(configuration.GetSection("Pdf"));
        services.Configure<PowerBIOptions>(configuration.GetSection("PowerBI"));
        services.Configure<OfflineOptions>(configuration.GetSection("Offline"));
        services.Configure<ImportStorageOptions>(configuration.GetSection("Imports"));

        services.AddSingleton<IExcelSchemaRegistry, ExcelSchemaRegistry>();
        services.AddScoped<ExcelDataProvider>();
        services.AddScoped<SqlDataProvider>();
        services.AddScoped<IDataProvider>(provider =>
        {
            var settings = provider.GetRequiredService<DataProviderSettings>();
            return ResolveProviderType(settings.Provider) == DataProviderType.Excel
                ? provider.GetRequiredService<ExcelDataProvider>()
                : provider.GetRequiredService<SqlDataProvider>();
        });

        services.AddScoped(typeof(IExcelRepository<>), typeof(ExcelRepository<>));
        services.AddScoped(typeof(ISqlRepository<>), typeof(SqlRepository<>));
        services.AddScoped(typeof(IRepository<>), typeof(ExcelRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IImportService, ImportService>();
        services.AddScoped<IIdentityStore, ExcelIdentityStore>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<IIdentitySeedService, IdentitySeedService>();
        services.AddScoped<IAuditService, ExcelAuditService>();
        services.AddSingleton<IAuditContextAccessor, AuditContextAccessor>();
        services.AddScoped<IDataGovernanceService, DataGovernanceService>();
        services.AddScoped<IExcelImportWorkflowService, ExcelImportWorkflowService>();
        services.AddScoped<IFaenaService, FaenaService>();
        services.AddScoped<IAssetService, AssetService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IMaterialRequestService, MaterialRequestService>();
        services.AddScoped<IProcurementService, ProcurementService>();
        services.AddScoped<IWorkNotificationService, WorkNotificationService>();
        services.AddScoped<IWorkOrderService, WorkOrderService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IAlertService, AlertService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IPdfService, PdfService>();
        services.AddScoped<IPdfTemplateService, PdfTemplateService>();
        services.AddScoped<SharePointManualLinkService>();
        services.AddScoped<LocalSharePointSimulationService>();
        services.AddScoped<GraphSharePointService>();
        services.AddScoped<IDocumentStorageService>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<SharePointOptions>>().Value;
            return ResolveSharePointMode(options.Provider) switch
            {
                SharePointMode.ManualLink => provider.GetRequiredService<SharePointManualLinkService>(),
                SharePointMode.GraphApiReady => provider.GetRequiredService<GraphSharePointService>(),
                _ => provider.GetRequiredService<LocalSharePointSimulationService>()
            };
        });
        services.AddScoped<ITechnicalHierarchyService, TechnicalHierarchyService>();
        services.AddScoped<IAuthorizationPolicyService, AuthorizationPolicyService>();
        services.AddScoped<IExternalIdentityProvider, MicrosoftEntraIdentityProvider>();

        return services;
    }

    private static DataProviderSettings ResolveDataProviderSettings(IConfiguration configuration)
    {
        var section = configuration.GetSection("DataProvider");
        var provider = section["Provider"];

        if (string.IsNullOrWhiteSpace(provider))
        {
            provider = configuration["DataProvider"];
        }

        provider = string.IsNullOrWhiteSpace(provider) ? "Excel" : provider;

        var legacyExcelPath = configuration["DataProviders:Excel:BasePath"];
        var legacySqlServerName = configuration["DataProviders:SqlServer:ConnectionStringName"];
        var legacyPostgreSqlName = configuration["DataProviders:PostgreSql:ConnectionStringName"];

        return new DataProviderSettings
        {
            Provider = provider,
            ExcelPath = section["ExcelPath"] ?? legacyExcelPath ?? "data/excel",
            SqlServerConnectionString = section["SqlServerConnectionString"]
                ?? ResolveConnectionString(configuration, legacySqlServerName)
                ?? string.Empty,
            PostgreSqlConnectionString = section["PostgreSqlConnectionString"]
                ?? ResolveConnectionString(configuration, legacyPostgreSqlName)
                ?? string.Empty
        };
    }

    private static string? ResolveConnectionString(IConfiguration configuration, string? connectionStringName)
    {
        if (string.IsNullOrWhiteSpace(connectionStringName))
        {
            return null;
        }

        return configuration.GetConnectionString(connectionStringName) ?? Environment.GetEnvironmentVariable(connectionStringName);
    }

    private static DataProviderType ResolveProviderType(string provider)
    {
        return Enum.TryParse<DataProviderType>(provider, ignoreCase: true, out var providerType)
            ? providerType
            : DataProviderType.Excel;
    }

    private static SharePointMode ResolveSharePointMode(string? provider)
    {
        return provider?.Trim().ToLowerInvariant() switch
        {
            "manuallink" or "manual" => SharePointMode.ManualLink,
            "graphapiready" or "graph" or "microsoftgraph" => SharePointMode.GraphApiReady,
            "localsimulation" or "localsimulator" or "local" => SharePointMode.LocalSimulation,
            _ => SharePointMode.LocalSimulation
        };
    }

    private enum SharePointMode
    {
        ManualLink,
        LocalSimulation,
        GraphApiReady
    }
}
