using Npgsql;

namespace SchoolAdmission.IntegrationTests.Infrastructure;

/// <summary>
/// A throwaway PostgreSQL database (<c>test_&lt;guid&gt;</c>) per test class, created on the server named by
/// <c>TEST_DB_CONNECTION</c> and dropped (WITH FORCE) afterwards. Never EF InMemory/SQLite: holds need FOR UPDATE.
/// </summary>
public sealed class PostgresDatabase : IAsyncLifetime
{
    public static string AdminConnectionString { get; } =
        Environment.GetEnvironmentVariable("TEST_DB_CONNECTION")
        ?? "Host=localhost;Port=5433;Database=postgres;Username=admission;Password=admission";

    public PostgresDatabase()
    {
        Name = $"test_{Guid.NewGuid():N}";
        ConnectionString = new NpgsqlConnectionStringBuilder(AdminConnectionString) { Database = Name }.ConnectionString;
    }

    public string Name { get; }
    public string ConnectionString { get; }

    public async Task InitializeAsync() => await AdminExecuteAsync($"CREATE DATABASE \"{Name}\"");

    public async Task DisposeAsync() => await DropAsync();

    /// <summary>Drops the database, terminating open connections (also used to simulate an outage).</summary>
    public async Task DropAsync()
    {
        await using (var c = new NpgsqlConnection(ConnectionString)) NpgsqlConnection.ClearPool(c);
        await AdminExecuteAsync($"DROP DATABASE IF EXISTS \"{Name}\" WITH (FORCE)");
    }

    public async Task<T?> ScalarAsync<T>(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        var result = await command.ExecuteScalarAsync();
        return result is null or DBNull ? default : (T)result;
    }

    private static async Task AdminExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(AdminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
