using System.Net;
using System.Text;
using SchoolAdmission.IntegrationTests.Infrastructure;

namespace SchoolAdmission.IntegrationTests.Classes;

/// <summary>POST /classes input rules (API.md §4.1) and duplicate names. Never a 500.</summary>
public sealed class CreateClassValidationTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    private readonly HttpClient _client = api.Client;

    [Theory]
    [InlineData("""{"totalSeats":5}""", "name")]
    [InlineData("""{"name":null,"totalSeats":5}""", "name")]
    [InlineData("""{"name":"","totalSeats":5}""", "name")]
    [InlineData("""{"name":"   ","totalSeats":5}""", "name")]
    [InlineData("""{"name":"x"}""", "totalSeats")]
    [InlineData("""{"name":"x","totalSeats":null}""", "totalSeats")]
    [InlineData("""{"name":"x","totalSeats":0}""", "totalSeats")]
    [InlineData("""{"name":"x","totalSeats":-1}""", "totalSeats")]
    [InlineData("""{"name":"x","totalSeats":10001}""", "totalSeats")]
    public async Task InvalidField_Is400ValidationFailed_NamingTheField(string body, string field)
    {
        using var res = await _client.PostJsonAsync("/classes", body);

        var json = await res.AssertProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_FAILED");
        Assert.True(json.GetProperty("errors").TryGetProperty(field, out _), json.GetRawText());
    }

    [Fact]
    public async Task EmptyObject_ReportsBothFields()
    {
        using var res = await _client.PostJsonAsync("/classes", "{}");

        var json = await res.AssertProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_FAILED");
        var errors = json.GetProperty("errors");
        Assert.True(errors.TryGetProperty("name", out _));
        Assert.True(errors.TryGetProperty("totalSeats", out _));
    }

    [Fact]
    public async Task Name101CharsAfterTrimming_Is400()
    {
        using var res = await _client.CreateClassAsync(new string('n', 101), 5);

        var json = await res.AssertProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_FAILED");
        Assert.True(json.GetProperty("errors").TryGetProperty("name", out _));
    }

    [Theory]
    [InlineData("""{"name":"x","totalSeats":"120"}""")]
    [InlineData("""{"name":"x","totalSeats":120.5}""")]
    [InlineData("""{"name":"x","totalSeats":1e10}""")]
    [InlineData("""{"name":"x","totalSeats":99999999999999999999}""")]
    [InlineData("""{"name":42,"totalSeats":5}""")]
    [InlineData("{bad")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("")]
    public async Task MalformedOrWronglyTypedBody_Is400MalformedJson(string body)
    {
        using var res = await _client.PostJsonAsync("/classes", body);
        await res.AssertProblemAsync(HttpStatusCode.BadRequest, "MALFORMED_JSON");
    }

    [Fact]
    public async Task JsonNullBody_Is400()
    {
        using var res = await _client.PostJsonAsync("/classes", "null");

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("application/problem+json", res.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task NonJsonContentType_Is415()
    {
        using var res = await _client.PostAsync("/classes", new StringContent("name=x&totalSeats=5", Encoding.UTF8, "application/x-www-form-urlencoded"));
        await res.AssertProblemAsync(HttpStatusCode.UnsupportedMediaType, "UNSUPPORTED_MEDIA_TYPE");
    }

    [Fact]
    public async Task UnknownFields_AreIgnored()
    {
        using var res = await _client.PostJsonAsync("/classes",
            $$"""{"name":"{{TestData.ClassName()}}","totalSeats":5,"shift":"Morning","id":"ignored"}""");

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10_000)]
    public async Task SeatBoundaries_AreAccepted(int seats)
    {
        using var res = await _client.CreateClassAsync(TestData.ClassName(), seats);

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        Assert.Equal(seats, (await res.ReadJsonAsync()).GetProperty("totalSeats").GetInt32());
    }

    [Fact]
    public async Task Name100CharsAfterTrimming_IsAccepted()
    {
        var name = Guid.NewGuid().ToString("N") + new string('x', 68); // exactly 100, unique

        using var res = await _client.CreateClassAsync($"   {name}   ", 5);

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        Assert.Equal(name, (await res.ReadJsonAsync()).GetProperty("name").GetString());
    }

    [Theory]
    [InlineData("{0}")]
    [InlineData("  {0}  ")]
    [InlineData("UPPER:{0}")]
    [InlineData("lower:{0}")]
    public async Task DuplicateName_CaseInsensitiveAndTrimmed_Is409(string variant)
    {
        var name = TestData.ClassName("Class 7 - Day Shift");
        await _client.CreateClassIdAsync(name, 10);
        var duplicate = variant.StartsWith("UPPER:") ? name.ToUpperInvariant()
            : variant.StartsWith("lower:") ? name.ToLowerInvariant()
            : string.Format(variant, name);

        using var res = await _client.CreateClassAsync(duplicate, 99);

        await res.AssertProblemAsync(HttpStatusCode.Conflict, "CLASS_NAME_EXISTS");
    }

    [Fact]
    public async Task ConcurrentCreatesWithSameName_ExactlyOneSucceeds_RestAre409()
    {
        var name = TestData.ClassName("Race");
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(0, 20).Select(async _ =>
        {
            await start.Task;
            using var res = await _client.CreateClassAsync(name, 5);
            return (res.StatusCode, Code: res.StatusCode == HttpStatusCode.Created ? null : await res.ProblemCodeAsync());
        }).ToArray();
        start.SetResult();
        var results = await Task.WhenAll(tasks);

        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(results.Where(r => r.StatusCode != HttpStatusCode.Created), r =>
        {
            Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
            Assert.Equal("CLASS_NAME_EXISTS", r.Code);
        });
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("123")]
    [InlineData("c_123")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("0193a6f2-7c1e-7b4a-9d3e-2f1a5c6b7d8e")] // well-formed but unknown
    [InlineData("0193a6f2-7c1e-7b4a-9d3e-2f1a5c6b7d8e-zz")]
    [InlineData("%20")]
    public async Task GetClass_MalformedOrUnknownId_Is404ClassNotFound(string id)
    {
        using var res = await _client.GetAsync($"/classes/{id}");
        await res.AssertProblemAsync(HttpStatusCode.NotFound, "CLASS_NOT_FOUND");
    }

    [Fact]
    public async Task GetClass_VeryLongId_Is404()
    {
        using var res = await _client.GetAsync($"/classes/{new string('a', 3000)}");
        await res.AssertProblemAsync(HttpStatusCode.NotFound, "CLASS_NOT_FOUND");
    }

    [Theory]
    [InlineData("PUT", "/classes")]
    [InlineData("DELETE", "/classes")]
    [InlineData("PUT", "/classes/0193a6f2-7c1e-7b4a-9d3e-2f1a5c6b7d8e")]
    public async Task UnsupportedMethod_Is405(string method, string path)
    {
        using var res = await _client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));
        await res.AssertProblemAsync(HttpStatusCode.MethodNotAllowed, "METHOD_NOT_ALLOWED");
    }
}
