using System.Net;
using System.Text.Json;
using SchoolAdmission.IntegrationTests.Infrastructure;

namespace SchoolAdmission.IntegrationTests.Contract;

/// <summary>Exact contract for endpoints 5–8 (API.md §4.5–4.8).</summary>
public sealed class HoldLifecycleContractTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    private readonly HttpClient _client = api.Client;
    private static int _student = 10_000;
    private static string NewStudent() => TestData.StudentId(Interlocked.Increment(ref _student));

    private async Task<(Guid classId, Guid holdId, string student, string className)> NewHoldAsync()
    {
        var className = TestData.ClassName("Class 6 - Morning Shift");
        var classId = await _client.CreateClassIdAsync(className, 5);
        var student = NewStudent();
        return (classId, await _client.CreateHoldIdAsync(classId, student), student, className);
    }

    [Fact]
    public async Task GetHold_Returns200_WithExactFields()
    {
        var (classId, holdId, _, className) = await NewHoldAsync();

        using var res = await _client.GetAsync($"/holds/{holdId}");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var json = await res.ReadJsonAsync();
        json.AssertHasProperties("holdId", "classId", "status", "expiresAt");                                   // contract
        json.AssertHasProperties("className", "createdAt", "confirmedAt", "cancelledAt", "serverTime");        // extras
        Assert.Equal(holdId.ToString(), json.GetProperty("holdId").GetString());
        Assert.Equal(classId.ToString(), json.GetProperty("classId").GetString());
        Assert.Equal("HELD", json.GetProperty("status").GetString());
        Assert.Equal(className, json.GetProperty("className").GetString());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("confirmedAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("cancelledAt").ValueKind);
        Assert.EndsWith("Z", json.GetProperty("expiresAt").GetString());
    }

    [Fact]
    public async Task Confirm_Returns200_WithHoldIdAndStatus()
    {
        var (classId, holdId, student, _) = await NewHoldAsync();

        using var res = await _client.ConfirmAsync(holdId, student);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var json = await res.ReadJsonAsync();
        json.AssertHasProperties("holdId", "status");          // contract
        json.AssertHasProperties("classId", "confirmedAt");    // extras
        Assert.Equal(holdId.ToString(), json.GetProperty("holdId").GetString());
        Assert.Equal("CONFIRMED", json.GetProperty("status").GetString());
        Assert.Equal(classId.ToString(), json.GetProperty("classId").GetString());
        Assert.EndsWith("Z", json.GetProperty("confirmedAt").GetString());
    }

    [Fact]
    public async Task Cancel_Returns204_WithEmptyBody()
    {
        var (_, holdId, student, _) = await NewHoldAsync();

        using var res = await _client.CancelAsync(holdId, student);

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        Assert.Empty(await res.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task MyHolds_Returns200_Array_WithExactFields()
    {
        var (classId, holdId, student, className) = await NewHoldAsync();

        using var res = await _client.MyHoldsAsync(student);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var item = Assert.Single((await res.ReadJsonAsync()).EnumerateArray());
        item.AssertHasProperties("holdId", "classId", "className", "status", "expiresAt"); // contract
        item.AssertHasProperties("createdAt");                                              // extra
        Assert.Equal(holdId.ToString(), item.GetProperty("holdId").GetString());
        Assert.Equal(classId.ToString(), item.GetProperty("classId").GetString());
        Assert.Equal(className, item.GetProperty("className").GetString());
        Assert.Equal("HELD", item.GetProperty("status").GetString());
    }

    [Fact]
    public async Task MyHolds_WithNoHolds_ReturnsEmptyArray()
    {
        using var res = await _client.MyHoldsAsync(NewStudent());

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("[]", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task StatusValues_AreUppercaseContractStrings_ThroughTheWholeLifecycle()
    {
        var (_, confirmed, s1, _) = await NewHoldAsync();
        var (_, cancelled, s2, _) = await NewHoldAsync();
        var (_, expired, _, _) = await NewHoldAsync();
        (await _client.ConfirmAsync(confirmed, s1)).Dispose();
        (await _client.CancelAsync(cancelled, s2)).Dispose();
        api.Factory.Clock.Advance(TimeSpan.FromSeconds(600));

        Assert.Equal("CONFIRMED", await _client.HoldStatusAsync(confirmed));
        Assert.Equal("CANCELLED", await _client.HoldStatusAsync(cancelled));
        Assert.Equal("EXPIRED", await _client.HoldStatusAsync(expired));
    }
}
