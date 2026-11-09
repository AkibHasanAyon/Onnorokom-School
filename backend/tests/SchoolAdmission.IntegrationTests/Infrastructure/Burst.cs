using System.Net;

namespace SchoolAdmission.IntegrationTests.Infrastructure;

/// <summary>Outcome of one request in a burst.</summary>
public sealed record BurstResult(int Index, HttpStatusCode Status, string? Code, Guid? HoldId);

public static class Burst
{
    /// <summary>
    /// Starts <paramref name="count"/> requests, holds them on a gate, then releases them all at the same moment
    /// so they really race inside the API and the database.
    /// </summary>
    public static async Task<BurstResult[]> RunAsync(int count, Func<int, Task<HttpResponseMessage>> send)
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(0, count).Select(async i =>
        {
            await gate.Task;
            using var res = await send(i);
            var body = await res.Content.ReadAsStringAsync();
            if (body.Length == 0) return new BurstResult(i, res.StatusCode, null, null); // e.g. 204 from cancel

            var json = System.Text.Json.JsonDocument.Parse(body).RootElement;
            string? code = json.TryGetProperty("code", out var c) ? c.GetString() : null;
            Guid? holdId = json.TryGetProperty("holdId", out var h) ? Guid.Parse(h.GetString()!) : null;
            return new BurstResult(i, res.StatusCode, code, holdId);
        }).ToArray();

        gate.SetResult();
        return await Task.WhenAll(tasks);
    }

    /// <summary>R7 under load: no request in a burst may ever produce a 5xx.</summary>
    public static void AssertNoServerErrors(this IEnumerable<BurstResult> results)
    {
        var errors = results.Where(r => (int)r.Status >= 500).ToList();
        Assert.True(errors.Count == 0, $"{errors.Count} request(s) failed with 5xx: " +
            string.Join(", ", errors.Take(5).Select(e => $"{(int)e.Status} {e.Code}")));
    }

    public static int CountOf(this IEnumerable<BurstResult> results, HttpStatusCode status, string? code = null) =>
        results.Count(r => r.Status == status && (code is null || r.Code == code));
}
