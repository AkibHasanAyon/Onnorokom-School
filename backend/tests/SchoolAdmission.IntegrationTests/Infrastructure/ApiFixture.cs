using Microsoft.AspNetCore.Mvc.Testing;

namespace SchoolAdmission.IntegrationTests.Infrastructure;

/// <summary>
/// One throwaway database + one API host per test class (<c>IClassFixture&lt;ApiFixture&gt;</c>).
/// Test classes run in parallel safely because each has its own database; tests inside a class share
/// it, so they must use unique class names / student ids (see <see cref="TestData"/>).
/// </summary>
public class ApiFixture : IAsyncLifetime
{
    public PostgresDatabase Database { get; } = new();
    public AdmissionApiFactory Factory { get; private set; } = default!;

    /// <summary>The configured host (after <see cref="Customize"/>); use it to create extra clients or scopes.</summary>
    public WebApplicationFactory<Program> Host { get; private set; } = default!;

    public HttpClient Client { get; private set; } = default!;

    /// <summary>HOLD_DURATION_SECONDS for this host.</summary>
    protected virtual int HoldSeconds => 600;

    /// <summary>HOLD_SWEEPER_ENABLED for this host (off by default: tests trigger sweeps explicitly).</summary>
    protected virtual bool SweeperEnabled => false;

    /// <summary>HOLD_SWEEP_INTERVAL_SECONDS for this host.</summary>
    protected virtual int SweepIntervalSeconds => 5;

    /// <summary>Hook for fixtures that need extra test services (e.g. a probe controller).</summary>
    protected virtual WebApplicationFactory<Program> Customize(AdmissionApiFactory factory) => factory;

    public async Task InitializeAsync()
    {
        await Database.InitializeAsync();
        Factory = new AdmissionApiFactory(Database.ConnectionString, HoldSeconds, SweeperEnabled, SweepIntervalSeconds);
        Host = Customize(Factory);
        Client = Host.CreateClient(); // starts the host → runs migrations
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        if (!ReferenceEquals(Host, Factory)) await Host.DisposeAsync();
        await Factory.DisposeAsync();
        await Database.DisposeAsync();
    }
}
