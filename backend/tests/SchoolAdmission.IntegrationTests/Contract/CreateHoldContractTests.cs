using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using SchoolAdmission.IntegrationTests.Infrastructure;

namespace SchoolAdmission.IntegrationTests.Contract;

/// <summary>Exact contract for endpoint 4, POST /classes/{classId}/holds (API.md §4.4).</summary>
public sealed partial class CreateHoldContractTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    private readonly HttpClient _client = api.Client;

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$")]
    private static partial Regex WholeSecondUtc();

    [GeneratedRegex("^[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$")]
    private static partial Regex UuidV7Lowercase();

    private static DateTimeOffset ParseUtc(string s) =>
        DateTimeOffset.ParseExact(s, "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

    [Fact]
    public async Task CreateHold_Returns201_WithLocation_AndExactFields()
    {
        var className = TestData.ClassName("Class 6 - Morning Shift");
        var classId = await _client.CreateClassIdAsync(className, 5);
        var student = TestData.StudentId(1);

        using var res = await _client.HoldAsync(classId, student);

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var json = await res.ReadJsonAsync();
        json.AssertHasProperties("holdId", "classId", "studentId", "status", "expiresAt"); // contract
        json.AssertHasProperties("className", "createdAt", "serverTime");                // extras
        var holdId = json.GetProperty("holdId").GetString()!;
        Assert.Matches(UuidV7Lowercase(), holdId);
        Assert.Equal($"/holds/{holdId}", res.Headers.Location?.OriginalString);
        Assert.Equal(classId.ToString(), json.GetProperty("classId").GetString());
        Assert.Equal(student, json.GetProperty("studentId").GetString());
        Assert.Equal("HELD", json.GetProperty("status").GetString());
        Assert.Equal(className, json.GetProperty("className").GetString());
    }

    [Fact]
    public async Task Timestamps_AreWholeSecondUtc_AndExpiresAtIsCreatedAtPlusHoldDuration()
    {
        // Give the fake clock a fractional second: createdAt must be truncated, not rounded.
        api.Factory.Clock.Advance(TimeSpan.FromMilliseconds(987));
        var expectedCreated = api.Factory.Clock.GetUtcNow();
        expectedCreated = expectedCreated.AddTicks(-(expectedCreated.Ticks % TimeSpan.TicksPerSecond));
        var classId = await _client.CreateClassIdAsync(TestData.ClassName(), 5);

        using var res = await _client.HoldAsync(classId, TestData.StudentId(2));

        var json = await res.ReadJsonAsync();
        foreach (var field in new[] { "expiresAt", "createdAt", "serverTime" })
            Assert.Matches(WholeSecondUtc(), json.GetProperty(field).GetString()!);
        var createdAt = ParseUtc(json.GetProperty("createdAt").GetString()!);
        var expiresAt = ParseUtc(json.GetProperty("expiresAt").GetString()!);
        Assert.Equal(expectedCreated, createdAt);
        Assert.Equal(TimeSpan.FromSeconds(600), expiresAt - createdAt);
        Assert.Equal(createdAt, ParseUtc(json.GetProperty("serverTime").GetString()!));
    }

    [Fact]
    public async Task StudentIdHeader_IsTrimmed()
    {
        var classId = await _client.CreateClassIdAsync(TestData.ClassName(), 5);

        using var res = await _client.HoldAsync(classId, $"  {TestData.StudentId(3)}\t");

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        Assert.Equal(TestData.StudentId(3), (await res.ReadJsonAsync()).GetProperty("studentId").GetString());
    }

    [Theory]
    [InlineData("""{"anything":"goes"}""", "application/json")]
    [InlineData("not json at all", "text/plain")]
    [InlineData("{broken", "application/json")]
    public async Task RequestBody_IsIgnored(string body, string contentType)
    {
        var classId = await _client.CreateClassIdAsync(TestData.ClassName(), 5);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/classes/{classId}/holds")
        {
            Content = new StringContent(body, Encoding.UTF8, contentType),
        };
        request.Headers.Add("X-Student-Id", TestData.StudentId(4));

        using var res = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    [Fact]
    public async Task NoSeats_Is409ProblemJson_NamingTheClass()
    {
        var className = TestData.ClassName("Tiny");
        var classId = await _client.CreateClassIdAsync(className, 1);
        await _client.CreateHoldIdAsync(classId, TestData.StudentId(5));

        using var res = await _client.HoldAsync(classId, TestData.StudentId(6));

        var json = await res.AssertProblemAsync(HttpStatusCode.Conflict, "NO_SEATS_AVAILABLE");
        Assert.Contains(className, json.GetProperty("detail").GetString());
    }
}

/// <summary>HOLD_DURATION_SECONDS is honoured (R2).</summary>
public sealed class ShortHoldFixture : ApiFixture
{
    protected override int HoldSeconds => 30;
}

public sealed class HoldDurationContractTests(ShortHoldFixture api) : IClassFixture<ShortHoldFixture>
{
    [Fact]
    public async Task ExpiresAt_UsesConfiguredHoldDuration()
    {
        var classId = await api.Client.CreateClassIdAsync(TestData.ClassName(), 3);

        using var res = await api.Client.HoldAsync(classId, TestData.StudentId(1));

        var json = await res.ReadJsonAsync();
        var created = DateTimeOffset.Parse(json.GetProperty("createdAt").GetString()!, CultureInfo.InvariantCulture);
        var expires = DateTimeOffset.Parse(json.GetProperty("expiresAt").GetString()!, CultureInfo.InvariantCulture);
        Assert.Equal(TimeSpan.FromSeconds(30), expires - created);
    }
}
