using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SchoolAdmission.IntegrationTests.Infrastructure;

namespace SchoolAdmission.IntegrationTests.Api;

/// <summary>The API host plus the test-only <see cref="ProbeController"/>.</summary>
public sealed class ProbeApiFixture : ApiFixture
{
    protected override WebApplicationFactory<Program> Customize(AdmissionApiFactory factory) =>
        factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.AddControllers().AddApplicationPart(typeof(ProbeController).Assembly)));
}

/// <summary>problem+json error mapping, JSON conventions and CORS (API.md §1, §3).</summary>
public sealed class ApiConventionsTests(ProbeApiFixture api) : IClassFixture<ProbeApiFixture>
{
    private readonly HttpClient _client = api.Client;

    private static Task<JsonElement> ProblemAsync(HttpResponseMessage res, HttpStatusCode status, string code) =>
        res.AssertProblemAsync(status, code);

    private Task<HttpResponseMessage> PostRawAsync(string path, string body, string contentType = "application/json") =>
        _client.PostAsync(path, new StringContent(body, Encoding.UTF8, contentType));

    // ---- Exception mapping -----------------------------------------------------------

    [Theory]
    [InlineData("not-found", HttpStatusCode.NotFound, "CLASS_NOT_FOUND")]
    [InlineData("forbidden", HttpStatusCode.Forbidden, "HOLD_NOT_OWNED")]
    [InlineData("domain", HttpStatusCode.Conflict, "HOLD_EXPIRED")]
    [InlineData("student", HttpStatusCode.BadRequest, "INVALID_STUDENT_ID")]
    [InlineData("db-down", HttpStatusCode.ServiceUnavailable, "SERVICE_UNAVAILABLE")]
    [InlineData("unavailable", HttpStatusCode.ServiceUnavailable, "SERVICE_UNAVAILABLE")]
    [InlineData("too-large", HttpStatusCode.RequestEntityTooLarge, "PAYLOAD_TOO_LARGE")]
    public async Task Exceptions_MapToStatusAndCode(string kind, HttpStatusCode status, string code)
    {
        using var res = await _client.GetAsync($"/__probe/throw/{kind}");
        await ProblemAsync(res, status, code);
    }

    [Fact]
    public async Task Conflict_IncludesExtraContextFields()
    {
        using var res = await _client.GetAsync("/__probe/throw/conflict");

        var json = await ProblemAsync(res, HttpStatusCode.Conflict, "ACTIVE_HOLD_EXISTS");
        Assert.Equal(ProbeController.ExistingHoldId.ToString(), json.GetProperty("existingHoldId").GetString());
        Assert.Equal("Already applied.", json.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task InvalidStudentId_IncludesErrorsUnderHeaderName()
    {
        using var res = await _client.GetAsync("/__probe/throw/student");

        var json = await ProblemAsync(res, HttpStatusCode.BadRequest, "INVALID_STUDENT_ID");
        Assert.Equal(JsonValueKind.Array, json.GetProperty("errors").GetProperty("X-Student-Id").ValueKind);
    }

    [Fact]
    public async Task UnexpectedException_Is500InternalError_WithoutLeakingDetails()
    {
        using var res = await _client.GetAsync("/__probe/throw/bug");

        var json = await ProblemAsync(res, HttpStatusCode.InternalServerError, "INTERNAL_ERROR");
        var raw = json.GetRawText();
        Assert.DoesNotContain("SECRET", raw);
        Assert.DoesNotContain("hunter2", raw);
        Assert.DoesNotContain("InvalidOperationException", raw);
        Assert.DoesNotContain(" at ", json.GetProperty("detail").GetString());
    }

    // ---- Routing errors --------------------------------------------------------------

    [Fact]
    public async Task UnknownRoute_Is404NotFound()
    {
        using var res = await _client.GetAsync("/nope");
        await ProblemAsync(res, HttpStatusCode.NotFound, "NOT_FOUND");
    }

    [Fact]
    public async Task WrongMethodOnKnownRoute_Is405MethodNotAllowed()
    {
        using var res = await _client.PutAsync("/health", new StringContent("{}", Encoding.UTF8, "application/json"));
        await ProblemAsync(res, HttpStatusCode.MethodNotAllowed, "METHOD_NOT_ALLOWED");
    }

    // ---- Body binding ------------------------------------------------------------------

    [Fact]
    public async Task ValidBody_IsBound_AndUnknownFieldsAreIgnored()
    {
        using var res = await PostRawAsync("/__probe/body", """{"name":"Class 6","totalSeats":120,"extra":true}""");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("Class 6", json.GetProperty("name").GetString());
        Assert.Equal(120, json.GetProperty("totalSeats").GetInt32());
    }

    [Theory]
    [InlineData("{bad")]
    [InlineData("not json")]
    [InlineData("""{"name":"x","totalSeats":"120"}""")]
    [InlineData("""{"name":"x","totalSeats":120.5}""")]
    [InlineData("""{"name":"x","totalSeats":1e10}""")]
    [InlineData("""{"name":"x","totalSeats":99999999999999999999}""")]
    [InlineData("""{"name":123,"totalSeats":5}""")]
    [InlineData("[]")]
    [InlineData("")]
    public async Task MalformedOrWronglyTypedJson_Is400MalformedJson(string body)
    {
        using var res = await PostRawAsync("/__probe/body", body);

        var json = await ProblemAsync(res, HttpStatusCode.BadRequest, "MALFORMED_JSON");
        Assert.Equal(JsonValueKind.Object, json.GetProperty("errors").ValueKind);
        Assert.DoesNotContain("System.", json.GetRawText()); // no .NET type names leak
    }

    [Fact]
    public async Task WrongTypeError_NamesTheField()
    {
        using var res = await PostRawAsync("/__probe/body", """{"name":"x","totalSeats":"120"}""");

        var json = await ProblemAsync(res, HttpStatusCode.BadRequest, "MALFORMED_JSON");
        Assert.True(json.GetProperty("errors").TryGetProperty("totalSeats", out _));
    }

    [Fact]
    public async Task NonJsonContentType_Is415()
    {
        using var res = await PostRawAsync("/__probe/body", "name=x", "text/plain");
        await ProblemAsync(res, HttpStatusCode.UnsupportedMediaType, "UNSUPPORTED_MEDIA_TYPE");
    }

    // ---- JSON conventions ----------------------------------------------------------------

    [Fact]
    public async Task Timestamps_AreUtcWholeSecondsWithZ_AndNullStaysNull()
    {
        using var res = await _client.GetAsync("/__probe/time");

        var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("2026-11-01T04:10:00Z", json.GetProperty("at").GetString()); // +06:00 → UTC, .456 dropped
        Assert.Equal(JsonValueKind.Null, json.GetProperty("none").ValueKind);
    }

    // ---- CORS ------------------------------------------------------------------------------

    [Fact]
    public async Task Preflight_FromFrontendOrigin_AllowsStudentIdHeaderAndDelete()
    {
        using var req = new HttpRequestMessage(HttpMethod.Options, "/health");
        req.Headers.Add("Origin", "http://localhost:5173");
        req.Headers.Add("Access-Control-Request-Method", "DELETE");
        req.Headers.Add("Access-Control-Request-Headers", "x-student-id,content-type");

        using var res = await _client.SendAsync(req);

        Assert.True(res.IsSuccessStatusCode, $"preflight returned {(int)res.StatusCode}");
        Assert.Equal("http://localhost:5173", res.Headers.GetValues("Access-Control-Allow-Origin").Single());
        var allowedHeaders = string.Join(',', res.Headers.GetValues("Access-Control-Allow-Headers")).ToLowerInvariant();
        Assert.Contains("x-student-id", allowedHeaders);
        Assert.Contains("DELETE", string.Join(',', res.Headers.GetValues("Access-Control-Allow-Methods")));
    }

    [Fact]
    public async Task ErrorResponses_CarryCorsHeaders_SoTheUiCanReadThem()
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/__probe/throw/not-found");
        req.Headers.Add("Origin", "http://localhost:5173");

        using var res = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal("http://localhost:5173", res.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task UnknownOrigin_GetsNoCorsHeaders()
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/health");
        req.Headers.Add("Origin", "http://evil.example");

        using var res = await _client.SendAsync(req);

        Assert.False(res.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
