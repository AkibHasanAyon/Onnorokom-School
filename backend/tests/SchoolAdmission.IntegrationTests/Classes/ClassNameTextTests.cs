using System.Net;
using SchoolAdmission.IntegrationTests.Infrastructure;

namespace SchoolAdmission.IntegrationTests.Classes;

/// <summary>Class names round-trip any printable Unicode, but never control characters (NUL used to be a 500).</summary>
public sealed class ClassNameTextTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task NulInName_Is400_NotAServerError_AndNothingIsStored()
    {
        var before = await api.Database.ScalarAsync<long>("SELECT count(*) FROM classes");

        using var res = await api.Client.PostJsonAsync("/classes", """{"name":"Class\u00006","totalSeats":5}""");

        var json = await res.AssertProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_FAILED");
        Assert.True(json.GetProperty("errors").TryGetProperty("name", out _));
        Assert.Equal(before, await api.Database.ScalarAsync<long>("SELECT count(*) FROM classes"));
    }

    [Theory]
    [InlineData("৬ষ্ঠ শ্রেণি - প্রভাতী শাখা")]
    [InlineData("Class 6 – Day Shift 🌞")]
    [InlineData("O'Brien \"quoted\" <b>not html</b> & co")]
    public async Task PrintableUnicode_RoundTripsExactly(string text)
    {
        var name = $"{text} {Guid.NewGuid():N}"[..Math.Min(100, text.Length + 33)];
        var id = await api.Client.CreateClassIdAsync(name, 3);

        var json = await (await api.Client.GetAsync($"/classes/{id}")).ReadJsonAsync();

        Assert.Equal(name, json.GetProperty("name").GetString());
    }

    [Fact]
    public async Task EdgeWhitespaceAndLineBreaks_AreTrimmed_NotRejected()
    {
        var name = TestData.ClassName("Trimmed");

        using var res = await api.Client.CreateClassAsync($"\t {name}\r\n", 3);

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        Assert.Equal(name, (await res.ReadJsonAsync()).GetProperty("name").GetString());
    }
}
