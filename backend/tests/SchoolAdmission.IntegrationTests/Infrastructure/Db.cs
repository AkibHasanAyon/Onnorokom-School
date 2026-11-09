using Npgsql;

namespace SchoolAdmission.IntegrationTests.Infrastructure;

/// <summary>Direct database checks of the rules that must hold no matter what the API returned.</summary>
public static class Db
{
    // Database.md §10. Evaluated at the app's (fake) clock time, not the DB server's now().
    private const string SeatInvariantSql = """
        SELECT c.name || ': ' || COUNT(*) FILTER (WHERE h.status = 'HELD' AND h.expires_at > @at)
                      || ' held + ' || COUNT(*) FILTER (WHERE h.status = 'CONFIRMED')
                      || ' confirmed > ' || c.total_seats || ' seats'
          FROM classes c LEFT JOIN holds h ON h.class_id = c.id
         GROUP BY c.id
        HAVING COUNT(*) FILTER (WHERE h.status = 'HELD' AND h.expires_at > @at)
             + COUNT(*) FILTER (WHERE h.status = 'CONFIRMED') > c.total_seats
        """;

    private const string OneActivePerStudentSql = """
        SELECT class_id || ' / ' || student_id || ': ' || COUNT(*) || ' active-or-confirmed holds'
          FROM holds
         WHERE status = 'CONFIRMED' OR (status = 'HELD' AND expires_at > @at)
         GROUP BY class_id, student_id
        HAVING COUNT(*) > 1
        """;

    /// <summary>
    /// R1: for every class held + confirmed ≤ totalSeats. R3: at most one active-or-confirmed hold per student per
    /// class. <paramref name="at"/> defaults to the real current time; pass the factory's FakeTimeProvider time in expiry tests.
    /// </summary>
    public static async Task AssertInvariantAsync(string connectionString, DateTimeOffset? at = null)
    {
        var when = at ?? DateTimeOffset.UtcNow;
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var oversold = await QueryAsync(connection, SeatInvariantSql, when);
        Assert.True(oversold.Count == 0, "Seat invariant violated (R1): " + string.Join("; ", oversold));

        var duplicates = await QueryAsync(connection, OneActivePerStudentSql, when);
        Assert.True(duplicates.Count == 0, "One-active-hold rule violated (R3): " + string.Join("; ", duplicates));
    }

    private static async Task<List<string>> QueryAsync(NpgsqlConnection connection, string sql, DateTimeOffset at)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("at", at.ToUniversalTime());
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<string>();
        while (await reader.ReadAsync()) rows.Add(reader.GetString(0));
        return rows;
    }
}
