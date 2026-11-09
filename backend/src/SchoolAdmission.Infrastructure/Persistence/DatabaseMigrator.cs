using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace SchoolAdmission.Infrastructure.Persistence;

/// <summary>
/// Applies EF Core migrations at startup. Waits for PostgreSQL to accept connections, then holds a
/// PostgreSQL advisory lock while migrating so several backend instances can start at the same time.
/// </summary>
public static class DatabaseMigrator
{
    private const long AdvisoryLockKey = 7_272_026;
    private const int MaxConnectAttempts = 15;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AdmissionDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseMigrator).FullName!);
        var connectionString = db.Database.GetConnectionString()
            ?? throw new InvalidOperationException("ConnectionStrings__Default is not set.");

        // Dedicated connection: the advisory lock belongs to this session until unlocked or closed.
        await using var lockConnection = await OpenWithRetryAsync(connectionString, logger, ct);
        await ExecuteAsync(lockConnection, $"SELECT pg_advisory_lock({AdvisoryLockKey})", ct);
        try
        {
            // On a brand-new database EF reads __EFMigrationsHistory before creating it and logs a
            // misleading "Failed executing DbCommand" error; creating the table first avoids that.
            await db.GetService<IHistoryRepository>().CreateIfNotExistsAsync(ct);
            logger.LogInformation("Applying database migrations (if any)");
            await db.Database.MigrateAsync(ct);
            logger.LogInformation("Database schema is up to date");
        }
        finally
        {
            await ExecuteAsync(lockConnection, $"SELECT pg_advisory_unlock({AdvisoryLockKey})", CancellationToken.None);
        }
    }

    private static async Task<NpgsqlConnection> OpenWithRetryAsync(string connectionString, ILogger logger, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            var connection = new NpgsqlConnection(connectionString);
            try
            {
                await connection.OpenAsync(ct);
                return connection;
            }
            catch (NpgsqlException ex) when (attempt < MaxConnectAttempts)
            {
                await connection.DisposeAsync();
                logger.LogWarning("Database not reachable (attempt {Attempt}/{Max}): {Reason}. Retrying in {Delay}s",
                    attempt, MaxConnectAttempts, ex.Message, RetryDelay.TotalSeconds);
                await Task.Delay(RetryDelay, ct);
            }
            catch
            {
                await connection.DisposeAsync();
                throw;
            }
        }
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(ct);
    }
}
