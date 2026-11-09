using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using SchoolAdmission.IntegrationTests.Infrastructure;

namespace SchoolAdmission.IntegrationTests.Contract;

/// <summary>Exact contract for endpoints 1–3 (API.md §4.1–4.3): status codes, headers, property names, casing.</summary>
public sealed partial class ClassContractTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    private readonly HttpClient _client = api.Client;

    [GeneratedRegex("^[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$")]
    private static partial Regex UuidV7Lowercase();

    private static readonly string[] SummaryFields = ["id", "name", "totalSeats", "heldSeats", "confirmedSeats", "availableSeats"];

    [Fact]
    public async Task CreateClass_Returns201_WithLocation_AndExactFields()
    {
        var name = TestData.ClassName("Class 6 - Morning Shift");

        using var res = await _client.CreateClassAsync($"  {name}  ", 120);

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        Assert.Equal("application/json", res.Content.Headers.ContentType?.MediaType);
        var json = await res.ReadJsonAsync();
        json.AssertHasProperties("id", "name", "totalSeats");
        var id = json.GetProperty("id").GetString()!;
        Assert.Matches(UuidV7Lowercase(), id);
        Assert.Equal(name, json.GetProperty("name").GetString()); // returned trimmed
        Assert.Equal(120, json.GetProperty("totalSeats").GetInt32());
        Assert.Equal($"/classes/{id}", res.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task GetClass_Returns200_WithExactFields_AndFreshCounts()
    {
        var id = await _client.CreateClassIdAsync(TestData.ClassName(), 30);

        using var res = await _client.GetAsync($"/classes/{id}");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var json = await res.ReadJsonAsync();
        json.AssertHasProperties(SummaryFields);
        Assert.Equal(id.ToString(), json.GetProperty("id").GetString());
        Assert.Equal(30, json.GetProperty("totalSeats").GetInt32());
        Assert.Equal(0, json.GetProperty("heldSeats").GetInt32());
        Assert.Equal(0, json.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(30, json.GetProperty("availableSeats").GetInt32());
    }

    [Fact]
    public async Task Location_PointsToTheCreatedClass()
    {
        using var created = await _client.CreateClassAsync(TestData.ClassName(), 7);

        using var res = await _client.GetAsync(created.Headers.Location);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal(7, (await res.ReadJsonAsync()).GetProperty("totalSeats").GetInt32());
    }

    [Fact]
    public async Task ListClasses_Returns200_Array_WithExactFields_OldestFirst()
    {
        var ids = new List<Guid>();
        foreach (var seats in new[] { 3, 1, 2 })
        {
            ids.Add(await _client.CreateClassIdAsync(TestData.ClassName("Order"), seats));
            api.Factory.Clock.Advance(TimeSpan.FromSeconds(1));
        }

        using var res = await _client.GetAsync("/classes");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var json = await res.ReadJsonAsync();
        Assert.Equal(JsonValueKind.Array, json.ValueKind);
        foreach (var item in json.EnumerateArray()) item.AssertHasProperties(SummaryFields);
        var listed = json.EnumerateArray().Select(e => Guid.Parse(e.GetProperty("id").GetString()!)).Where(ids.Contains).ToList();
        Assert.Equal(ids, listed);
    }

    [Fact]
    public async Task ListItem_AndDetail_HaveTheSameShapeAndValues()
    {
        var id = await _client.CreateClassIdAsync(TestData.ClassName(), 12);

        var list = await (await _client.GetAsync("/classes")).ReadJsonAsync();
        var item = list.EnumerateArray().Single(e => e.GetProperty("id").GetString() == id.ToString());
        var detail = await (await _client.GetAsync($"/classes/{id}")).ReadJsonAsync();

        Assert.Equal(item.GetRawText(), detail.GetRawText());
    }
}

/// <summary>Needs a database with no classes at all.</summary>
public sealed class EmptyClassListTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task ListClasses_WithNoClasses_ReturnsEmptyArray()
    {
        using var res = await api.Client.GetAsync("/classes");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("[]", await res.Content.ReadAsStringAsync());
    }
}
