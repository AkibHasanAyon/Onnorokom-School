using System.Net;
using SchoolAdmission.IntegrationTests.Infrastructure;

namespace SchoolAdmission.IntegrationTests.Holds;

/// <summary>GET /me/holds (API.md §4.8): own holds only, newest first, every status as effective status.</summary>
public sealed class MyHoldsTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    private readonly HttpClient _client = api.Client;
    private static int _student = 30_000;
    private static string NewStudent() => TestData.StudentId(Interlocked.Increment(ref _student));

    private async Task<List<(string holdId, string status)>> MineAsync(string student)
    {
        using var res = await _client.MyHoldsAsync(student);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.ReadJsonAsync()).EnumerateArray()
            .Select(e => (e.GetProperty("holdId").GetString()!, e.GetProperty("status").GetString()!))
            .ToList();
    }

    [Fact]
    public async Task ListsAllStatuses_NewestFirst()
    {
        var student = NewStudent();
        var ids = new List<Guid>();
        for (var i = 0; i < 4; i++)
        {
            var classId = await _client.CreateClassIdAsync(TestData.ClassName($"Mine {i}"), 3);
            ids.Add(await _client.CreateHoldIdAsync(classId, student));
            api.Factory.Clock.Advance(TimeSpan.FromSeconds(1));
        }
        (await _client.ConfirmAsync(ids[1], student)).Dispose();
        (await _client.CancelAsync(ids[2], student)).Dispose();
        api.Factory.Clock.Advance(TimeSpan.FromSeconds(600)); // ids[0] and ids[3] are now EXPIRED (effective)

        var mine = await MineAsync(student);

        Assert.Equal(ids.AsEnumerable().Reverse().Select(i => i.ToString()), mine.Select(m => m.holdId));
        Assert.Equal(["EXPIRED", "CANCELLED", "CONFIRMED", "EXPIRED"], mine.Select(m => m.status));
    }

    [Fact]
    public async Task SameSecondApplications_StillListNewestFirst()
    {
        var student = NewStudent();
        var first = await _client.CreateHoldIdAsync(await _client.CreateClassIdAsync(TestData.ClassName(), 3), student);
        api.Factory.Clock.Advance(TimeSpan.FromMilliseconds(5)); // same whole second, later millisecond
        var second = await _client.CreateHoldIdAsync(await _client.CreateClassIdAsync(TestData.ClassName(), 3), student);

        var mine = await MineAsync(student);

        Assert.Equal([second.ToString(), first.ToString()], mine.Select(m => m.holdId));
    }

    [Fact]
    public async Task ReturnsOnlyTheCallersHolds()
    {
        var classId = await _client.CreateClassIdAsync(TestData.ClassName(), 5);
        var me = NewStudent();
        var other = NewStudent();
        var mine = await _client.CreateHoldIdAsync(classId, me);
        await _client.CreateHoldIdAsync(classId, other);

        var list = await MineAsync(me);

        Assert.Equal([mine.ToString()], list.Select(m => m.holdId));
    }

    [Fact]
    public async Task IncludesReapplications_AsSeparateEntries()
    {
        var classId = await _client.CreateClassIdAsync(TestData.ClassName(), 5);
        var student = NewStudent();
        var first = await _client.CreateHoldIdAsync(classId, student);
        (await _client.CancelAsync(first, student)).Dispose();
        api.Factory.Clock.Advance(TimeSpan.FromSeconds(1));
        var second = await _client.CreateHoldIdAsync(classId, student);

        var list = await MineAsync(student);

        Assert.Equal([(second.ToString(), "HELD"), (first.ToString(), "CANCELLED")], list);
    }

    [Fact]
    public async Task HeaderIsTrimmed()
    {
        var student = NewStudent();
        await _client.CreateHoldIdAsync(await _client.CreateClassIdAsync(TestData.ClassName(), 2), student);

        var list = await MineAsync($"  {student}  ");

        Assert.Single(list);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("2015269123456789O")]
    [InlineData("২০১৫২৬৯১২৩৪৫৬৭৮৯০")]
    public async Task InvalidHeader_Is400(string? header)
    {
        using var res = await _client.MyHoldsAsync(header);
        await res.AssertProblemAsync(HttpStatusCode.BadRequest, "INVALID_STUDENT_ID");
    }
}
