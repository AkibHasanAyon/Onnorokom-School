using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace SchoolAdmission.IntegrationTests.Infrastructure;

/// <summary>
/// The real API (Program) on a given test database, with a controllable clock.
/// The background sweeper is off by default so tests decide exactly when stored statuses change.
/// </summary>
public sealed class AdmissionApiFactory(
    string connectionString,
    int holdSeconds = 600,
    bool sweeperEnabled = false,
    int sweepIntervalSeconds = 5) : WebApplicationFactory<Program>
{
    public FakeTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", connectionString);
        builder.UseSetting("HOLD_DURATION_SECONDS", holdSeconds.ToString(CultureInfo.InvariantCulture));
        builder.UseSetting("HOLD_SWEEPER_ENABLED", sweeperEnabled ? "true" : "false");
        builder.UseSetting("HOLD_SWEEP_INTERVAL_SECONDS", sweepIntervalSeconds.ToString(CultureInfo.InvariantCulture));
        builder.ConfigureTestServices(s =>
        {
            s.RemoveAll<TimeProvider>();
            s.AddSingleton<TimeProvider>(Clock);
        });
    }
}
