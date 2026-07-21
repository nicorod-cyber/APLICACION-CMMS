using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using MaintenanceCMMS.Api;
using MaintenanceCMMS.Api.Jobs;
using MaintenanceCMMS.Api.Security;
using MaintenanceCMMS.Application.Abstractions.Data;
using MaintenanceCMMS.Application;
using MaintenanceCMMS.Application.Alerts;
using MaintenanceCMMS.Application.Assets;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.Availability;
using MaintenanceCMMS.Application.Documents;
using MaintenanceCMMS.Application.Costs;
using MaintenanceCMMS.Application.Faenas;
using MaintenanceCMMS.Application.Imports;
using MaintenanceCMMS.Application.Inventory;
using MaintenanceCMMS.Application.MaterialRequests;
using MaintenanceCMMS.Application.MaintenanceTargets;
using MaintenanceCMMS.Application.PreventiveMaintenance;
using MaintenanceCMMS.Application.OperationalUnits;
using MaintenanceCMMS.Application.Procurement;
using MaintenanceCMMS.Application.Scheduling;
using MaintenanceCMMS.Application.Storage;
using MaintenanceCMMS.Application.System;
using MaintenanceCMMS.Application.TechnicalHierarchy;
using MaintenanceCMMS.Application.WorkNotifications;
using MaintenanceCMMS.Application.WorkOrders;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Domain.Enums;
using MaintenanceCMMS.Infrastructure;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.Data.Excel;
using MaintenanceCMMS.Infrastructure.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;
using Quartz;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, loggerConfiguration) =>
{
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console();
});

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddHealthChecks();
var preventiveJobsEnabled = builder.Configuration.GetValue("PreventiveMaintenance:JobsEnabled", true);
var documentaryJobsEnabled = builder.Configuration.GetValue("DocumentCompliance:JobsEnabled", true);
if (preventiveJobsEnabled || documentaryJobsEnabled)
{
    builder.Services.AddQuartz(options =>
    {
        if (preventiveJobsEnabled)
        {
            var jobKey = new JobKey("preventive-maintenance-engine");
            options.AddJob<PreventiveMaintenanceJob>(job => job.WithIdentity(jobKey));
            options.AddTrigger(trigger => trigger
                .ForJob(jobKey)
                .WithIdentity("preventive-maintenance-engine-trigger")
                .WithCronSchedule(builder.Configuration["PreventiveMaintenance:JobCron"] ?? "0 0/30 * * * ?"));
        }

        if (documentaryJobsEnabled)
        {
            var documentaryJobKey = new JobKey("documentary-work-order-engine");
            options.AddJob<DocumentaryWorkOrderJob>(job => job.WithIdentity(documentaryJobKey));
            options.AddTrigger(trigger => trigger.ForJob(documentaryJobKey).WithIdentity("documentary-work-order-engine-trigger").WithCronSchedule(builder.Configuration["DocumentCompliance:JobCron"] ?? "0 15 2 * * ?"));
        }
    });
    builder.Services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);
}
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Ingrese un token JWT Bearer."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Administracion", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context =>
            context.User.IsInRole(AuthRoles.Admin) ||
            context.User.HasClaim("permission", AuthPermissions.Administration));
    });

    options.AddPolicy("VerFaenas", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context =>
            context.User.IsInRole(AuthRoles.Admin) ||
            context.User.HasClaim("permission", AuthPermissions.ViewFaenas));
    });

    options.AddPolicy("CrearFaenas", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context =>
            context.User.IsInRole(AuthRoles.Admin) ||
            context.User.HasClaim("permission", AuthPermissions.CreateFaenas));
    });

    options.AddPolicy("EditarFaenas", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context =>
            context.User.IsInRole(AuthRoles.Admin) ||
            context.User.HasClaim("permission", AuthPermissions.EditFaenas));
    });

    options.AddPolicy("DesactivarFaenas", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context =>
            context.User.IsInRole(AuthRoles.Admin) ||
            context.User.HasClaim("permission", AuthPermissions.DeactivateFaenas));
    });

    options.AddPolicy("Importaciones", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context =>
            context.User.IsInRole(AuthRoles.Admin) ||
            context.User.HasClaim("permission", AuthPermissions.ApproveImports));
    });

    options.AddPolicy("AjustesStock", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context =>
            context.User.IsInRole(AuthRoles.Admin) ||
            context.User.HasClaim("permission", AuthPermissions.AdjustStock));
    });

    options.AddPolicy("CerrarOT", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context =>
            context.User.IsInRole(AuthRoles.Admin) ||
            context.User.IsInRole(AuthRoles.MaintenanceSupervisor) ||
            context.User.HasClaim("permission", AuthPermissions.CloseWorkOrders));
    });

    options.AddPolicy("RegistrarLecturasActivos", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("permission", AuthPermissions.RegisterAssetReadings);
    });

    options.AddPolicy("CorregirLecturasActivos", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("permission", AuthPermissions.CorrectAssetReadings);
    });

    options.AddPolicy("VerUnidadesOperativas", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("permission", AuthPermissions.ViewOperationalUnits);
    });

    options.AddPolicy("AdministrarUnidadesOperativas", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("permission", AuthPermissions.ManageOperationalUnits);
    });

    options.AddPolicy("GestionarComposicionUnidadesOperativas", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("permission", AuthPermissions.ManageOperationalUnitComposition);
    });

    options.AddPolicy("ValidarOTFinal", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context =>
            context.User.IsInRole(AuthRoles.Admin) ||
            context.User.IsInRole(AuthRoles.Planner) ||
            context.User.HasClaim("permission", AuthPermissions.FinalValidateWorkOrders));
    });
});

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .GetChildren()
    .Select(origin => origin.Value)
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .ToArray();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins(allowedOrigins!)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

var migrationDatabaseTarget = "unknown";
string[] pendingMigrations = [];

try
{
    await using var migrationScope = app.Services.CreateAsyncScope();
    var dbContext = migrationScope.ServiceProvider.GetRequiredService<CmmsDbContext>();
    migrationDatabaseTarget = DescribeDatabaseTarget(dbContext.Database.GetConnectionString());

    pendingMigrations = (await dbContext.Database.GetPendingMigrationsAsync(CancellationToken.None)).ToArray();
    var nextMigration = pendingMigrations.FirstOrDefault() ?? "(none)";

    Log.Information(
        "Applying {PendingMigrationCount} pending EF Core migration(s) to {DatabaseTarget}. Next migration: {MigrationId}.",
        pendingMigrations.Length,
        migrationDatabaseTarget,
        nextMigration);

    await dbContext.Database.MigrateAsync(CancellationToken.None);

    Log.Information(
        "EF Core migrations completed for {DatabaseTarget}. Applied migrations in this bootstrap: {AppliedMigrations}.",
        migrationDatabaseTarget,
        pendingMigrations);
}
catch (Exception exception)
{
    var postgresException = FindPostgresException(exception);
    var failedMigration = pendingMigrations.FirstOrDefault() ?? "unknown";
    var sqlState = postgresException?.SqlState ?? "n/a";

    Log.Fatal(
        exception,
        "Database migration bootstrap failed for {DatabaseTarget}. Migration: {MigrationId}. PostgreSQL SQLSTATE: {PostgreSqlState}.",
        migrationDatabaseTarget,
        failedMigration,
        sqlState);

    if (IsLegacyOperationalDataSetBlock(postgresException, exception))
    {
        Log.Fatal(
            "Migration is blocked by legacy rows in public.conjuntos_datos_operacionales. " +
            "Do not remove the migration guard. Create and verify backups, run " +
            "backend/scripts/ReportLegacyOperationalDataSets.sql, then " +
            "backend/scripts/ClearLegacyOperationalDataSets.sql with its explicit confirmation.");
    }

    await app.DisposeAsync();
    return 1;
}

await using (var structuralBootstrapScope = app.Services.CreateAsyncScope())
{
    var structuralBootstrap = structuralBootstrapScope.ServiceProvider.GetRequiredService<IPostgreSqlStructuralBootstrap>();
    await structuralBootstrap.BootstrapAsync(CancellationToken.None);
}
if (app.Environment.IsDevelopment() && builder.Configuration.GetValue("Database:SeedDemoData", false))
{
    await using var developmentDemoScope = app.Services.CreateAsyncScope();
    var developmentSeeder = developmentDemoScope.ServiceProvider.GetRequiredService<IPostgreSqlDevelopmentSeeder>();
    await developmentSeeder.SeedDemoDataAsync(CancellationToken.None);
}

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var statusCode = error is BadHttpRequestException
        ? StatusCodes.Status400BadRequest
        : StatusCodes.Status500InternalServerError;

    if (statusCode == StatusCodes.Status400BadRequest)
    {
        Log.Warning(
            "Rejected malformed API request. TraceId: {TraceId}; ErrorType: {ErrorType}.",
            context.TraceIdentifier,
            error?.GetType().Name ?? "Unknown");
    }
    else
    {
        Log.Error(
            "Unhandled API exception. TraceId: {TraceId}; ErrorType: {ErrorType}.",
            context.TraceIdentifier,
            error?.GetType().Name ?? "Unknown");
    }

    await Results.Problem(
            statusCode: statusCode,
            title: statusCode == StatusCodes.Status400BadRequest ? "Solicitud invalida." : "Error interno del servidor.",
            detail: statusCode == StatusCodes.Status400BadRequest ? "La solicitud no es valida." : null,
            extensions: new Dictionary<string, object?> { ["traceId"] = context.TraceIdentifier })
        .ExecuteAsync(context);
}));

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<AuditContextMiddleware>();
app.UseMiddleware<FaenaAuthorizationMiddleware>();

var api = app.MapGroup("/api");

api.MapPost("/auth/login", async (LoginRequest request, IAuthService authService, CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await authService.LoginAsync(request, cancellationToken));
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Unauthorized();
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    })
    .AllowAnonymous()
    .WithName("Login");

api.MapPost("/auth/logout", async (ClaimsPrincipal user, IAuthService authService, CancellationToken cancellationToken) =>
    {
        await authService.LogoutAsync(user, cancellationToken);
        return Results.NoContent();
    })
    .RequireAuthorization()
    .WithName("Logout");

api.MapGet("/auth/me", async (ClaimsPrincipal user, IAuthService authService, CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await authService.GetCurrentUserAsync(user, cancellationToken));
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Unauthorized();
        }
    })
    .RequireAuthorization()
    .WithName("GetCurrentUser");

api.MapGet("/faenas", async (
        string? search,
        bool? includeInactive,
        string? codigo,
        string? nombre,
        string? zona,
        string? cliente,
        string? tipoFaena,
        string? region,
        string? comuna,
        Guid? responsableUsuarioId,
        bool? activo,
        string? ubicacionTecnicaCodigo,
        ClaimsPrincipal user,
        IFaenaService faenaService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await faenaService.ListAsync(
                new FaenaQuery(
                    Search: search,
                    IncludeInactive: includeInactive ?? false,
                    Codigo: codigo,
                    Nombre: nombre,
                    Zona: zona,
                    Cliente: cliente,
                    TipoFaena: tipoFaena,
                    Region: region,
                    Comuna: comuna,
                    ResponsableUsuarioId: responsableUsuarioId,
                    Activa: activo,
                    UbicacionTecnicaCodigo: ubicacionTecnicaCodigo),
                UserAccessContext.FromClaims(user),
                cancellationToken));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .RequireAuthorization("VerFaenas")
    .WithName("ListFaenas");

api.MapGet("/faenas/{code}", async (
        string code,
        ClaimsPrincipal user,
        IFaenaService faenaService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await faenaService.GetByCodeAsync(code, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .RequireAuthorization("VerFaenas")
    .WithName("GetFaena");

api.MapPost("/faenas", async (
        UpsertFaenaRequest request,
        ClaimsPrincipal user,
        IFaenaService faenaService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var created = await faenaService.CreateAsync(request, UserAccessContext.FromClaims(user), cancellationToken);
            return Results.Created($"/api/faenas/{created.Codigo}", created);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .RequireAuthorization("CrearFaenas")
    .WithName("CreateFaena");

api.MapPut("/faenas/{code}", async (
        string code,
        UpsertFaenaRequest request,
        ClaimsPrincipal user,
        IFaenaService faenaService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var updated = await faenaService.UpdateAsync(code, request, UserAccessContext.FromClaims(user), cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .RequireAuthorization("EditarFaenas")
    .WithName("UpdateFaena");

var usersApi = api.MapGroup("/users")
    .RequireAuthorization("Administracion");

usersApi.MapPost("/", async (
        CreateUserRequest request,
        ClaimsPrincipal user,
        IUserManagementService userManagementService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var created = await userManagementService.CreateAsync(request, GetActorId(user), cancellationToken);
            return Results.Created($"/api/users/{created.Id}", created);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    })
    .WithName("CreateUser");

usersApi.MapGet("/", async (IUserManagementService userManagementService, CancellationToken cancellationToken) =>
        Results.Ok(await userManagementService.ListAsync(cancellationToken)))
    .WithName("ListUsers");

usersApi.MapGet("/{id}", async (string id, IUserManagementService userManagementService, CancellationToken cancellationToken) =>
    {
        var result = await userManagementService.GetByIdAsync(id, cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    })
    .WithName("GetUser");

usersApi.MapPut("/{id}", async (
        string id,
        UpdateUserRequest request,
        ClaimsPrincipal user,
        IUserManagementService userManagementService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await userManagementService.UpdateAsync(id, request, GetActorId(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    })
    .WithName("UpdateUser");

usersApi.MapPost("/{id}/roles", async (
        string id,
        AssignUserRolesRequest request,
        ClaimsPrincipal user,
        IUserManagementService userManagementService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await userManagementService.AssignRolesAsync(id, request.Roles, GetActorId(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    })
    .WithName("AssignUserRoles");

usersApi.MapPost("/{id}/faenas", async (
        string id,
        AssignUserFaenasRequest request,
        ClaimsPrincipal user,
        IUserManagementService userManagementService,
        CancellationToken cancellationToken) =>
    {
        var result = await userManagementService.AssignFaenasAsync(id, request.Faenas, GetActorId(user), cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    })
    .WithName("AssignUserFaenas");

usersApi.MapPost("/{id}/lock", async (
        string id,
        ClaimsPrincipal user,
        IUserManagementService userManagementService,
        CancellationToken cancellationToken) =>
    {
        var result = await userManagementService.LockAsync(id, GetActorId(user), cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    })
    .WithName("LockUser");

usersApi.MapPost("/{id}/unlock", async (
        string id,
        ClaimsPrincipal user,
        IUserManagementService userManagementService,
        CancellationToken cancellationToken) =>
    {
        var result = await userManagementService.UnlockAsync(id, GetActorId(user), cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    })
    .WithName("UnlockUser");

api.MapGet("/maintenance-targets", async (
        string? faenaCodigo,
        string? search,
        string? tipo,
        string? scope,
        bool? soloDisponibilidad,
        bool? incluirDadosDeBaja,
        ClaimsPrincipal user,
        IMaintenanceTargetService service,
        CancellationToken cancellationToken) =>
    {
        var targetType = default(MaintenanceTargetType);
        var targetScope = MaintenanceTargetScope.Operational;
        if (!string.IsNullOrWhiteSpace(tipo) && !Enum.TryParse<MaintenanceTargetType>(tipo, true, out targetType))
        {
            return Results.BadRequest(new { message = "El parÃ¡metro tipo no es vÃ¡lido." });
        }
        if (!string.IsNullOrWhiteSpace(scope) && !Enum.TryParse<MaintenanceTargetScope>(scope, true, out targetScope))
        {
            return Results.BadRequest(new { message = "El parÃ¡metro scope no es vÃ¡lido." });
        }
        try
        {
            return Results.Ok(await service.ListAsync(
                new MaintenanceTargetQuery(faenaCodigo, search,
                    string.IsNullOrWhiteSpace(tipo) ? null : targetType,
                    string.IsNullOrWhiteSpace(scope) ? MaintenanceTargetScope.Operational : targetScope,
                    soloDisponibilidad ?? false,
                    incluirDadosDeBaja ?? false),
                UserAccessContext.FromClaims(user), cancellationToken));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    })
    .RequireAuthorization()
    .WithName("ListMaintenanceTargets");
var assetsApi = api.MapGroup("/assets")
    .RequireAuthorization();

assetsApi.MapGet("/catalog", async (ClaimsPrincipal user, IAssetService assetService, CancellationToken cancellationToken) =>
{
    try { return Results.Ok(await assetService.GetCatalogAsync(UserAccessContext.FromClaims(user), cancellationToken)); }
    catch (UnauthorizedAccessException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden); }
})
.WithName("GetAssetCatalog");

assetsApi.MapGet("/attribute-definitions", async (string tipoActivoCodigo, string? familiaEquipoCodigo, ClaimsPrincipal user, IAssetService assetService, CancellationToken cancellationToken) =>
{
    try { return Results.Ok(await assetService.GetApplicableDefinitionsAsync(tipoActivoCodigo, familiaEquipoCodigo, UserAccessContext.FromClaims(user), cancellationToken)); }
    catch (DomainException ex) { return Results.BadRequest(new { message = ex.Message }); }
    catch (UnauthorizedAccessException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden); }
})
.WithName("GetApplicableAssetAttributeDefinitions");
assetsApi.MapGet("/", async (
        string? faenaCodigo,
        string? tipoActivoCodigo,
        string? familiaEquipoCodigo,
        string? criticidad,
        string? estadoOperacionalCodigo,
        string? texto,
        ClaimsPrincipal user,
        IAssetService assetService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await assetService.ListAsync(
                new AssetListQuery(faenaCodigo, tipoActivoCodigo, familiaEquipoCodigo, criticidad, estadoOperacionalCodigo, texto),
                UserAccessContext.FromClaims(user),
                cancellationToken));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("ListAssets");

assetsApi.MapGet("/{id}", async (
        string id,
        ClaimsPrincipal user,
        IAssetService assetService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await assetService.GetByIdAsync(id, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("GetAsset");

assetsApi.MapPost("/", async (
        CreateAssetRequest request,
        ClaimsPrincipal user,
        IAssetService assetService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var created = await assetService.CreateAsync(request, UserAccessContext.FromClaims(user), cancellationToken);
            return Results.Created($"/api/assets/{created.Resumen.Codigo}", created);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("CreateAsset");

assetsApi.MapPut("/{id}", async (
        string id,
        UpdateAssetRequest request,
        ClaimsPrincipal user,
        IAssetService assetService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await assetService.UpdateAsync(id, request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("UpdateAsset");

assetsApi.MapPost("/{id}/state-events", async (
        string id,
        CreateAssetStateEventRequest request,
        ClaimsPrincipal user,
        IAssetService assetService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await assetService.AddStateEventAsync(id, request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("CreateAssetStateEvent");

assetsApi.MapPost("/{id}/transfers", async (
        string id, TransferAssetRequest request, ClaimsPrincipal user, IAssetService assetService, CancellationToken cancellationToken) =>
    {
        try { return Results.Ok(await assetService.TransferAsync(id, request, UserAccessContext.FromClaims(user), cancellationToken)); }
        catch (DomainException ex) { return Results.BadRequest(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden); }
    })
    .WithName("TransferAsset");

assetsApi.MapGet("/{id}/history", async (
        string id,
        ClaimsPrincipal user,
        IAssetService assetService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await assetService.GetHistoryAsync(id, UserAccessContext.FromClaims(user), cancellationToken));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("GetAssetHistory");

assetsApi.MapGet("/{id}/documents", async (
        string id,
        ClaimsPrincipal user,
        IAssetService assetService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await assetService.GetDocumentsAsync(id, UserAccessContext.FromClaims(user), cancellationToken));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("GetAssetDocuments");

assetsApi.MapGet("/{id}/document-matrix", async (
        string id, ClaimsPrincipal user, IAssetService assetService, CancellationToken cancellationToken) =>
    {
        try { return Results.Ok(await assetService.GetDocumentMatrixAsync(id, UserAccessContext.FromClaims(user), cancellationToken)); }
        catch (UnauthorizedAccessException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden); }
    })
    .WithName("GetAssetDocumentMatrix");

assetsApi.MapGet("/{id}/readings", async (
        string id, ClaimsPrincipal user, IAssetService assetService, CancellationToken cancellationToken) =>
    {
        try { return Results.Ok(await assetService.GetReadingsAsync(id, UserAccessContext.FromClaims(user), cancellationToken)); }
        catch (UnauthorizedAccessException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden); }
    })
    .WithName("GetAssetReadings");

assetsApi.MapPost("/{id}/readings", async (
        string id, CreateAssetReadingRequest request, ClaimsPrincipal user, IAssetService assetService, CancellationToken cancellationToken) =>
    {
        try { var result = await assetService.AddReadingAsync(id, request, UserAccessContext.FromClaims(user), cancellationToken); return result is null ? Results.NotFound() : Results.Ok(result); }
        catch (DomainException ex) { return Results.BadRequest(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden); }
    }).RequireAuthorization("RegistrarLecturasActivos").WithName("CreateAssetReading");

assetsApi.MapPost("/{id}/readings/{readingId}/corrections", async (
        string id, string readingId, CorrectAssetReadingRequest request, ClaimsPrincipal user, IAssetService assetService, CancellationToken cancellationToken) =>
    {
        try { var result = await assetService.CorrectReadingAsync(id, readingId, request, UserAccessContext.FromClaims(user), cancellationToken); return result is null ? Results.NotFound() : Results.Ok(result); }
        catch (DomainException ex) { return Results.BadRequest(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden); }
    }).RequireAuthorization("CorregirLecturasActivos").WithName("CorrectAssetReading");
assetsApi.MapGet("/{id}/costs", async (
        string id,
        ClaimsPrincipal user,
        IAssetService assetService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await assetService.GetCostsAsync(id, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("GetAssetCosts");

assetsApi.MapGet("/{id}/availability", async (
        string id,
        ClaimsPrincipal user,
        IAssetService assetService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await assetService.GetAvailabilityAsync(id, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("GetAssetAvailability");

var operationalUnitsApi = api.MapGroup("/operational-units")
    .RequireAuthorization();

operationalUnitsApi.MapGet("/", async (string? faenaCodigo, ClaimsPrincipal user, IOperationalUnitService service, CancellationToken ct) =>
{
    try { return Results.Ok(await service.ListAsync(faenaCodigo, UserAccessContext.FromClaims(user), ct)); }
    catch (UnauthorizedAccessException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden); }
}).RequireAuthorization("VerUnidadesOperativas").WithName("ListOperationalUnits");

operationalUnitsApi.MapGet("/{codigo}", async (string codigo, ClaimsPrincipal user, IOperationalUnitService service, CancellationToken ct) =>
{
    try { var result = await service.GetAsync(codigo, UserAccessContext.FromClaims(user), ct); return result is null ? Results.NotFound() : Results.Ok(result); }
    catch (UnauthorizedAccessException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden); }
}).RequireAuthorization("VerUnidadesOperativas").WithName("GetOperationalUnit");

operationalUnitsApi.MapPost("/", async (OperationalUnitRequest request, ClaimsPrincipal user, IOperationalUnitService service, CancellationToken ct) =>
{
    try { var result = await service.CreateAsync(request, UserAccessContext.FromClaims(user), ct); return Results.Created($"/api/operational-units/{result.Codigo}", result); }
    catch (DomainException ex) { return Results.BadRequest(new { message = ex.Message }); }
    catch (UnauthorizedAccessException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden); }
}).RequireAuthorization("AdministrarUnidadesOperativas").WithName("CreateOperationalUnit");

operationalUnitsApi.MapPost("/types", async (OperationalUnitTypeRequest request, ClaimsPrincipal user, IOperationalUnitService service, CancellationToken ct) =>
{
    try { return Results.Ok(await service.CreateTypeAsync(request, UserAccessContext.FromClaims(user), ct)); }
    catch (DomainException ex) { return Results.BadRequest(new { message = ex.Message }); }
    catch (UnauthorizedAccessException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden); }
}).RequireAuthorization("AdministrarUnidadesOperativas").WithName("CreateOperationalUnitType");

operationalUnitsApi.MapPost("/roles", async (OperationalUnitRoleRequest request, ClaimsPrincipal user, IOperationalUnitService service, CancellationToken ct) =>
{
    try { return Results.Ok(await service.CreateRoleAsync(request, UserAccessContext.FromClaims(user), ct)); }
    catch (DomainException ex) { return Results.BadRequest(new { message = ex.Message }); }
    catch (UnauthorizedAccessException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden); }
}).RequireAuthorization("AdministrarUnidadesOperativas").WithName("CreateOperationalUnitRole");

operationalUnitsApi.MapPut("/rules", async (OperationalUnitRuleRequest request, ClaimsPrincipal user, IOperationalUnitService service, CancellationToken ct) =>
{
    try { return Results.Ok(await service.UpsertRuleAsync(request, UserAccessContext.FromClaims(user), ct)); }
    catch (DomainException ex) { return Results.BadRequest(new { message = ex.Message }); }
    catch (UnauthorizedAccessException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden); }
}).RequireAuthorization("GestionarComposicionUnidadesOperativas").WithName("UpsertOperationalUnitRule");

operationalUnitsApi.MapPost("/{codigo}/components", async (string codigo, MountOperationalUnitComponentRequest request, ClaimsPrincipal user, IOperationalUnitService service, CancellationToken ct) =>
{
    try { var result = await service.MountAsync(codigo, request, UserAccessContext.FromClaims(user), ct); return result is null ? Results.NotFound() : Results.Ok(result); }
    catch (DomainException ex) { return Results.BadRequest(new { message = ex.Message }); }
    catch (UnauthorizedAccessException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden); }
}).RequireAuthorization("GestionarComposicionUnidadesOperativas").WithName("MountOperationalUnitComponent");

operationalUnitsApi.MapPost("/{codigo}/components/{activoCodigo}/unmount", async (string codigo, string activoCodigo, UnmountOperationalUnitComponentRequest request, ClaimsPrincipal user, IOperationalUnitService service, CancellationToken ct) =>
{
    try { var result = await service.UnmountAsync(codigo, activoCodigo, request, UserAccessContext.FromClaims(user), ct); return result is null ? Results.NotFound() : Results.Ok(result); }
    catch (DomainException ex) { return Results.BadRequest(new { message = ex.Message }); }
    catch (UnauthorizedAccessException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden); }
}).RequireAuthorization("GestionarComposicionUnidadesOperativas").WithName("UnmountOperationalUnitComponent");

operationalUnitsApi.MapPost("/{codigo}/components/replace", async (string codigo, ReplaceOperationalUnitComponentRequest request, ClaimsPrincipal user, IOperationalUnitService service, CancellationToken ct) =>
{
    try { var result = await service.ReplaceAsync(codigo, request, UserAccessContext.FromClaims(user), ct); return result is null ? Results.NotFound() : Results.Ok(result); }
    catch (DomainException ex) { return Results.BadRequest(new { message = ex.Message }); }
    catch (UnauthorizedAccessException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden); }
}).RequireAuthorization("GestionarComposicionUnidadesOperativas").WithName("ReplaceOperationalUnitComponent");
var availabilityApi = api.MapGroup("/availability")
    .RequireAuthorization();

availabilityApi.MapGet("/dashboard", async (
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? faenaCodigo,
        string? contractCode,
        string? cliente,
        AvailabilityPeriod? period,
        ClaimsPrincipal user,
        IAvailabilityService availabilityService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await availabilityService.GetDashboardAsync(
                new AvailabilityQuery(from, to, faenaCodigo, contractCode, cliente, period ?? AvailabilityPeriod.Mes),
                UserAccessContext.FromClaims(user),
                cancellationToken));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("GetAvailabilityDashboard");

availabilityApi.MapGet("/contracts", async (
        string? faenaCodigo,
        string? cliente,
        bool? includeInactive,
        ClaimsPrincipal user,
        IAvailabilityService availabilityService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await availabilityService.ListContractsAsync(
                new AvailabilityContractQuery(faenaCodigo, cliente, includeInactive ?? false),
                UserAccessContext.FromClaims(user),
                cancellationToken));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("ListAvailabilityContracts");

availabilityApi.MapPost("/contracts", async (
        UpsertAvailabilityContractRequest request,
        ClaimsPrincipal user,
        IAvailabilityService availabilityService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await availabilityService.UpsertContractAsync(request, UserAccessContext.FromClaims(user), cancellationToken);
            return Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("UpsertAvailabilityContract");

availabilityApi.MapPost("/contracts/{contractCode}/assets", async (
        string contractCode,
        AssignContractAssetRequest request,
        ClaimsPrincipal user,
        IAvailabilityService availabilityService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await availabilityService.AssignAssetAsync(
                request with { ContractCode = contractCode },
                UserAccessContext.FromClaims(user),
                cancellationToken));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("AssignAvailabilityContractAsset");

availabilityApi.MapPost("/contracts/{contractCode}/targets", async (
        string contractCode,
        AssignContractTargetRequest request,
        ClaimsPrincipal user,
        IAvailabilityService availabilityService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await availabilityService.AssignTargetAsync(
                request with { ContractCode = contractCode },
                UserAccessContext.FromClaims(user), cancellationToken));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("AssignAvailabilityContractTarget");
availabilityApi.MapGet("/events", async (
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? faenaCodigo,
        string? contractCode,
        string? activoCodigo,
        MaintenanceTargetType? tipoObjetivo,
        string? objetivoCodigo,
        AvailabilityCause? cause,
        ClaimsPrincipal user,
        IAvailabilityService availabilityService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await availabilityService.ListEventsAsync(
                new AvailabilityEventQuery(from, to, faenaCodigo, contractCode, activoCodigo, cause, tipoObjetivo, objetivoCodigo),
                UserAccessContext.FromClaims(user),
                cancellationToken));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("ListAvailabilityEvents");

availabilityApi.MapPost("/events", async (
        RegisterAvailabilityEventRequest request,
        ClaimsPrincipal user,
        IAvailabilityService availabilityService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await availabilityService.RegisterEventAsync(request, UserAccessContext.FromClaims(user), cancellationToken));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("RegisterAvailabilityEvent");

var inventoryApi = api.MapGroup("/inventory")
    .RequireAuthorization();

inventoryApi.MapGet("/dashboard", async (
        ClaimsPrincipal user,
        IInventoryService inventoryService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await inventoryService.GetDashboardAsync(UserAccessContext.FromClaims(user), cancellationToken));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("GetInventoryDashboard");

inventoryApi.MapGet("/warehouses", async (
        string? faenaCodigo,
        WarehouseType? tipo,
        bool? includeInactive,
        ClaimsPrincipal user,
        IInventoryService inventoryService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await inventoryService.ListWarehousesAsync(
                new WarehouseQuery(faenaCodigo, tipo, includeInactive ?? false),
                UserAccessContext.FromClaims(user),
                cancellationToken));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("ListWarehouses");

inventoryApi.MapPost("/warehouses", async (
        CreateWarehouseRequest request,
        ClaimsPrincipal user,
        IInventoryService inventoryService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var created = await inventoryService.CreateWarehouseAsync(request, UserAccessContext.FromClaims(user), cancellationToken);
            return Results.Created($"/api/inventory/warehouses/{created.Codigo}", created);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("CreateWarehouse");

inventoryApi.MapGet("/spare-parts", async (
        string? search,
        string? familia,
        SparePartStatus? estado,
        bool? criticalOnly,
        bool? lowStockOnly,
        bool? includeObsolete,
        ClaimsPrincipal user,
        IInventoryService inventoryService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await inventoryService.ListSparePartsAsync(
                new SparePartQuery(search, familia, estado, criticalOnly ?? false, lowStockOnly ?? false, includeObsolete ?? false),
                UserAccessContext.FromClaims(user),
                cancellationToken));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("ListSpareParts");

inventoryApi.MapGet("/spare-parts/{code}", async (
        string code,
        ClaimsPrincipal user,
        IInventoryService inventoryService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await inventoryService.GetSparePartAsync(code, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("GetSparePart");

inventoryApi.MapPost("/spare-parts", async (
        CreateSparePartRequest request,
        ClaimsPrincipal user,
        IInventoryService inventoryService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var created = await inventoryService.CreateSparePartAsync(request, UserAccessContext.FromClaims(user), cancellationToken);
            return Results.Created($"/api/inventory/spare-parts/{created.Summary.Codigo}", created);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("CreateSparePart");

inventoryApi.MapPut("/spare-parts/{code}", async (
        string code,
        UpdateSparePartRequest request,
        ClaimsPrincipal user,
        IInventoryService inventoryService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await inventoryService.UpdateSparePartAsync(code, request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("UpdateSparePart");

inventoryApi.MapGet("/stock", async (
        string? bodegaCodigo,
        string? repuestoCodigo,
        string? faenaCodigo,
        bool? lowStockOnly,
        bool? criticalOnly,
        ClaimsPrincipal user,
        IInventoryService inventoryService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await inventoryService.ListStockAsync(
                new StockQuery(bodegaCodigo, repuestoCodigo, faenaCodigo, lowStockOnly ?? false, criticalOnly ?? false),
                UserAccessContext.FromClaims(user),
                cancellationToken));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("ListStock");

inventoryApi.MapGet("/stock/movements", async (
        string? bodegaCodigo,
        string? repuestoCodigo,
        StockMovementType? type,
        string? referenceType,
        string? referenceId,
        int? take,
        ClaimsPrincipal user,
        IInventoryService inventoryService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await inventoryService.ListMovementsAsync(
                new StockMovementQuery(bodegaCodigo, repuestoCodigo, type, referenceType, referenceId, take ?? 100),
                UserAccessContext.FromClaims(user),
                cancellationToken));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("ListStockMovements");

inventoryApi.MapPost("/stock/movements", async (
        StockMovementRequest request,
        ClaimsPrincipal user,
        IInventoryService inventoryService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await inventoryService.RegisterMovementAsync(request, UserAccessContext.FromClaims(user), cancellationToken));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .RequireAuthorization("AjustesStock")
    .WithName("RegisterStockMovement");

inventoryApi.MapGet("/reservations", async (
        ClaimsPrincipal user,
        IInventoryService inventoryService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await inventoryService.ListReservationsAsync(UserAccessContext.FromClaims(user), cancellationToken));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("ListStockReservations");

inventoryApi.MapPost("/reservations", async (
        CreateStockReservationRequest request,
        ClaimsPrincipal user,
        IInventoryService inventoryService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await inventoryService.CreateReservationAsync(request, UserAccessContext.FromClaims(user), cancellationToken));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .RequireAuthorization("AjustesStock")
    .WithName("CreateStockReservation");

inventoryApi.MapPost("/reservations/{reservationId}/release", async (
        string reservationId,
        ReleaseStockReservationRequest request,
        ClaimsPrincipal user,
        IInventoryService inventoryService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await inventoryService.ReleaseReservationAsync(reservationId, request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .RequireAuthorization("AjustesStock")
    .WithName("ReleaseStockReservation");

inventoryApi.MapPost("/deliveries", async (
        DeliverMaterialRequest request,
        ClaimsPrincipal user,
        IInventoryService inventoryService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await inventoryService.DeliverMaterialAsync(request, UserAccessContext.FromClaims(user), cancellationToken));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .RequireAuthorization("AjustesStock")
    .WithName("DeliverInventoryMaterial");

inventoryApi.MapGet("/transfers", async (
        ClaimsPrincipal user,
        IInventoryService inventoryService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await inventoryService.ListTransfersAsync(UserAccessContext.FromClaims(user), cancellationToken));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("ListStockTransfers");

inventoryApi.MapPost("/transfers", async (
        TransferStockRequest request,
        ClaimsPrincipal user,
        IInventoryService inventoryService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await inventoryService.TransferStockAsync(request, UserAccessContext.FromClaims(user), cancellationToken));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .RequireAuthorization("AjustesStock")
    .WithName("TransferStock");

inventoryApi.MapPost("/transfers/{transferId}/receive", async (
        string transferId,
        ReceiveTransferRequest request,
        ClaimsPrincipal user,
        IInventoryService inventoryService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await inventoryService.ReceiveTransferAsync(transferId, request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .RequireAuthorization("AjustesStock")
    .WithName("ReceiveStockTransfer");

inventoryApi.MapPost("/returns", async (
        ReturnStockRequest request,
        ClaimsPrincipal user,
        IInventoryService inventoryService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await inventoryService.ReturnStockAsync(request, UserAccessContext.FromClaims(user), cancellationToken));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .RequireAuthorization("AjustesStock")
    .WithName("ReturnStock");

inventoryApi.MapPost("/adjustments", async (
        AdjustStockRequest request,
        ClaimsPrincipal user,
        IInventoryService inventoryService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await inventoryService.AdjustStockAsync(request, UserAccessContext.FromClaims(user), cancellationToken));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .RequireAuthorization("AjustesStock")
    .WithName("AdjustStock");

inventoryApi.MapPost("/write-offs", async (
        WriteOffStockRequest request,
        ClaimsPrincipal user,
        IInventoryService inventoryService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await inventoryService.WriteOffStockAsync(request, UserAccessContext.FromClaims(user), cancellationToken));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .RequireAuthorization("AjustesStock")
    .WithName("WriteOffStock");

var materialRequestsApi = api.MapGroup("/material-requests")
    .RequireAuthorization();

materialRequestsApi.MapGet("/", async (
        MaterialRequestStatus? status,
        MaterialRequestType? type,
        MaterialRequestSource? source,
        string? faenaCodigo,
        string? requester,
        bool? includeClosed,
        ClaimsPrincipal user,
        IMaterialRequestService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await service.ListAsync(
                new MaterialRequestQuery(status, type, source, faenaCodigo, requester, includeClosed ?? false),
                UserAccessContext.FromClaims(user),
                cancellationToken));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("ListMaterialRequests");

materialRequestsApi.MapGet("/{id}", async (
        string id,
        ClaimsPrincipal user,
        IMaterialRequestService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await service.GetByIdAsync(id, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("GetMaterialRequest");

materialRequestsApi.MapPost("/", async (
        CreateMaterialRequestRequest request,
        ClaimsPrincipal user,
        IMaterialRequestService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var created = await service.CreateAsync(request, UserAccessContext.FromClaims(user), cancellationToken);
            return Results.Created($"/api/material-requests/{created.NumeroSolicitud}", created);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("CreateMaterialRequest");

materialRequestsApi.MapPost("/{id}/maintenance-approval", async (
        string id,
        MaterialRequestReasonRequest request,
        ClaimsPrincipal user,
        IMaterialRequestService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await service.ApproveMaintenanceAsync(id, request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("ApproveMaterialRequestMaintenance");

materialRequestsApi.MapPost("/{id}/reject", async (
        string id,
        MaterialRequestReasonRequest request,
        ClaimsPrincipal user,
        IMaterialRequestService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await service.RejectAsync(id, request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("RejectMaterialRequest");

materialRequestsApi.MapPost("/{id}/warehouse-review", async (
        string id,
        WarehouseReviewMaterialRequestRequest request,
        ClaimsPrincipal user,
        IMaterialRequestService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await service.ReviewWarehouseAsync(id, request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("ReviewMaterialRequestWarehouse");

materialRequestsApi.MapPost("/{id}/prepare", async (
        string id,
        MaterialRequestReasonRequest request,
        ClaimsPrincipal user,
        IMaterialRequestService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await service.PrepareAsync(id, request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("PrepareMaterialRequest");

materialRequestsApi.MapPost("/{id}/deliver", async (
        string id,
        DeliverRequestedMaterialRequest request,
        ClaimsPrincipal user,
        IMaterialRequestService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await service.DeliverAsync(id, request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("DeliverMaterialRequest");

materialRequestsApi.MapPost("/{id}/receive", async (
        string id,
        MaterialRequestReasonRequest request,
        ClaimsPrincipal user,
        IMaterialRequestService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await service.ReceiveAsync(id, request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("ReceiveMaterialRequest");

materialRequestsApi.MapPost("/{id}/close", async (
        string id,
        MaterialRequestReasonRequest request,
        ClaimsPrincipal user,
        IMaterialRequestService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await service.CloseAsync(id, request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("CloseMaterialRequest");

materialRequestsApi.MapPost("/{id}/convert-to-spare-part", async (
        string id,
        ConvertMaterialRequestToSparePartRequest request,
        ClaimsPrincipal user,
        IMaterialRequestService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await service.ConvertToSparePartAsync(id, request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("ConvertMaterialRequestToSparePart");

var workNotificationsApi = api.MapGroup("/work-notifications")
    .RequireAuthorization();

workNotificationsApi.MapGet("/", async (
        WorkNotificationStatus? status,
        WorkNotificationType? type,
        string? faenaCodigo,
        string? activoCodigo,
        string? unidadOperativaCodigo,
        MaintenanceTargetType? tipoObjetivo,
        string? objetivoCodigo,
        WorkNotificationPriority? priority,
        bool? includeClosed,
        bool? supervisorInbox,
        ClaimsPrincipal user,
        IWorkNotificationService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await service.ListAsync(
                new WorkNotificationQuery(status, type, faenaCodigo, activoCodigo, priority, includeClosed ?? false, supervisorInbox ?? false, unidadOperativaCodigo, tipoObjetivo, objetivoCodigo),
                UserAccessContext.FromClaims(user),
                cancellationToken));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("ListWorkNotifications");

workNotificationsApi.MapGet("/{id}", async (
        string id,
        ClaimsPrincipal user,
        IWorkNotificationService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await service.GetByIdAsync(id, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("GetWorkNotification");

workNotificationsApi.MapPost("/", async (
        CreateWorkNotificationRequest request,
        ClaimsPrincipal user,
        IWorkNotificationService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var created = await service.CreateAsync(request, UserAccessContext.FromClaims(user), cancellationToken);
            return Results.Created($"/api/work-notifications/{created.AvisoId}", created);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("CreateWorkNotification");

workNotificationsApi.MapPost("/{id}/evaluate", async (
        string id,
        WorkNotificationActionRequest request,
        ClaimsPrincipal user,
        IWorkNotificationService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await service.EvaluateAsync(id, request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("EvaluateWorkNotification");

workNotificationsApi.MapPost("/{id}/approve", async (
        string id,
        WorkNotificationActionRequest request,
        ClaimsPrincipal user,
        IWorkNotificationService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await service.ApproveAsync(id, request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("ApproveWorkNotification");

workNotificationsApi.MapPost("/{id}/reject", async (
        string id,
        WorkNotificationActionRequest request,
        ClaimsPrincipal user,
        IWorkNotificationService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await service.RejectAsync(id, request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("RejectWorkNotification");

workNotificationsApi.MapPost("/{id}/convert-to-work-order", async (
        string id,
        ConvertWorkNotificationToWorkOrderRequest request,
        ClaimsPrincipal user,
        IWorkNotificationService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await service.ConvertToWorkOrderAsync(id, request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("ConvertWorkNotificationToWorkOrder");

workNotificationsApi.MapPost("/{id}/annul", async (
        string id,
        WorkNotificationActionRequest request,
        ClaimsPrincipal user,
        IWorkNotificationService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await service.AnnulAsync(id, request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("AnnulWorkNotification");

var workOrdersApi = api.MapGroup("/work-orders")
    .RequireAuthorization();

workOrdersApi.MapGet("/", async (
        WorkOrderLifecycleStatus? status,
        string? faenaCodigo,
        string? technicianId,
        string? activoCodigo,
        string? unidadOperativaCodigo,
        MaintenanceTargetType? tipoObjetivo,
        string? objetivoCodigo,
        bool? includeClosed,
        ClaimsPrincipal user,
        IWorkOrderService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await service.ListAsync(
                new WorkOrderQuery(status, faenaCodigo, technicianId, activoCodigo, includeClosed ?? false, unidadOperativaCodigo, tipoObjetivo, objetivoCodigo),
                UserAccessContext.FromClaims(user),
                cancellationToken));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("ListWorkOrders");

workOrdersApi.MapGet("/{numeroOt}", async (
        string numeroOt,
        ClaimsPrincipal user,
        IWorkOrderService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await service.GetByIdAsync(numeroOt, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("GetWorkOrder");

workOrdersApi.MapPost("/{numeroOt}/schedule", async (
        string numeroOt,
        ScheduleWorkOrderRequest request,
        ClaimsPrincipal user,
        IWorkOrderService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await service.ScheduleAsync(numeroOt, request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("ScheduleWorkOrder");

workOrdersApi.MapPost("/{numeroOt}/start", async (
        string numeroOt,
        WorkOrderActionRequest request,
        ClaimsPrincipal user,
        IWorkOrderService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await service.StartAsync(numeroOt, request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("StartWorkOrder");

workOrdersApi.MapPost("/{numeroOt}/pause", async (
        string numeroOt,
        WorkOrderActionRequest request,
        ClaimsPrincipal user,
        IWorkOrderService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await service.PauseAsync(numeroOt, request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("PauseWorkOrder");

workOrdersApi.MapPost("/{numeroOt}/finish-technician", async (
        string numeroOt,
        WorkOrderActionRequest request,
        ClaimsPrincipal user,
        IWorkOrderService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await service.FinishByTechnicianAsync(numeroOt, request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("FinishWorkOrderByTechnician");

workOrdersApi.MapPost("/{numeroOt}/close-technical", async (
        string numeroOt,
        WorkOrderActionRequest request,
        ClaimsPrincipal user,
        IWorkOrderService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await service.CloseTechnicallyAsync(numeroOt, request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("CloseWorkOrderTechnically");

workOrdersApi.MapPost("/{numeroOt}/validate-planning", async (
        string numeroOt,
        WorkOrderActionRequest request,
        ClaimsPrincipal user,
        IWorkOrderService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await service.ValidatePlanningAsync(numeroOt, request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("ValidateWorkOrderPlanning");

workOrdersApi.MapPost("/{numeroOt}/annul", async (
        string numeroOt,
        WorkOrderActionRequest request,
        ClaimsPrincipal user,
        IWorkOrderService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await service.AnnulAsync(numeroOt, request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("AnnulWorkOrder");

workOrdersApi.MapOperationalWorkOrderEndpoints();

var preventiveApi = api.MapGroup("/preventive")
    .RequireAuthorization();

preventiveApi.MapGet("/plans", async (
        string? faenaCodigo,
        string? activoCodigo,
        string? familiaEquipo,
        PreventiveStatus? estado,
        bool? includeInactive,
        ClaimsPrincipal user,
        IPreventiveMaintenanceService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await service.ListPlansAsync(
                new PreventivePlanQuery(faenaCodigo, activoCodigo, familiaEquipo, estado, includeInactive ?? false),
                UserAccessContext.FromClaims(user),
                cancellationToken));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("ListPreventivePlans");

preventiveApi.MapPost("/plans", async (
        UpsertPreventivePlanRequest request,
        ClaimsPrincipal user,
        IPreventiveMaintenanceService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await service.UpsertPlanAsync(request, UserAccessContext.FromClaims(user), cancellationToken));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("UpsertPreventivePlan");

preventiveApi.MapPost("/plans/{planCode}/generate-ot", async (
        string planCode,
        GeneratePreventiveWorkOrderRequest request,
        ClaimsPrincipal user,
        IPreventiveMaintenanceService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await service.GenerateWorkOrderAsync(planCode, request, UserAccessContext.FromClaims(user), cancellationToken));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("GeneratePreventiveWorkOrder");

preventiveApi.MapPost("/plans/{planCode}/reprogram", async (
        string planCode,
        ReprogramPreventivePlanRequest request,
        ClaimsPrincipal user,
        IPreventiveMaintenanceService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await service.ReprogramAsync(planCode, request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("ReprogramPreventivePlan");

preventiveApi.MapGet("/readings", async (
        string? faenaCodigo,
        string? activoCodigo,
        DateTimeOffset? from,
        DateTimeOffset? to,
        ClaimsPrincipal user,
        IPreventiveMaintenanceService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await service.ListReadingsAsync(
                new PreventiveReadingQuery(faenaCodigo, activoCodigo, from, to),
                UserAccessContext.FromClaims(user),
                cancellationToken));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("ListPreventiveReadings");

preventiveApi.MapPost("/readings", async (
        RegisterPreventiveReadingRequest request,
        ClaimsPrincipal user,
        IPreventiveMaintenanceService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await service.RegisterReadingAsync(request, UserAccessContext.FromClaims(user), cancellationToken));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("RegisterPreventiveReading");

preventiveApi.MapGet("/dashboard", async (
        string? faenaCodigo,
        string? activoCodigo,
        DateTimeOffset? evaluationDate,
        bool? generateWorkOrders,
        ClaimsPrincipal user,
        IPreventiveMaintenanceService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await service.EvaluateAsync(
                new PreventiveEvaluationQuery(faenaCodigo, activoCodigo, evaluationDate, generateWorkOrders ?? false),
                UserAccessContext.FromClaims(user),
                cancellationToken));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("GetPreventiveDashboard");

preventiveApi.MapPost("/run", async (
        ClaimsPrincipal user,
        IPreventiveMaintenanceService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await service.RunAutomaticEvaluationAsync(UserAccessContext.FromClaims(user), cancellationToken));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("RunPreventiveEngine");

var schedulingApi = api.MapGroup("/scheduling")
    .RequireAuthorization();

schedulingApi.MapGet("/board", async (
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? faenaCodigo,
        string? tallerCodigo,
        ScheduleViewMode? view,
        bool? includeClosed,
        ClaimsPrincipal user,
        ISchedulingService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await service.GetBoardAsync(
                new ScheduleBoardQuery(from, to, faenaCodigo, tallerCodigo, view ?? ScheduleViewMode.Semanal, includeClosed ?? false),
                UserAccessContext.FromClaims(user),
                cancellationToken));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("GetSchedulingBoard");

schedulingApi.MapGet("/workshops", async (
        ClaimsPrincipal user,
        ISchedulingService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await service.ListWorkshopsAsync(UserAccessContext.FromClaims(user), cancellationToken));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("ListSchedulingWorkshops");

schedulingApi.MapPost("/workshops", async (
        UpsertWorkshopRequest request,
        ClaimsPrincipal user,
        ISchedulingService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await service.UpsertWorkshopAsync(request, UserAccessContext.FromClaims(user), cancellationToken));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("UpsertSchedulingWorkshop");

schedulingApi.MapPost("/work-orders/{numeroOt}", async (
        string numeroOt,
        ScheduleWorkOrderPlanningRequest request,
        ClaimsPrincipal user,
        ISchedulingService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await service.ScheduleWorkOrderAsync(numeroOt, request, UserAccessContext.FromClaims(user), cancellationToken));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("ScheduleWorkOrderPlanning");

schedulingApi.MapPost("/dependencies", async (
        AddScheduleDependencyRequest request,
        ClaimsPrincipal user,
        ISchedulingService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await service.AddDependencyAsync(request, UserAccessContext.FromClaims(user), cancellationToken));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("AddSchedulingDependency");

schedulingApi.MapGet("/alerts", async (
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? faenaCodigo,
        string? tallerCodigo,
        ScheduleViewMode? view,
        ClaimsPrincipal user,
        ISchedulingService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await service.ListAlertsAsync(
                new ScheduleBoardQuery(from, to, faenaCodigo, tallerCodigo, view ?? ScheduleViewMode.Semanal),
                UserAccessContext.FromClaims(user),
                cancellationToken));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("ListSchedulingAlerts");

var procurementApi = api.MapGroup("/procurement")
    .RequireAuthorization();

procurementApi.MapGet("/suppliers", async (
        string? search,
        bool? includeInactive,
        ClaimsPrincipal user,
        IProcurementService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await service.ListSuppliersAsync(
                new SupplierQuery(search, includeInactive ?? false),
                UserAccessContext.FromClaims(user),
                cancellationToken));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("ListProcurementSuppliers");

procurementApi.MapGet("/suppliers/{rut}", async (
        string rut,
        ClaimsPrincipal user,
        IProcurementService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await service.GetSupplierAsync(rut, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("GetProcurementSupplier");

procurementApi.MapPost("/suppliers", async (
        UpsertSupplierRequest request,
        ClaimsPrincipal user,
        IProcurementService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var created = await service.CreateSupplierAsync(request, UserAccessContext.FromClaims(user), cancellationToken);
            return Results.Created($"/api/procurement/suppliers/{created.Rut}", created);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("CreateProcurementSupplier");

procurementApi.MapPut("/suppliers/{rut}", async (
        string rut,
        UpsertSupplierRequest request,
        ClaimsPrincipal user,
        IProcurementService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await service.UpdateSupplierAsync(rut, request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("UpdateProcurementSupplier");

procurementApi.MapGet("/requests", async (
        ProcurementRequestStatus? status,
        string? supplierRut,
        string? faenaCodigo,
        string? repuestoCodigo,
        string? solicitudInternaCmms,
        bool? includeClosed,
        bool? overdueOnly,
        ClaimsPrincipal user,
        IProcurementService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await service.ListRequestsAsync(
                new ProcurementRequestQuery(status, supplierRut, faenaCodigo, repuestoCodigo, solicitudInternaCmms, includeClosed ?? false, overdueOnly ?? false),
                UserAccessContext.FromClaims(user),
                cancellationToken));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("ListProcurementRequests");

procurementApi.MapGet("/requests/{id}", async (
        string id,
        ClaimsPrincipal user,
        IProcurementService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await service.GetRequestAsync(id, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("GetProcurementRequest");

procurementApi.MapPost("/requests", async (
        CreateProcurementRequestRequest request,
        ClaimsPrincipal user,
        IProcurementService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var created = await service.CreateRequestAsync(request, UserAccessContext.FromClaims(user), cancellationToken);
            return Results.Created($"/api/procurement/requests/{created.SolicitudId}", created);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("CreateProcurementRequest");

procurementApi.MapPost("/requests/{id}/purchase-order", async (
        string id,
        LinkPurchaseOrderRequest request,
        ClaimsPrincipal user,
        IProcurementService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await service.LinkPurchaseOrderAsync(id, request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("LinkProcurementPurchaseOrder");

procurementApi.MapPost("/requests/{id}/receptions", async (
        string id,
        RegisterProcurementReceptionRequest request,
        ClaimsPrincipal user,
        IProcurementService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await service.RegisterReceptionAsync(id, request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("RegisterProcurementReception");

procurementApi.MapPost("/requests/{id}/delivery", async (
        string id,
        DeliverProcurementRequest request,
        ClaimsPrincipal user,
        IProcurementService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await service.RegisterDeliveryAsync(id, request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("RegisterProcurementDelivery");

var documentsApi = api.MapGroup("/documents")
    .RequireAuthorization();

documentsApi.MapGet("/types", async (IDocumentService documentService, CancellationToken cancellationToken) =>
        Results.Ok(await documentService.ListTypesAsync(cancellationToken)))
    .WithName("ListDocumentTypes");

documentsApi.MapPost("/types", async (
        CreateDocumentTypeRequest request,
        ClaimsPrincipal user,
        IDocumentService documentService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var created = await documentService.CreateTypeAsync(request, UserAccessContext.FromClaims(user), cancellationToken);
            return Results.Created($"/api/documents/types/{created.Codigo}", created);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("CreateDocumentType");

documentsApi.MapPut("/types/{code}", async (
        string code,
        UpdateDocumentTypeRequest request,
        ClaimsPrincipal user,
        IDocumentService documentService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await documentService.UpdateTypeAsync(code, request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("UpdateDocumentType");

documentsApi.MapGet("/expired", async (
        string? faenaCodigo,
        ClaimsPrincipal user,
        IDocumentService documentService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await documentService.GetExpiredAsync(faenaCodigo, UserAccessContext.FromClaims(user), cancellationToken));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("ListExpiredDocuments");

documentsApi.MapGet("/expiring", async (
        string? faenaCodigo,
        ClaimsPrincipal user,
        IDocumentService documentService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await documentService.GetExpiringAsync(faenaCodigo, UserAccessContext.FromClaims(user), cancellationToken));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("ListExpiringDocuments");

documentsApi.MapGet("/requirement-matrices", async (bool? incluirHistoricas, ClaimsPrincipal user, IDocumentRequirementMatrixService service, CancellationToken cancellationToken) =>
    {
        try { return Results.Ok(await service.ListAsync(incluirHistoricas ?? false, UserAccessContext.FromClaims(user), cancellationToken)); }
        catch (UnauthorizedAccessException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden); }
    }).WithName("ListDocumentRequirementMatrices");

documentsApi.MapPost("/requirement-matrices/versions", async (CreateDocumentRequirementMatrixVersionRequest request, ClaimsPrincipal user, IDocumentRequirementMatrixService service, CancellationToken cancellationToken) =>
    {
        try { var created = await service.CreateVersionAsync(request, UserAccessContext.FromClaims(user), cancellationToken); return Results.Created($"/api/documents/requirement-matrices/{created.Id}", created); }
        catch (DomainException ex) { return Results.BadRequest(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden); }
    }).WithName("CreateDocumentRequirementMatrixVersion");

documentsApi.MapPost("/documentary-engine/run", async (DateOnly? fechaReferencia, ClaimsPrincipal user, IDocumentaryWorkOrderService service, CancellationToken cancellationToken) =>
    {
        var access = UserAccessContext.FromClaims(user);
        if (!access.Roles.Contains(AuthRoles.Planner, StringComparer.OrdinalIgnoreCase)) return Results.Problem("Solo Planificacion puede ejecutar el motor documental.", statusCode: StatusCodes.Status403Forbidden);
        return Results.Ok(await service.RunAsync(fechaReferencia ?? DateOnly.FromDateTime(DateTime.UtcNow), access.UserId, cancellationToken));
    }).WithName("RunDocumentaryEngine");

documentsApi.MapGet("/matrix", async (
        string? faenaCodigo,
        ClaimsPrincipal user,
        IDocumentService documentService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await documentService.GetMatrixAsync(faenaCodigo, UserAccessContext.FromClaims(user), cancellationToken));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("GetDocumentMatrix");

documentsApi.MapGet("/summary", async (
        string? faenaCodigo,
        ClaimsPrincipal user,
        IDocumentService documentService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await documentService.GetSummaryAsync(faenaCodigo, UserAccessContext.FromClaims(user), cancellationToken));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("GetDocumentSummary");

documentsApi.MapGet("/", async (
        DocumentEntityType? entidadTipo,
        string? entidadCodigo,
        string? faenaCodigo,
        string? tipoDocumento,
        DocumentLifecycleStatus? estado,
        bool? includeHistorical,
        ClaimsPrincipal user,
        IDocumentService documentService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await documentService.ListAsync(
                new DocumentQuery(entidadTipo, entidadCodigo, faenaCodigo, tipoDocumento, estado, includeHistorical ?? false),
                UserAccessContext.FromClaims(user),
                cancellationToken));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("ListDocuments");

documentsApi.MapPost("/", async (
        CreateDocumentRequest request,
        ClaimsPrincipal user,
        IDocumentService documentService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var created = await documentService.CreateAsync(request, UserAccessContext.FromClaims(user), cancellationToken);
            return Results.Created($"/api/documents/{created.DocumentoId}", created);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("CreateDocument");

documentsApi.MapGet("/{id}", async (
        string id,
        ClaimsPrincipal user,
        IDocumentService documentService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await documentService.GetByIdAsync(id, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("GetDocument");

documentsApi.MapPut("/{id}", async (
        string id,
        UpdateDocumentRequest request,
        ClaimsPrincipal user,
        IDocumentService documentService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await documentService.UpdateAsync(id, request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("UpdateDocument");

documentsApi.MapPost("/{id}/validate", async (
        string id,
        ValidateDocumentRequest request,
        ClaimsPrincipal user,
        IDocumentService documentService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await documentService.ValidateAsync(id, request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("ValidateDocument");

documentsApi.MapPost("/{id}/reject", async (
        string id,
        RejectDocumentRequest request,
        ClaimsPrincipal user,
        IDocumentService documentService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await documentService.RejectAsync(id, request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("RejectDocument");

documentsApi.MapPost("/{id}/replace", async (
        string id,
        ReplaceDocumentRequest request,
        ClaimsPrincipal user,
        IDocumentService documentService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await documentService.ReplaceAsync(id, request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("ReplaceDocument");


documentsApi.MapGet("/{id}/versions", async (
        string id,
        ClaimsPrincipal user,
        IDocumentService documentService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await documentService.ListVersionsAsync(id, UserAccessContext.FromClaims(user), cancellationToken));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("ListDocumentVersions");

documentsApi.MapPost("/{id}/assets", async (
        string id,
        AssignDocumentAssetsRequest request,
        ClaimsPrincipal user,
        IDocumentService documentService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await documentService.AssignAssetsAsync(id, request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("AssignDocumentAssets");

documentsApi.MapPost("/{id}/assets/{assetCode}/unassign", async (
        string id,
        string assetCode,
        UnassignDocumentAssetRequest request,
        ClaimsPrincipal user,
        IDocumentService documentService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await documentService.UnassignAssetAsync(id, assetCode, request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("UnassignDocumentAsset");
documentsApi.MapPost("/{id}/annul", async (
        string id,
        AnnulDocumentRequest request,
        ClaimsPrincipal user,
        IDocumentService documentService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await documentService.AnnulAsync(id, request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("AnnulDocument");

var sharePointApi = api.MapGroup("/sharepoint")
    .RequireAuthorization();

sharePointApi.MapGet("/status", (IDocumentStorageService documentStorageService) =>
        Results.Ok(documentStorageService.GetProviderInfo()))
    .WithName("GetSharePointStorageStatus");

sharePointApi.MapGet("/files", async (
        string? purpose,
        string? module,
        string? entityType,
        string? entityId,
        string? faenaCodigo,
        string? activoCodigo,
        string? otNumero,
        bool? includeInactive,
        IDocumentStorageService documentStorageService,
        CancellationToken cancellationToken) =>
    {
        var parsedPurpose = ParseEnumOrDefault<DocumentStoragePurpose>(purpose, out var storagePurpose)
            ? storagePurpose
            : (DocumentStoragePurpose?)null;

        return Results.Ok(await documentStorageService.ListAsync(new DocumentStorageQuery(
            parsedPurpose,
            module,
            entityType,
            entityId,
            faenaCodigo,
            activoCodigo,
            otNumero,
            includeInactive ?? false), cancellationToken));
    })
    .WithName("ListSharePointFiles");

sharePointApi.MapGet("/link", async (
        string fileKey,
        IDocumentStorageService documentStorageService,
        CancellationToken cancellationToken) =>
    {
        var link = await documentStorageService.GetLinkAsync(fileKey, cancellationToken);
        return link is null ? Results.NotFound() : Results.Ok(new { fileKey, url = link });
    })
    .WithName("GetSharePointFileLink");

sharePointApi.MapGet("/download", async (
        string fileKey,
        IDocumentStorageService documentStorageService,
        CancellationToken cancellationToken) =>
    {
        var download = await documentStorageService.DownloadAsync(fileKey, cancellationToken);
        return download is null
            ? Results.NotFound()
            : Results.File(download.Content, download.ContentType, download.FileName);
    })
    .WithName("DownloadSharePointFile");

sharePointApi.MapPost("/folders", async (
        SharePointFolderApiRequest request,
        IDocumentStorageService documentStorageService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await documentStorageService.CreateFolderAsync(new DocumentStorageFolderRequest(
                request.Module,
                request.EntityType,
                request.EntityId,
                request.Purpose,
                request.FaenaCodigo,
                request.ActivoCodigo,
                request.OtNumero), cancellationToken));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    })
    .WithName("CreateSharePointFolder");

sharePointApi.MapPost("/validate-path", async (
        SharePointFolderApiRequest request,
        IDocumentStorageService documentStorageService,
        CancellationToken cancellationToken) =>
        Results.Ok(await documentStorageService.ValidatePathAsync(new DocumentStoragePathRequest(
            request.Module,
            request.EntityType,
            request.EntityId,
            request.Purpose,
            request.FaenaCodigo,
            request.ActivoCodigo,
            request.OtNumero), cancellationToken)))
    .WithName("ValidateSharePointPath");

sharePointApi.MapPost("/files/manual-link", async (
        SharePointManualLinkApiRequest request,
        ClaimsPrincipal user,
        IDocumentStorageService documentStorageService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await documentStorageService.SaveManualLinkAsync(new ManualDocumentLinkRequest(
                request.Module,
                request.EntityType,
                request.EntityId,
                request.FileName,
                request.Url,
                GetActorId(user),
                request.Purpose,
                request.FaenaCodigo,
                request.ActivoCodigo,
                request.OtNumero,
                request.Metadata), cancellationToken));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    })
    .WithName("SaveSharePointManualLink");

sharePointApi.MapPost("/files/upload", async (
        HttpRequest request,
        ClaimsPrincipal user,
        IDocumentStorageService documentStorageService,
        CancellationToken cancellationToken) =>
    {
        if (!request.HasFormContentType)
        {
            return Results.BadRequest(new { message = "La carga debe enviarse como multipart/form-data." });
        }

        var providerInfo = documentStorageService.GetProviderInfo();
        if (!providerInfo.SupportsUpload)
        {
            return Results.BadRequest(new { message = $"El modo SharePoint '{providerInfo.Mode}' no permite subir archivos desde el CMMS." });
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file");
        if (file is null || file.Length == 0)
        {
            return Results.BadRequest(new { message = "Debe adjuntar un archivo." });
        }

        var entityType = form["entityType"].FirstOrDefault();
        var entityId = form["entityId"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(entityType) || string.IsNullOrWhiteSpace(entityId))
        {
            return Results.BadRequest(new { message = "Debe indicar entidad y codigo de entidad." });
        }

        await using var stream = file.OpenReadStream();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);

        try
        {
            return Results.Ok(await documentStorageService.SaveDocumentAsync(new DocumentStorageSaveRequest(
                form["module"].FirstOrDefault() ?? "Documents",
                entityType,
                entityId,
                file.FileName,
                string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                memory.ToArray(),
                GetActorId(user),
                ParseEnumOrDefault<DocumentStoragePurpose>(form["purpose"].FirstOrDefault(), out var purpose) ? purpose : DocumentStoragePurpose.Document,
                form["faenaCodigo"].FirstOrDefault(),
                form["activoCodigo"].FirstOrDefault(),
                form["otNumero"].FirstOrDefault(),
                ToMetadata(form)), cancellationToken));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    })
    .DisableAntiforgery()
    .WithName("UploadSharePointFile");

var technicalHierarchyApi = api.MapGroup("/technical-hierarchy")
    .RequireAuthorization();

technicalHierarchyApi.MapGet("/nodes", async (
        string? faenaCodigo,
        string? familia,
        string? sistemaCodigo,
        TechnicalHierarchyLevel? nivel,
        bool? includeObsolete,
        ClaimsPrincipal user,
        ITechnicalHierarchyService technicalHierarchyService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await technicalHierarchyService.ListAsync(
                new TechnicalHierarchyQuery(faenaCodigo, familia, sistemaCodigo, nivel, includeObsolete ?? false),
                UserAccessContext.FromClaims(user),
                cancellationToken));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("ListTechnicalHierarchyNodes");

technicalHierarchyApi.MapGet("/tree", async (
        string? faenaCodigo,
        string? familia,
        string? sistemaCodigo,
        TechnicalHierarchyLevel? nivel,
        bool? includeObsolete,
        ClaimsPrincipal user,
        ITechnicalHierarchyService technicalHierarchyService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await technicalHierarchyService.GetTreeAsync(
                new TechnicalHierarchyQuery(faenaCodigo, familia, sistemaCodigo, nivel, includeObsolete ?? false),
                UserAccessContext.FromClaims(user),
                cancellationToken));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("GetTechnicalHierarchyTree");

technicalHierarchyApi.MapGet("/nodes/{code}", async (
        string code,
        ClaimsPrincipal user,
        ITechnicalHierarchyService technicalHierarchyService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await technicalHierarchyService.GetByCodeAsync(code, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("GetTechnicalHierarchyNode");

technicalHierarchyApi.MapPost("/nodes", async (
        CreateTechnicalNodeRequest request,
        ClaimsPrincipal user,
        ITechnicalHierarchyService technicalHierarchyService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var created = await technicalHierarchyService.CreateAsync(request, UserAccessContext.FromClaims(user), cancellationToken);
            return Results.Created($"/api/technical-hierarchy/nodes/{created.Codigo}", created);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("CreateTechnicalHierarchyNode");

technicalHierarchyApi.MapPut("/nodes/{code}", async (
        string code,
        UpdateTechnicalNodeRequest request,
        ClaimsPrincipal user,
        ITechnicalHierarchyService technicalHierarchyService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await technicalHierarchyService.UpdateAsync(code, request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("UpdateTechnicalHierarchyNode");

technicalHierarchyApi.MapDelete("/nodes/{code}", async (
        string code,
        string? reason,
        ClaimsPrincipal user,
        ITechnicalHierarchyService technicalHierarchyService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await technicalHierarchyService.MarkObsoleteAsync(
                code,
                new MarkTechnicalNodeObsoleteRequest(reason),
                UserAccessContext.FromClaims(user),
                cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("DeleteTechnicalHierarchyNodeAsObsolete");

technicalHierarchyApi.MapPost("/nodes/{code}/obsolete", async (
        string code,
        MarkTechnicalNodeObsoleteRequest request,
        ClaimsPrincipal user,
        ITechnicalHierarchyService technicalHierarchyService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await technicalHierarchyService.MarkObsoleteAsync(code, request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("MarkTechnicalHierarchyNodeObsolete");

technicalHierarchyApi.MapGet("/duplicates", async (
        string? faenaCodigo,
        string? familia,
        string? sistemaCodigo,
        TechnicalHierarchyLevel? nivel,
        ClaimsPrincipal user,
        ITechnicalHierarchyService technicalHierarchyService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await technicalHierarchyService.DetectSimilarAsync(
                new TechnicalHierarchyQuery(faenaCodigo, familia, sistemaCodigo, nivel),
                UserAccessContext.FromClaims(user),
                cancellationToken));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("DetectTechnicalHierarchyDuplicates");

technicalHierarchyApi.MapPost("/merge", async (
        MergeTechnicalNodesRequest request,
        ClaimsPrincipal user,
        ITechnicalHierarchyService technicalHierarchyService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await technicalHierarchyService.MergeAsync(request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("MergeTechnicalHierarchyNodes");

technicalHierarchyApi.MapPost("/families", async (
        BulkFamilyAssignmentRequest request,
        ClaimsPrincipal user,
        ITechnicalHierarchyService technicalHierarchyService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await technicalHierarchyService.AssignFamiliesAsync(
                request,
                UserAccessContext.FromClaims(user),
                cancellationToken));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("AssignTechnicalHierarchyFamilies");

technicalHierarchyApi.MapPost("/nodes/{code}/assets", async (
        string code,
        AssetAssignmentRequest request,
        ClaimsPrincipal user,
        ITechnicalHierarchyService technicalHierarchyService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await technicalHierarchyService.AssignAssetsAsync(code, request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("AssignTechnicalHierarchyAssets");

technicalHierarchyApi.MapPost("/import-excel", async (
        TechnicalHierarchyExcelImportRequest request,
        ClaimsPrincipal user,
        ITechnicalHierarchyExcelImportService importService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await importService.ImportAsync(
                new TechnicalHierarchyExcelImportCommand(
                    request.SistemasComponentesPath,
                    request.UbicacionesTecnicasPath,
                    GetActorId(user)),
                UserAccessContext.FromClaims(user),
                cancellationToken));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("ImportTechnicalHierarchyFromExcel");
var alertsApi = api.MapGroup("/alerts")
    .RequireAuthorization();

alertsApi.MapGet("", async (
        AlertStatus? status,
        AlertSeverityLevel? severity,
        string? source,
        string? faenaCodigo,
        bool? includeResolved,
        ClaimsPrincipal user,
        IAlertService alertService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await alertService.ListAsync(
                new AlertQuery(status, severity, source, faenaCodigo, includeResolved ?? false),
                UserAccessContext.FromClaims(user),
                cancellationToken));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("ListAlerts");

alertsApi.MapGet("/rules", async (
        ClaimsPrincipal user,
        IAlertService alertService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await alertService.ListRulesAsync(UserAccessContext.FromClaims(user), cancellationToken));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("ListAlertRules");

alertsApi.MapPut("/rules/{code}", async (
        string code,
        UpdateAlertRuleRequest request,
        ClaimsPrincipal user,
        IAlertService alertService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await alertService.UpdateRuleAsync(code, request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("UpdateAlertRule");

alertsApi.MapPost("/{id}/acknowledge", async (
        string id,
        ClaimsPrincipal user,
        IAlertService alertService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await alertService.AcknowledgeAsync(id, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("AcknowledgeAlert");

alertsApi.MapPost("/{id}/resolve", async (
        string id,
        ResolveAlertRequest request,
        ClaimsPrincipal user,
        IAlertService alertService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await alertService.ResolveAsync(id, request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("ResolveAlert");

alertsApi.MapPost("/{id}/send-test", async (
        string id,
        SendTestNotificationRequest request,
        ClaimsPrincipal user,
        IAlertService alertService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await alertService.SendTestAsync(id, request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("SendTestAlertNotification");

api.MapGet("/notifications", async (
        ClaimsPrincipal user,
        IAlertService alertService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await alertService.ListNotificationsAsync(UserAccessContext.FromClaims(user), cancellationToken));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .RequireAuthorization()
    .WithName("ListNotifications");

var pdfApi = api.MapGroup("/pdf/templates")
    .RequireAuthorization();

pdfApi.MapGet("", async (IPdfTemplateService templateService, CancellationToken cancellationToken) =>
        Results.Ok(await templateService.ListAsync(cancellationToken)))
    .WithName("ListPdfTemplates");

pdfApi.MapPut("/{id}", async (
        string id,
        UpdatePdfTemplateRequest request,
        ClaimsPrincipal user,
        IPdfTemplateService templateService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await templateService.UpdateAsync(id, request, UserAccessContext.FromClaims(user), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    })
    .WithName("UpdatePdfTemplate");

pdfApi.MapPost("/{id}/preview", async (
        string id,
        IReadOnlyDictionary<string, string?> data,
        IPdfTemplateService templateService,
        CancellationToken cancellationToken) =>
    {
        var result = await templateService.PreviewAsync(new PdfPreviewRequest(id, data), cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    })
    .WithName("PreviewPdfTemplate");

var importsApi = api.MapGroup("/imports")
    .RequireAuthorization("Importaciones");

importsApi.MapPost("/alerts", async (
        AlertsExcelImportRequest request,
        IAlertsExcelImportService importService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await importService.ImportAsync(request, cancellationToken));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    })
    .WithName("ImportAlertsFromExcel");
importsApi.MapPost("/sharepoint-files", async (
        FileMetadataExcelImportRequest request,
        IFileMetadataExcelImportService importService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await importService.ImportAsync(request, cancellationToken));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    })
    .WithName("ImportSharePointFileMetadata");
importsApi.MapPost("/upload", async (
        HttpRequest request,
        ClaimsPrincipal user,
        IExcelImportWorkflowService importWorkflowService,
        CancellationToken cancellationToken) =>
    {
        if (!request.HasFormContentType)
        {
            return Results.BadRequest(new { message = "La carga debe enviarse como multipart/form-data." });
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file");
        var entity = form["entity"].FirstOrDefault();
        var simulateOnly = bool.TryParse(form["simulateOnly"].FirstOrDefault(), out var parsedSimulation) && parsedSimulation;

        if (file is null || file.Length == 0)
        {
            return Results.BadRequest(new { message = "Debe adjuntar un archivo Excel." });
        }

        if (string.IsNullOrWhiteSpace(entity))
        {
            return Results.BadRequest(new { message = "Debe indicar la entidad a importar." });
        }

        await using var stream = file.OpenReadStream();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);

        try
        {
            var result = await importWorkflowService.UploadAsync(new ExcelImportUploadCommand(
                entity,
                file.FileName,
                memory.ToArray(),
                GetActorId(user),
                simulateOnly), cancellationToken);

            return Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    })
    .DisableAntiforgery()
    .WithName("UploadImport");

importsApi.MapGet("/", async (IExcelImportWorkflowService importWorkflowService, CancellationToken cancellationToken) =>
        Results.Ok(await importWorkflowService.ListAsync(cancellationToken)))
    .WithName("ListImports");

importsApi.MapGet("/{id}/preview", async (
        string id,
        IExcelImportWorkflowService importWorkflowService,
        CancellationToken cancellationToken) =>
    {
        var result = await importWorkflowService.GetPreviewAsync(id, cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    })
    .WithName("GetImportPreview");

importsApi.MapPost("/{id}/approve", async (
        string id,
        ClaimsPrincipal user,
        IExcelImportWorkflowService importWorkflowService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var preview = await importWorkflowService.GetPreviewAsync(id, cancellationToken);
            if (preview is null) return Results.NotFound();
            if (RequiresFaenaDeactivationPermission(preview) &&
                !user.IsInRole(AuthRoles.Admin) &&
                !user.HasClaim("permission", AuthPermissions.DeactivateFaenas))
            {
                return Results.Forbid();
            }

            var result = await importWorkflowService.ApproveAsync(id, GetActorId(user), cancellationToken);
            return Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    })
    .WithName("ApproveImport");

importsApi.MapPost("/{id}/reject", async (
        string id,
        RejectImportRequest request,
        ClaimsPrincipal user,
        IExcelImportWorkflowService importWorkflowService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await importWorkflowService.RejectAsync(id, GetActorId(user), request.Reason, cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    })
    .WithName("RejectImport");

importsApi.MapGet("/templates/{entity}", async (
        string entity,
        IExcelImportWorkflowService importWorkflowService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var template = await importWorkflowService.CreateTemplateAsync(entity, cancellationToken);
            return Results.File(template.Content, template.ContentType, template.FileName);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    })
    .WithName("GetImportTemplate");

api.MapGet("/audit", async (
        string? userId,
        string? module,
        string? entityName,
        string? action,
        string? faenaCodigo,
        string? severity,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int? skip,
        int? take,
        IAuditService auditService,
        CancellationToken cancellationToken) =>
    {
        var parsedSeverity = Enum.TryParse<AuditSeverity>(severity, ignoreCase: true, out var auditSeverity)
            ? auditSeverity
            : (AuditSeverity?)null;

        var result = await auditService.QueryAsync(new AuditQuery(
            userId,
            module,
            entityName,
            action,
            faenaCodigo,
            parsedSeverity,
            fromUtc,
            toUtc,
            skip ?? 0,
            take ?? 200), cancellationToken);

        return Results.Ok(result);
    })
    .RequireAuthorization("Administracion")
    .WithName("GetAuditLog");

api.MapGet("/health", async (HealthCheckService healthCheckService, CancellationToken cancellationToken) =>
    {
        var report = await healthCheckService.CheckHealthAsync(cancellationToken);

        var response = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration,
            checkedAtUtc = DateTimeOffset.UtcNow
        };

        return report.Status == HealthStatus.Healthy
            ? Results.Ok(response)
            : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    })
    .WithName("GetHealth");

api.MapGet("/system/info", (
        ISystemInfoService systemInfoService,
        IConfiguration configuration,
        IWebHostEnvironment environment) =>
    {
        var dataProvider = "PostgreSql";

        return Results.Ok(systemInfoService.GetInfo(dataProvider, environment.EnvironmentName));
    })
    .WithName("GetSystemInfo");

api.MapGet("/system/data-provider", (DataProviderSettings settings) =>
    Results.Ok(new
    {
        activeProvider = "PostgreSql",
        providerType = DataProviderType.PostgreSql.ToString(),
        postgreSqlConfigured = !string.IsNullOrWhiteSpace(settings.PostgreSqlConnectionString),
        legacyExcelRuntimeEnabled = false
    }))
    .WithName("GetDataProviderInfo");

api.MapGet("/system/database-health", async (CmmsDbContext dbContext, CancellationToken cancellationToken) =>
    {
        var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
        var pendingMigrations = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();
        var appliedMigrations = (await dbContext.Database.GetAppliedMigrationsAsync(cancellationToken)).ToArray();
        return Results.Ok(new
        {
            activeProvider = "PostgreSql",
            postgreSqlOfficial = true,
            healthy = canConnect && pendingMigrations.Length == 0,
            canConnect,
            appliedMigrations,
            pendingMigrations,
            checkedAtUtc = DateTimeOffset.UtcNow
        });
    })
    .WithName("GetDatabaseHealth");

api.MapGet("/system/import-schemas", (IExcelSchemaRegistry schemaRegistry) =>
    {
        return Results.Ok(schemaRegistry.GetAll());
    })
    .WithName("GetImportSchemas");

var costsApi = api.MapGroup("/costs").RequireAuthorization();
costsApi.MapGet("/", async (DateTimeOffset? desde, DateTimeOffset? hasta, string? otNumero, string? activoCodigo, string? faenaCodigo, string? contratoCodigo, string? proveedorRut, CostCategory? categoria, ClaimsPrincipal user, ICostManagementService service, CancellationToken ct) =>
{
    try { return Results.Ok(await service.ListAsync(new CostQuery(desde,hasta,otNumero,activoCodigo,faenaCodigo,contratoCodigo,proveedorRut,categoria), UserAccessContext.FromClaims(user), ct)); }
    catch (UnauthorizedAccessException ex) { return Results.Problem(ex.Message,statusCode:403); }
});
costsApi.MapPost("/", async (CreateCostRequest request, ClaimsPrincipal user, ICostManagementService service, CancellationToken ct) =>
{
    try { var result=await service.CreateAsync(request,UserAccessContext.FromClaims(user),ct); return Results.Created($"/api/costs/{result.Numero}",result); }
    catch (DomainException ex) { return Results.BadRequest(new { message=ex.Message }); } catch(UnauthorizedAccessException ex){return Results.Problem(ex.Message,statusCode:403);}
});
costsApi.MapPut("/{number}", async (string number, UpdateCostRequest request, ClaimsPrincipal user, ICostManagementService service, CancellationToken ct) =>
{ try { var result=await service.UpdateAsync(number,request,UserAccessContext.FromClaims(user),ct);return result is null?Results.NotFound():Results.Ok(result); } catch(DomainException ex){return Results.BadRequest(new{message=ex.Message});}catch(UnauthorizedAccessException ex){return Results.Problem(ex.Message,statusCode:403);} });
costsApi.MapGet("/dashboard", async (DateTimeOffset? desde,DateTimeOffset? hasta,string? faenaCodigo,ClaimsPrincipal user,ICostManagementService service,CancellationToken ct)=> {try{return Results.Ok(await service.DashboardAsync(new CostQuery(desde,hasta,FaenaCodigo:faenaCodigo),UserAccessContext.FromClaims(user),ct));}catch(UnauthorizedAccessException ex){return Results.Problem(ex.Message,statusCode:403);}});
costsApi.MapGet("/work-orders/{number}",async(string number,ClaimsPrincipal user,ICostManagementService service,CancellationToken ct)=>{try{return Results.Ok(await service.GetWorkOrderCostsAsync(number,UserAccessContext.FromClaims(user),ct));}catch(UnauthorizedAccessException ex){return Results.Problem(ex.Message,statusCode:403);}});
costsApi.MapGet("/labor-rates",async(ClaimsPrincipal user,ICostManagementService service,CancellationToken ct)=>{try{return Results.Ok(await service.ListRatesAsync(UserAccessContext.FromClaims(user),ct));}catch(UnauthorizedAccessException ex){return Results.Problem(ex.Message,statusCode:403);}});
costsApi.MapPut("/labor-rates",async(UpsertLaborRateRequest request,ClaimsPrincipal user,ICostManagementService service,CancellationToken ct)=>{try{return Results.Ok(await service.UpsertRateAsync(request,UserAccessContext.FromClaims(user),ct));}catch(DomainException ex){return Results.BadRequest(new{message=ex.Message});}catch(UnauthorizedAccessException ex){return Results.Problem(ex.Message,statusCode:403);}});
costsApi.MapGet("/payment-statements",async(ClaimsPrincipal user,ICostManagementService service,CancellationToken ct)=>{try{return Results.Ok(await service.ListPaymentsAsync(UserAccessContext.FromClaims(user),ct));}catch(UnauthorizedAccessException ex){return Results.Problem(ex.Message,statusCode:403);}});
costsApi.MapPost("/payment-statements",async(CreatePaymentStatementRequest request,ClaimsPrincipal user,ICostManagementService service,CancellationToken ct)=>{try{return Results.Ok(await service.CreatePaymentAsync(request,UserAccessContext.FromClaims(user),ct));}catch(DomainException ex){return Results.BadRequest(new{message=ex.Message});}catch(UnauthorizedAccessException ex){return Results.Problem(ex.Message,statusCode:403);}});
costsApi.MapPost("/payment-statements/{number}/status",async(string number,ChangePaymentStatusRequest request,ClaimsPrincipal user,ICostManagementService service,CancellationToken ct)=>{try{var result=await service.ChangePaymentStatusAsync(number,request,UserAccessContext.FromClaims(user),ct);return result is null?Results.NotFound():Results.Ok(result);}catch(DomainException ex){return Results.BadRequest(new{message=ex.Message});}catch(UnauthorizedAccessException ex){return Results.Problem(ex.Message,statusCode:403);}});
await app.RunAsync();
return 0;

static string DescribeDatabaseTarget(string? connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return "connection string not configured";
    }

    try
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var host = string.IsNullOrWhiteSpace(builder.Host) ? "(default host)" : builder.Host;
        var database = string.IsNullOrWhiteSpace(builder.Database) ? "(default database)" : builder.Database;
        var username = string.IsNullOrWhiteSpace(builder.Username) ? "(default user)" : builder.Username;

        return $"{host}:{builder.Port}/{database} as {username}";
    }
    catch (ArgumentException)
    {
        return "invalid connection string (redacted)";
    }
}

static PostgresException? FindPostgresException(Exception exception)
{
    for (Exception? current = exception; current is not null; current = current.InnerException)
    {
        if (current is PostgresException postgresException)
        {
            return postgresException;
        }

        if (current is AggregateException aggregateException)
        {
            foreach (var innerException in aggregateException.Flatten().InnerExceptions)
            {
                if (innerException is PostgresException aggregatePostgresException)
                {
                    return aggregatePostgresException;
                }
            }
        }
    }

    return null;
}

static bool IsLegacyOperationalDataSetBlock(PostgresException? postgresException, Exception exception)
{
    var message = postgresException?.MessageText ?? exception.Message;
    return postgresException?.SqlState == "P0001"
        && message.Contains("conjuntos_datos_operacionales", StringComparison.OrdinalIgnoreCase);
}

static string GetActorId(ClaimsPrincipal user)
{
    return user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "system";
}

static bool RequiresFaenaDeactivationPermission(ExcelImportPreviewResult preview)
{
    return string.Equals(preview.Import.SchemaName, "faenas", StringComparison.OrdinalIgnoreCase) &&
           preview.Rows.Any(row => IsExplicitInactiveFaenaState(row.Values));
}

static bool IsExplicitInactiveFaenaState(IReadOnlyDictionary<string, string?> values)
{
    if (!values.TryGetValue("Estado", out var raw) || string.IsNullOrWhiteSpace(raw)) return false;

    return raw.Trim().ToUpperInvariant() switch
    {
        "NO" or "INACTIVO" or "INACTIVA" or "FALSE" or "0" => true,
        _ => false
    };
}

static bool ParseEnumOrDefault<TEnum>(string? value, out TEnum parsed)
    where TEnum : struct, Enum
{
    return Enum.TryParse(value, ignoreCase: true, out parsed);
}

static IReadOnlyDictionary<string, string?> ToMetadata(IFormCollection form)
{
    return form
        .Where(item => item.Key.StartsWith("metadata.", StringComparison.OrdinalIgnoreCase))
        .ToDictionary(
            item => item.Key["metadata.".Length..],
            item => (string?)item.Value.FirstOrDefault(),
            StringComparer.OrdinalIgnoreCase);
}

public sealed record RejectImportRequest(string? Reason);

public sealed record SharePointFolderApiRequest(
    string Module,
    string EntityType,
    string EntityId,
    DocumentStoragePurpose Purpose = DocumentStoragePurpose.Document,
    string? FaenaCodigo = null,
    string? ActivoCodigo = null,
    string? OtNumero = null);

public sealed record SharePointManualLinkApiRequest(
    string Module,
    string EntityType,
    string EntityId,
    string FileName,
    string Url,
    DocumentStoragePurpose Purpose = DocumentStoragePurpose.Document,
    string? FaenaCodigo = null,
    string? ActivoCodigo = null,
    string? OtNumero = null,
    IReadOnlyDictionary<string, string?>? Metadata = null);

public partial class Program
{
}
