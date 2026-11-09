using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace SchoolAdmission.IntegrationTests.Infrastructure;

/// <summary>The real API (Program) on a given test database, with a controllable clock.</summary>
public sealed class AdmissionApiFactory(string connectionString, int holdSeconds = 600) : WebApplicationFactory<Program>
{
    public FakeTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", connectionString);
        builder.UseSetting("HOLD_DURATION_SECONDS", holdSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.UseSetting("HOLD_SWEEPER_ENABLED", "false"); // tests trigger sweeps explicitly
        builder.ConfigureTestServices(s =>
        {
            s.RemoveAll<TimeProvider>();
            s.AddSingleton<TimeProvider>(Clock);
        });
    }
}
