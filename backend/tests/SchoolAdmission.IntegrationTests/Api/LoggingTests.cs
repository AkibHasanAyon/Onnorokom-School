using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using SchoolAdmission.IntegrationTests.Infrastructure;

namespace SchoolAdmission.IntegrationTests.Api;

/// <summary>A host whose every log entry (Debug and up, all categories) goes into <see cref="Logs"/>.</summary>
public sealed class LoggingFixture : ApiFixture
{
    public LogCapture Logs { get; } = new();

    protected override WebApplicationFactory<Program> Customize(AdmissionApiFactory factory) =>
        factory.WithWebHostBuilder(b => b.ConfigureLogging(l =>
        {
            l.AddProvider(Logs);
            l.AddFilter<LogCapture>(null, LogLevel.Trace); // beats the appsettings category levels for this provider
        }));
}

/// <summary>Student ids are personal data: logs show only the last 4 digits; requests are logged without headers/bodies.</summary>
public sealed class LoggingTests(LoggingFixture api) : IClassFixture<LoggingFixture>
{
    private const string Alice = "20152691234561111";
    private const string Bob = "20152691234562222";

    private async Task RunTypicalSessionAsync()
    {
        var c = api.Client;
        var classId = await c.CreateClassIdAsync(TestData.ClassName("Logged"), 1);
        var holdId = await c.CreateHoldIdAsync(classId, Alice);
        (await c.HoldAsync(classId, Alice)).Dispose();            // 409 ACTIVE_HOLD_EXISTS
        (await c.HoldAsync(classId, Bob)).Dispose();              // 409 NO_SEATS_AVAILABLE
        (await c.ConfirmAsync(holdId, Bob)).Dispose();            // 403 HOLD_NOT_OWNED
        (await c.MyHoldsAsync(Alice)).Dispose();
        (await c.MyHoldsAsync(Bob + "9")).Dispose();             // 18 digits → 400
        (await c.ConfirmAsync(holdId, Alice)).Dispose();
        (await c.CancelAsync(holdId, Alice)).Dispose();           // 409 HOLD_ALREADY_CONFIRMED
        (await c.GetAsync($"/holds/{holdId}")).Dispose();
    }

    [Fact]
    public async Task FullStudentIds_NeverAppearInAnyLogEntry()
    {
        await RunTypicalSessionAsync();

        Assert.NotEmpty(api.Logs.Entries);
        var leaks = api.Logs.Entries
            .Where(e => e.Contains(Alice) || e.Contains(Bob) || e.Contains("X-Student-Id: ")) // headers are never logged
            .ToList();
        Assert.True(leaks.Count == 0, "Full student id found in logs:\n" + string.Join("\n", leaks.Take(3)));
    }

    [Fact]
    public async Task UseCaseLogs_ShowTheMaskedStudentId_AndTheOutcome()
    {
        await RunTypicalSessionAsync();

        Assert.Contains(api.Logs.Entries, e => e.Contains("CreateHoldCommand for student *************2222 rejected with NO_SEATS_AVAILABLE"));
        Assert.Contains(api.Logs.Entries, e => e.Contains("ConfirmHoldCommand for student *************2222 rejected with HOLD_NOT_OWNED"));
        Assert.Contains(api.Logs.Entries, e => e.Contains("GetMyHoldsQuery for student (invalid) rejected with INVALID_STUDENT_ID"));
        Assert.Contains(api.Logs.Entries, e => e.Contains("ConfirmHoldCommand for student *************1111 succeeded"));
    }

    [Fact]
    public async Task EveryRequest_IsLoggedWithMethodPathStatusAndDuration()
    {
        await RunTypicalSessionAsync();

        var http = api.Logs.Entries.Where(e => e.Contains("HttpLogging")).ToList();
        Assert.Contains(http, e => e.Contains("Method: POST") && e.Contains("Path: /classes") && e.Contains("StatusCode: 201"));
        Assert.Contains(http, e => e.Contains("Path: /me/holds") && e.Contains("StatusCode: 400"));
        Assert.Contains(http, e => e.Contains("StatusCode: 403"));
        Assert.All(http, e => Assert.Contains("Duration:", e));
    }
}
