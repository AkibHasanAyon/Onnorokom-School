using System.Net;
using System.Text.Json;
using SchoolAdmission.IntegrationTests.Infrastructure;

namespace SchoolAdmission.IntegrationTests.Holds;

/// <summary>GET hold, confirm and cancel: check order, ownership (R4), idempotency (R5), effective expiry.</summary>
public sealed class HoldLifecycleTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    private readonly HttpClient _client = api.Client;
    private static int _student = 20_000;
    private static string NewStudent() => TestData.StudentId(Interlocked.Increment(ref _student));

    private async Task<(Guid classId, Guid holdId, string student)> NewHoldAsync(int seats = 5)
    {
        var classId = await _client.CreateClassIdAsync(TestData.ClassName(), seats);
        var student = NewStudent();
        return (classId, await _client.CreateHoldIdAsync(classId, student), student);
    }

    // ---- GET /holds/{id} -------------------------------------------------------------------

    [Fact]
    public async Task GetHold_NeedsNoHeader_AndIgnoresAnInvalidOne()
    {
        var (_, holdId, _) = await NewHoldAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/holds/{holdId}");
        request.Headers.Add("X-Student-Id", "garbage");

        using var res = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("h_789")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("0193a6f3-0a11-7c22-8e33-445566778899")]
    [InlineData("%20")]
    public async Task GetHold_MalformedOrUnknownId_Is404(string id)
    {
        using var res = await _client.GetAsync($"/holds/{id}");
        await res.AssertProblemAsync(HttpStatusCode.NotFound, "HOLD_NOT_FOUND");
    }

    [Fact]
    public async Task GetHold_ReportsExpired_ExactlyAtExpiresAt_BeforeAnySweep()
    {
        var (_, holdId, _) = await NewHoldAsync();

        api.Factory.Clock.Advance(TimeSpan.FromSeconds(599));
        Assert.Equal("HELD", await _client.HoldStatusAsync(holdId));

        api.Factory.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal("EXPIRED", await _client.HoldStatusAsync(holdId));
        Assert.Equal("HELD", await api.Database.ScalarAsync<string>($"SELECT status FROM holds WHERE id = '{holdId}'")); // stored row untouched
    }

    // ---- Confirm -------------------------------------------------------------------------------

    [Fact]
    public async Task Confirm_ByOwner_ConfirmsAndMovesSeatFromHeldToConfirmed()
    {
        var (classId, holdId, student) = await NewHoldAsync(seats: 3);
        Assert.Equal((1, 0, 2), await _client.SeatCountsAsync(classId));

        using var res = await _client.ConfirmAsync(holdId, student);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal((0, 1, 2), await _client.SeatCountsAsync(classId));
        var hold = await (await _client.GetAsync($"/holds/{holdId}")).ReadJsonAsync();
        Assert.Equal("CONFIRMED", hold.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, hold.GetProperty("confirmedAt").ValueKind);
    }

    [Fact]
    public async Task Confirm_Twice_Returns200WithSameBody_AndCountsOnce()
    {
        var (classId, holdId, student) = await NewHoldAsync();
        using var first = await _client.ConfirmAsync(holdId, student);
        api.Factory.Clock.Advance(TimeSpan.FromSeconds(30));

        using var second = await _client.ConfirmAsync(holdId, student);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await second.Content.ReadAsStringAsync());
        Assert.Equal(1, (await _client.SeatCountsAsync(classId)).confirmed);
    }

    [Fact]
    public async Task Confirm_ByOtherStudent_Is403_AndChangesNothing()
    {
        var (_, holdId, _) = await NewHoldAsync();

        using var res = await _client.ConfirmAsync(holdId, NewStudent());

        await res.AssertProblemAsync(HttpStatusCode.Forbidden, "HOLD_NOT_OWNED");
        Assert.Equal("HELD", await _client.HoldStatusAsync(holdId));
    }

    [Fact]
    public async Task Confirm_ByOtherStudent_OnConfirmedHold_IsStill403()
    {
        var (_, holdId, student) = await NewHoldAsync();
        (await _client.ConfirmAsync(holdId, student)).Dispose();

        using var res = await _client.ConfirmAsync(holdId, NewStudent());

        await res.AssertProblemAsync(HttpStatusCode.Forbidden, "HOLD_NOT_OWNED"); // ownership before idempotency
    }

    [Fact]
    public async Task Confirm_AtExpiresAt_Is409HoldExpired_ButOneSecondBefore_Succeeds()
    {
        var (_, late, s1) = await NewHoldAsync();
        var (_, inTime, s2) = await NewHoldAsync();

        api.Factory.Clock.Advance(TimeSpan.FromSeconds(599));
        using (var ok = await _client.ConfirmAsync(inTime, s2)) Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        api.Factory.Clock.Advance(TimeSpan.FromSeconds(1));
        using var res = await _client.ConfirmAsync(late, s1);

        await res.AssertProblemAsync(HttpStatusCode.Conflict, "HOLD_EXPIRED");
        Assert.Equal("EXPIRED", await _client.HoldStatusAsync(late));
        Assert.Equal("CONFIRMED", await _client.HoldStatusAsync(inTime)); // a confirmed hold never expires
    }

    [Fact]
    public async Task Confirm_CancelledHold_Is409HoldCancelled()
    {
        var (_, holdId, student) = await NewHoldAsync();
        (await _client.CancelAsync(holdId, student)).Dispose();

        using var res = await _client.ConfirmAsync(holdId, student);

        await res.AssertProblemAsync(HttpStatusCode.Conflict, "HOLD_CANCELLED");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("2015269123456789X")]
    public async Task Confirm_InvalidHeader_Is400_EvenForUnknownHold(string? header)
    {
        using var known = await _client.ConfirmAsync((await NewHoldAsync()).holdId, header);
        using var unknown = await _client.ConfirmAsync("nope", header);

        await known.AssertProblemAsync(HttpStatusCode.BadRequest, "INVALID_STUDENT_ID");
        await unknown.AssertProblemAsync(HttpStatusCode.BadRequest, "INVALID_STUDENT_ID");
    }

    [Theory]
    [InlineData("nope")]
    [InlineData("0193a6f3-0a11-7c22-8e33-445566778899")]
    public async Task Confirm_UnknownHold_Is404(string id)
    {
        using var res = await _client.ConfirmAsync(id, NewStudent());
        await res.AssertProblemAsync(HttpStatusCode.NotFound, "HOLD_NOT_FOUND");
    }

    [Fact]
    public async Task ApplyAgain_AfterConfirm_Is409AlreadyAdmitted()
    {
        var (classId, holdId, student) = await NewHoldAsync();
        (await _client.ConfirmAsync(holdId, student)).Dispose();

        using var res = await _client.HoldAsync(classId, student);

        var json = await res.AssertProblemAsync(HttpStatusCode.Conflict, "ALREADY_ADMITTED");
        Assert.Equal(holdId.ToString(), json.GetProperty("existingHoldId").GetString());
    }

    // ---- Cancel ----------------------------------------------------------------------------------

    [Fact]
    public async Task Cancel_FreesTheSeatImmediately_AndKeepsTheRow()
    {
        var (classId, holdId, student) = await NewHoldAsync(seats: 1);
        using (var full = await _client.HoldAsync(classId, NewStudent()))
            await full.AssertProblemAsync(HttpStatusCode.Conflict, "NO_SEATS_AVAILABLE");

        using var res = await _client.CancelAsync(holdId, student);

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        Assert.Equal((0, 0, 1), await _client.SeatCountsAsync(classId));
        Assert.Equal("CANCELLED", await api.Database.ScalarAsync<string>($"SELECT status FROM holds WHERE id = '{holdId}'"));
        var hold = await (await _client.GetAsync($"/holds/{holdId}")).ReadJsonAsync();
        Assert.NotEqual(JsonValueKind.Null, hold.GetProperty("cancelledAt").ValueKind);
        await _client.CreateHoldIdAsync(classId, NewStudent()); // someone else gets the seat at once
    }

    [Fact]
    public async Task Cancel_Twice_Returns204Both()
    {
        var (_, holdId, student) = await NewHoldAsync();

        using var first = await _client.CancelAsync(holdId, student);
        using var second = await _client.CancelAsync(holdId, student);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
    }

    [Fact]
    public async Task Cancel_ConfirmedHold_Is409HoldAlreadyConfirmed()
    {
        var (classId, holdId, student) = await NewHoldAsync();
        (await _client.ConfirmAsync(holdId, student)).Dispose();

        using var res = await _client.CancelAsync(holdId, student);

        await res.AssertProblemAsync(HttpStatusCode.Conflict, "HOLD_ALREADY_CONFIRMED");
        Assert.Equal(1, (await _client.SeatCountsAsync(classId)).confirmed);
    }

    [Fact]
    public async Task Cancel_ExpiredHold_Is409HoldExpired()
    {
        var (_, holdId, student) = await NewHoldAsync();
        api.Factory.Clock.Advance(TimeSpan.FromSeconds(600));

        using var res = await _client.CancelAsync(holdId, student);

        await res.AssertProblemAsync(HttpStatusCode.Conflict, "HOLD_EXPIRED");
    }

    [Fact]
    public async Task Cancel_ByOtherStudent_Is403_AndChangesNothing()
    {
        var (_, holdId, _) = await NewHoldAsync();

        using var res = await _client.CancelAsync(holdId, NewStudent());

        await res.AssertProblemAsync(HttpStatusCode.Forbidden, "HOLD_NOT_OWNED");
        Assert.Equal("HELD", await _client.HoldStatusAsync(holdId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("2015269123456789")]
    public async Task Cancel_InvalidHeader_Is400(string? header)
    {
        using var res = await _client.CancelAsync((await NewHoldAsync()).holdId, header);
        await res.AssertProblemAsync(HttpStatusCode.BadRequest, "INVALID_STUDENT_ID");
    }

    [Fact]
    public async Task Cancel_UnknownHold_Is404()
    {
        using var res = await _client.CancelAsync("0193a6f3-0a11-7c22-8e33-445566778899", NewStudent());
        await res.AssertProblemAsync(HttpStatusCode.NotFound, "HOLD_NOT_FOUND");
    }

    [Fact]
    public async Task ApplyAgain_AfterCancel_Succeeds_WithANewHold()
    {
        var (classId, holdId, student) = await NewHoldAsync();
        (await _client.CancelAsync(holdId, student)).Dispose();

        var again = await _client.CreateHoldIdAsync(classId, student);

        Assert.NotEqual(holdId, again);
    }

    [Fact]
    public async Task OwnerHeader_IsTrimmed_ForConfirmAndCancel()
    {
        var (_, a, s1) = await NewHoldAsync();
        var (_, b, s2) = await NewHoldAsync();

        using var confirm = await _client.ConfirmAsync(a, $"  {s1} ");
        using var cancel = await _client.CancelAsync(b, $"\t{s2}");

        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
    }

    [Theory]
    [InlineData("PUT", "/holds/{0}")]
    [InlineData("POST", "/holds/{0}")]
    [InlineData("GET", "/holds/{0}/confirm")]
    [InlineData("DELETE", "/holds/{0}/confirm")]
    [InlineData("POST", "/me/holds")]
    public async Task WrongMethod_Is405(string method, string pathTemplate)
    {
        var (_, holdId, _) = await NewHoldAsync();

        using var res = await _client.SendAsync(new HttpRequestMessage(new HttpMethod(method), string.Format(pathTemplate, holdId)));

        await res.AssertProblemAsync(HttpStatusCode.MethodNotAllowed, "METHOD_NOT_ALLOWED");
    }
}
