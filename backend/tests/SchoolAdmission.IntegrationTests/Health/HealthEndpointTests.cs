using System.Net;
using System.Text.Json;
using SchoolAdmission.IntegrationTests.Infrastructure;

namespace SchoolAdmission.IntegrationTests.Health;

public sealed class HealthEndpointTests : IAsyncLifetime
{
    private readonly PostgresDatabase _db = new();
    private AdmissionApiFactory _factory = default!;

    public async Task InitializeAsync()
    {
        await _db.InitializeAsync();
        _factory = new AdmissionApiFactory(_db.ConnectionString);
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task Health_WithDatabase_Returns200_WithServerTimeInWholeUtcSeconds()
    {
        // FakeTimeProvider only moves forward and starts at "now", so use a fixed future instant.
        _factory.Clock.SetUtcNow(new DateTimeOffset(2099, 11, 1, 4, 3, 12, 789, TimeSpan.Zero));
        using var client = _factory.CreateClient();

        using var res = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        using var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.Equal("ok", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("2099-11-01T04:03:12Z", json.RootElement.GetProperty("serverTime").GetString());
    }

    [Fact]
    public async Task Health_WhenDatabaseGoesAway_Returns503Unavailable()
    {
        using var client = _factory.CreateClient();           // starts the host, runs migrations
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);

        await _db.DropAsync();                                 // simulate the database disappearing

        using var res = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, res.StatusCode);
        using var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.Equal("unavailable", json.RootElement.GetProperty("status").GetString());
        Assert.True(json.RootElement.TryGetProperty("serverTime", out _));
    }
}
