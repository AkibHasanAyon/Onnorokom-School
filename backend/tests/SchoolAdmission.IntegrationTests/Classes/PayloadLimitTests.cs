using System.Net;
using SchoolAdmission.IntegrationTests.Infrastructure;

namespace SchoolAdmission.IntegrationTests.Classes;

/// <summary>
/// The 64 KB body limit is enforced by Kestrel, which TestServer doesn't emulate, so this host runs on a real
/// Kestrel socket (<c>WebApplicationFactory.UseKestrel</c>, .NET 10).
/// </summary>
public sealed class PayloadLimitTests : IAsyncLifetime
{
    private readonly PostgresDatabase _db = new();
    private AdmissionApiFactory _factory = default!;
    private HttpClient _client = default!;

    public async Task InitializeAsync()
    {
        await _db.InitializeAsync();
        _factory = new AdmissionApiFactory(_db.ConnectionString);
        _factory.UseKestrel(0); // random free port
        _factory.StartServer();
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task BodyOver64KB_Is413PayloadTooLarge()
    {
        var hugeName = new string('a', 100_000);

        using var res = await _client.PostJsonAsync("/classes", $$"""{"name":"{{hugeName}}","totalSeats":1}""");

        await res.AssertProblemAsync(HttpStatusCode.RequestEntityTooLarge, "PAYLOAD_TOO_LARGE");
    }

    [Fact]
    public async Task BodyUnder64KB_IsProcessedNormally()
    {
        // ~60 KB of ignored padding: allowed by the limit, then the normal rules apply.
        var padding = new string('p', 60_000);

        using var res = await _client.PostJsonAsync("/classes",
            $$"""{"name":"{{TestData.ClassName("Padded")}}","totalSeats":3,"padding":"{{padding}}"}""");

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }
}
