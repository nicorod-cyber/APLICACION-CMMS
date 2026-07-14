using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MaintenanceCMMS.Infrastructure.Security;

public interface IIdentitySeedTransaction
{
    Task ExecuteAsync(
        Func<IIdentityStore, CancellationToken, Task> seedOperation,
        CancellationToken cancellationToken);
}

public sealed class PostgreSqlIdentitySeedTransaction : IIdentitySeedTransaction
{
    private const int MaxAttempts = 3;
    private const long RolePermissionSeedLockKey = 7_144_260_118_247_903_411;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PostgreSqlIdentitySeedTransaction> _logger;

    public PostgreSqlIdentitySeedTransaction(
        IServiceScopeFactory scopeFactory,
        ILogger<PostgreSqlIdentitySeedTransaction> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task ExecuteAsync(
        Func<IIdentityStore, CancellationToken, Task> seedOperation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(seedOperation);

        DbUpdateConcurrencyException? lastConflict = null;
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var dbContext = services.GetRequiredService<CmmsDbContext>();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                await dbContext.Database.ExecuteSqlRawAsync(
                    "SELECT pg_advisory_xact_lock(7144260118247903411);",
                    cancellationToken);

                var identityStore = services.GetRequiredService<IIdentityStore>();
                await seedOperation(identityStore, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return;
            }
            catch (DbUpdateConcurrencyException exception)
            {
                lastConflict = exception;
                await LogConcurrencyEntriesAsync(exception, attempt, cancellationToken);
                await RollbackAsync(transaction);

                if (await IsSeedStateCurrentAsync(cancellationToken))
                {
                    _logger.LogInformation(
                        "La sincronizacion de identidad encontro un conflicto recuperable en el intento {Attempt}; el catalogo de roles y permisos ya coincide con la matriz esperada.",
                        attempt);
                    return;
                }

                if (attempt < MaxAttempts)
                {
                    _logger.LogWarning(
                        "Se reintentara la sincronizacion de identidad con un DbContext limpio. Intento {Attempt} de {MaxAttempts}.",
                        attempt + 1,
                        MaxAttempts);
                    await Task.Delay(TimeSpan.FromMilliseconds(75 * attempt), cancellationToken);
                }
            }
        }

        throw new InvalidOperationException(
            $"No fue posible sincronizar los roles y permisos de identidad despues de {MaxAttempts} intentos por conflictos de concurrencia.",
            lastConflict);
    }

    private async Task<bool> IsSeedStateCurrentAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var identityStore = scope.ServiceProvider.GetRequiredService<IIdentityStore>();
        var roles = await identityStore.ListRolesAsync(cancellationToken);
        if (!RolePermissionCatalog.MatchesSeededRoles(roles))
        {
            return false;
        }

        var users = await identityStore.ListUsersAsync(cancellationToken);
        return users.Count > 0;
    }

    private async Task LogConcurrencyEntriesAsync(
        DbUpdateConcurrencyException exception,
        int attempt,
        CancellationToken cancellationToken)
    {
        if (exception.Entries.Count == 0)
        {
            _logger.LogWarning(
                exception,
                "Conflicto de concurrencia durante la sincronizacion de identidad. {EntityType} {EntityId} {EntityState} {OriginalVersion} {CurrentVersion} {DatabaseVersion} {Attempt}",
                null,
                null,
                null,
                null,
                null,
                null,
                attempt);
            return;
        }

        foreach (var entry in exception.Entries)
        {
            var databaseVersion = await GetDatabaseVersionAsync(entry, cancellationToken);
            var versionProperty = entry.Metadata.FindProperty("Version");
            var originalVersion = versionProperty is null ? null : entry.OriginalValues[versionProperty]?.ToString();
            var currentVersion = versionProperty is null ? null : entry.CurrentValues[versionProperty]?.ToString();
            var entityId = string.Join(",", entry.Properties
                .Where(property => property.Metadata.IsPrimaryKey())
                .Select(property => property.CurrentValue?.ToString() ?? property.OriginalValue?.ToString() ?? "<null>"));

            _logger.LogWarning(
                exception,
                "Conflicto de concurrencia durante la sincronizacion de identidad. {EntityType} {EntityId} {EntityState} {OriginalVersion} {CurrentVersion} {DatabaseVersion} {Attempt}",
                entry.Metadata.ClrType.Name,
                entityId,
                entry.State.ToString(),
                originalVersion,
                currentVersion,
                databaseVersion,
                attempt);
        }
    }

    private static async Task<string?> GetDatabaseVersionAsync(EntityEntry entry, CancellationToken cancellationToken)
    {
        try
        {
            var databaseValues = await entry.GetDatabaseValuesAsync(cancellationToken);
            var versionProperty = entry.Metadata.FindProperty("Version");
            return databaseValues is null || versionProperty is null
                ? null
                : databaseValues[versionProperty]?.ToString();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static async Task RollbackAsync(IDbContextTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            // The transaction may already have been rolled back by the provider after the failed save.
        }
    }
}