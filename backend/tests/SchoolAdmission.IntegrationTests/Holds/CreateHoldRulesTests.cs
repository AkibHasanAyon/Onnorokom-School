using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolAdmission.Domain.Entities;
using SchoolAdmission.Infrastructure.Persistence;
using SchoolAdmission.IntegrationTests.Infrastructure;

namespace SchoolAdmission.IntegrationTests.Holds;

/// <summary>
/// Apply rules and their check order (API.md §4.4): 400 header → 404 class → 409 ALREADY_ADMITTED →
/// 409 ACTIVE_HOLD_EXISTS → 409 NO_SEATS_AVAILABLE. Confirm/cancel are applied through the domain model
/// here because those endpoints arrive in Phase 06.
/// </summary>
public sealed class CreateHoldRulesTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    private readonly HttpClient _client = api.Client;
    private static int _nextStudent = 100; // static: xUnit creates a new instance per test, ids must stay unique in the shared DB
    private static string NewStudent() => TestData.StudentId(Interlocked.Increment(ref _nextStudent));

    private async Task ChangeHoldAsync(Guid holdId, Action<Hold, DateTimeOffset> change)
    {
        await using var scope = api.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AdmissionDbContext>();
        var hold = await db.Holds.SingleAsync(h => h.Id == holdId);
        change(hold, api.Factory.Clock.GetUtcNow());
        await db.SaveChangesAsync();
    }

    private async Task<long> HoldRowsAsync(Guid classId) =>
        await api.Database.ScalarAsync<long>($"SELECT count(*) FROM holds WHERE class_id = '{classId}'");

    // ---- 1. Header (400) ------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("2015269123456789")]        // 16 digits
    [InlineData("201526912345678901")]      // 18 digits
    [InlineData("2015269123456789A")]
    [InlineData("abcdefghijklmnopq")]
    [InlineData("2015 2691234567890")]      // inner space
    [InlineData("20152691234567890,1")]
    [InlineData("২০১৫২৬৯১২৩৪৫৬৭৮৯০")]       // Bengali digits: the UI converts, the API accepts ASCII only
    [InlineData("-2015269123456789")]
    public async Task InvalidStudentId_Is400_AndCreatesNothing(string? header)
    {
        var classId = await _client.CreateClassIdAsync(TestData.ClassName(), 5);

        using var res = await _client.HoldAsync(classId, header);

        await res.AssertProblemAsync(HttpStatusCode.BadRequest, "INVALID_STUDENT_ID");
        Assert.Equal(0L, await HoldRowsAsync(classId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("123")]
    public async Task InvalidStudentId_WinsOverUnknownClass(string? header)
    {
        using var res = await _client.HoldAsync("not-a-class", header);
        await res.AssertProblemAsync(HttpStatusCode.BadRequest, "INVALID_STUDENT_ID");
    }

    // ---- 2. Class (404) -------------------------------------------------------------------

    [Theory]
    [InlineData("abc")]
    [InlineData("c_123")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("0193a6f2-7c1e-7b4a-9d3e-2f1a5c6b7d8e")]
    [InlineData("%20")]
    public async Task UnknownOrMalformedClass_Is404(string classId)
    {
        using var res = await _client.HoldAsync(classId, NewStudent());
        await res.AssertProblemAsync(HttpStatusCode.NotFound, "CLASS_NOT_FOUND");
    }

    // ---- 3–5. Conflicts (409) and their precedence --------------------------------------------

    [Fact]
    public async Task SecondApplication_SameClass_Is409ActiveHoldExists_WithExistingHoldId()
    {
        var classId = await _client.CreateClassIdAsync(TestData.ClassName(), 5);
        var student = NewStudent();
        var first = await _client.CreateHoldIdAsync(classId, student);

        using var res = await _client.HoldAsync(classId, student);

        var json = await res.AssertProblemAsync(HttpStatusCode.Conflict, "ACTIVE_HOLD_EXISTS");
        Assert.Equal(first.ToString(), json.GetProperty("existingHoldId").GetString());
        Assert.Equal(1L, await HoldRowsAsync(classId));
    }

    [Fact]
    public async Task ApplyAfterConfirmed_Is409AlreadyAdmitted_WithExistingHoldId()
    {
        var classId = await _client.CreateClassIdAsync(TestData.ClassName(), 5);
        var student = NewStudent();
        var holdId = await _client.CreateHoldIdAsync(classId, student);
        await ChangeHoldAsync(holdId, (h, now) => h.Confirm(now));

        using var res = await _client.HoldAsync(classId, student);

        var json = await res.AssertProblemAsync(HttpStatusCode.Conflict, "ALREADY_ADMITTED");
        Assert.Equal(holdId.ToString(), json.GetProperty("existingHoldId").GetString());
    }

    [Fact]
    public async Task AlreadyAdmitted_WinsOverNoSeats()
    {
        var classId = await _client.CreateClassIdAsync(TestData.ClassName(), 1);
        var student = NewStudent();
        var holdId = await _client.CreateHoldIdAsync(classId, student);
        await ChangeHoldAsync(holdId, (h, now) => h.Confirm(now)); // class is now full

        using var res = await _client.HoldAsync(classId, student);

        await res.AssertProblemAsync(HttpStatusCode.Conflict, "ALREADY_ADMITTED");
    }

    [Fact]
    public async Task ActiveHoldExists_WinsOverNoSeats()
    {
        var classId = await _client.CreateClassIdAsync(TestData.ClassName(), 1);
        var student = NewStudent();
        await _client.CreateHoldIdAsync(classId, student); // class is now full

        using var res = await _client.HoldAsync(classId, student);

        await res.AssertProblemAsync(HttpStatusCode.Conflict, "ACTIVE_HOLD_EXISTS");
    }

    [Fact]
    public async Task FullClass_OtherStudent_Is409NoSeatsAvailable_AndNothingInserted()
    {
        var classId = await _client.CreateClassIdAsync(TestData.ClassName(), 2);
        await _client.CreateHoldIdAsync(classId, NewStudent());
        await _client.CreateHoldIdAsync(classId, NewStudent());

        using var res = await _client.HoldAsync(classId, NewStudent());

        await res.AssertProblemAsync(HttpStatusCode.Conflict, "NO_SEATS_AVAILABLE");
        Assert.Equal(2L, await HoldRowsAsync(classId));
    }

    // ---- Re-applying ----------------------------------------------------------------------------

    [Fact]
    public async Task ApplyAfterCancelled_Succeeds_WithNewHold()
    {
        var classId = await _client.CreateClassIdAsync(TestData.ClassName(), 5);
        var student = NewStudent();
        var first = await _client.CreateHoldIdAsync(classId, student);
        await ChangeHoldAsync(first, (h, now) => h.Cancel(now));

        var second = await _client.CreateHoldIdAsync(classId, student);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task ApplyAfterExpiry_Succeeds_AndStaleHoldIsStoredAsExpired()
    {
        var classId = await _client.CreateClassIdAsync(TestData.ClassName(), 5);
        var student = NewStudent();
        var first = await _client.CreateHoldIdAsync(classId, student);

        api.Factory.Clock.Advance(TimeSpan.FromSeconds(599));
        using (var stillActive = await _client.HoldAsync(classId, student))
            await stillActive.AssertProblemAsync(HttpStatusCode.Conflict, "ACTIVE_HOLD_EXISTS");

        api.Factory.Clock.Advance(TimeSpan.FromSeconds(1)); // exactly expiresAt
        var second = await _client.CreateHoldIdAsync(classId, student);

        Assert.NotEqual(first, second);
        Assert.Equal("EXPIRED", await api.Database.ScalarAsync<string>($"SELECT status FROM holds WHERE id = '{first}'"));
        Assert.Equal("HELD", await api.Database.ScalarAsync<string>($"SELECT status FROM holds WHERE id = '{second}'"));
    }

    [Fact]
    public async Task ExpiredHold_FreesTheSeat_ForAnotherStudent()
    {
        var classId = await _client.CreateClassIdAsync(TestData.ClassName(), 1);
        await _client.CreateHoldIdAsync(classId, NewStudent());
        using (var full = await _client.HoldAsync(classId, NewStudent()))
            await full.AssertProblemAsync(HttpStatusCode.Conflict, "NO_SEATS_AVAILABLE");

        api.Factory.Clock.Advance(TimeSpan.FromSeconds(600));

        await _client.CreateHoldIdAsync(classId, NewStudent());
    }

    [Fact]
    public async Task SameStudent_DifferentClasses_BothSucceed()
    {
        var student = NewStudent();
        var a = await _client.CreateClassIdAsync(TestData.ClassName("A"), 5);
        var b = await _client.CreateClassIdAsync(TestData.ClassName("B"), 5);

        await _client.CreateHoldIdAsync(a, student);
        await _client.CreateHoldIdAsync(b, student);
    }

    [Fact]
    public async Task SuccessfulApply_UpdatesClassCounts()
    {
        var classId = await _client.CreateClassIdAsync(TestData.ClassName(), 3);
        await _client.CreateHoldIdAsync(classId, NewStudent());

        var json = await (await _client.GetAsync($"/classes/{classId}")).ReadJsonAsync();

        Assert.Equal(1, json.GetProperty("heldSeats").GetInt32());
        Assert.Equal(2, json.GetProperty("availableSeats").GetInt32());
        await Db.AssertInvariantAsync(api.Database.ConnectionString, api.Factory.Clock.GetUtcNow());
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("DELETE")]
    [InlineData("PUT")]
    public async Task WrongMethodOnHoldsCollection_Is405(string method)
    {
        var classId = await _client.CreateClassIdAsync(TestData.ClassName(), 3);
        using var res = await _client.SendAsync(new HttpRequestMessage(new HttpMethod(method), $"/classes/{classId}/holds"));
        await res.AssertProblemAsync(HttpStatusCode.MethodNotAllowed, "METHOD_NOT_ALLOWED");
    }
}
