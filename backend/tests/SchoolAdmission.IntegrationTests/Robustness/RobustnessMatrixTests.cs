using System.Net;
using System.Net.Http.Headers;
using System.Text;
using SchoolAdmission.IntegrationTests.Infrastructure;

namespace SchoolAdmission.IntegrationTests.Robustness;

/// <summary>
/// R7: no client input may produce a 5xx. Every case must answer 4xx with a problem+json body
/// (Testing_QA.md §4.6, plus extra hostile inputs). Real seeded ids are substituted for {class}/{hold}.
/// </summary>
public sealed class RobustnessMatrixTests(RobustnessFixture api) : IClassFixture<RobustnessFixture>
{
    public sealed record Case(string Name, string Method, string Path, string? Body = null,
        string? ContentType = "application/json", string? StudentId = null, bool OmitStudentId = true,
        int? ExpectedStatus = null, string? ExpectedCode = null)
    {
        public override string ToString() => Name; // readable test names
    }

    private const string Valid = "20152691234567890";
    private const string Unknown = "0193a6f2-7c1e-7b4a-9d3e-2f1a5c6b7d8e";

    private static Case Body(string name, string body, int status = 400, string? code = null, string contentType = "application/json") =>
        new(name, "POST", "/classes", body, contentType, ExpectedStatus: status, ExpectedCode: code);

    public static TheoryData<Case> Cases()
    {
        var data = new TheoryData<Case>
        {
            // ---- POST /classes (Testing_QA.md §4.6) ----
            new("classes: no body", "POST", "/classes", null, null, ExpectedStatus: 415, ExpectedCode: "UNSUPPORTED_MEDIA_TYPE"),
            Body("classes: empty json body", "", 400, "MALFORMED_JSON"),
            Body("classes: {}", "{}", 400, "VALIDATION_FAILED"),
            Body("classes: not json", "not json", 400, "MALFORMED_JSON"),
            Body("classes: name empty", """{"name":""}""", 400, "VALIDATION_FAILED"),
            Body("classes: name blank", """{"name":"   ","totalSeats":5}""", 400, "VALIDATION_FAILED"),
            Body("classes: seats 0", """{"name":"x","totalSeats":0}""", 400, "VALIDATION_FAILED"),
            Body("classes: seats -1", """{"name":"x","totalSeats":-1}""", 400, "VALIDATION_FAILED"),
            Body("classes: seats 10001", """{"name":"x","totalSeats":10001}""", 400, "VALIDATION_FAILED"),
            Body("classes: seats string", """{"name":"x","totalSeats":"5"}""", 400, "MALFORMED_JSON"),
            Body("classes: seats 5.5", """{"name":"x","totalSeats":5.5}""", 400, "MALFORMED_JSON"),
            Body("classes: seats null", """{"name":"x","totalSeats":null}""", 400, "VALIDATION_FAILED"),
            Body("classes: seats 1e10", """{"name":"x","totalSeats":1e10}""", 400, "MALFORMED_JSON"),
            Body("classes: seats huge", """{"name":"x","totalSeats":99999999999999999999}""", 400, "MALFORMED_JSON"),
            Body("classes: name 101 chars", $$"""{"name":"{{new string('n', 101)}}","totalSeats":5}""", 400, "VALIDATION_FAILED"),
            Body("classes: array body", "[]", 400, "MALFORMED_JSON"),
            Body("classes: text/plain", "name=x", 415, "UNSUPPORTED_MEDIA_TYPE", "text/plain"),
            Body("classes: ~100 KB body (TestServer: validation, Kestrel: 413)", $$"""{"name":"{{new string('a', 100_000)}}","totalSeats":1}"""),
            // ---- POST /classes: extra hostile inputs ----
            Body("classes: json null", "null"),
            Body("classes: seats true", """{"name":"x","totalSeats":true}""", 400, "MALFORMED_JSON"),
            Body("classes: seats object", """{"name":"x","totalSeats":{}}""", 400, "MALFORMED_JSON"),
            Body("classes: name object", """{"name":{"a":1},"totalSeats":5}""", 400, "MALFORMED_JSON"),
            Body("classes: name array", """{"name":["x"],"totalSeats":5}""", 400, "MALFORMED_JSON"),
            Body("classes: seats -0.0", """{"name":"x","totalSeats":-0.0}""", 400, "MALFORMED_JSON"),
            Body("classes: seats int.MaxValue+1", """{"name":"x","totalSeats":2147483648}""", 400, "MALFORMED_JSON"),
            Body("classes: seats int.MinValue", """{"name":"x","totalSeats":-2147483648}""", 400, "VALIDATION_FAILED"),
            Body("classes: trailing comma", """{"name":"x","totalSeats":5,}""", 400, "MALFORMED_JSON"),
            Body("classes: comments", """{"name":"x", /* c */ "totalSeats":5}""", 400, "MALFORMED_JSON"),
            Body("classes: truncated", """{"name":"x","totalSe""", 400, "MALFORMED_JSON"),
            Body("classes: two documents", """{"name":"x","totalSeats":5}{}""", 400, "MALFORMED_JSON"),
            Body("classes: deeply nested", new string('[', 5000) + new string(']', 5000), 400, "MALFORMED_JSON"),
            Body("classes: bare string", "\"x\"", 400, "MALFORMED_JSON"),
            Body("classes: bare number", "42", 400, "MALFORMED_JSON"),
            Body("classes: xml", "<class/>", 415, "UNSUPPORTED_MEDIA_TYPE", "application/xml"),
            Body("classes: form", "name=x&totalSeats=5", 415, "UNSUPPORTED_MEDIA_TYPE", "application/x-www-form-urlencoded"),
            Body("classes: unknown charset", """{"name":"x","totalSeats":5}""", 415, "UNSUPPORTED_MEDIA_TYPE", "application/json; charset=klingon"),
            Body("classes: lone high surrogate", """{"name":"a\ud800b","totalSeats":5}""", 400, "MALFORMED_JSON"),
            Body("classes: lone low surrogate", """{"name":"\udc00","totalSeats":5}""", 400, "MALFORMED_JSON"),
            // ---- GET /classes/{id} ----
            new("class: abc", "GET", "/classes/abc", ExpectedStatus: 404, ExpectedCode: "CLASS_NOT_FOUND"),
            new("class: 123", "GET", "/classes/123", ExpectedStatus: 404, ExpectedCode: "CLASS_NOT_FOUND"),
            new("class: zero uuid", "GET", "/classes/00000000-0000-0000-0000-000000000000", ExpectedStatus: 404, ExpectedCode: "CLASS_NOT_FOUND"),
            new("class: random uuid", "GET", $"/classes/{Unknown}", ExpectedStatus: 404, ExpectedCode: "CLASS_NOT_FOUND"),
            new("class: very long id", "GET", $"/classes/{new string('a', 4000)}", ExpectedStatus: 404, ExpectedCode: "CLASS_NOT_FOUND"),
            // "/classes/%00" is rejected by Kestrel itself (400, before any middleware); TestServer throws client-side,
            // so it is verified against the real server only (see todo.md, Phase 08).
            new("class: braces uuid", "GET", $"/classes/%7B{Unknown}%7D", ExpectedStatus: 404, ExpectedCode: "CLASS_NOT_FOUND"),
            new("class: sql-ish", "GET", "/classes/1'%20OR%20'1'='1", ExpectedStatus: 404, ExpectedCode: "CLASS_NOT_FOUND"),
            // ---- POST /classes/{id}/holds ----
            new("apply: missing header", "POST", "/classes/{class}/holds", ExpectedStatus: 400, ExpectedCode: "INVALID_STUDENT_ID"),
            Apply("apply: empty header", ""),
            Apply("apply: 16 digits", "2015269123456789"),
            Apply("apply: 18 digits", "201526912345678901"),
            Apply("apply: letters", "abcdefghijklmnopq"),
            Apply("apply: bengali digits", "২০১৫২৬৯১২৩৪৫৬৭৮৯০"),
            Apply("apply: inner space", "2015 2691234567890"),
            Apply("apply: 10k digits", new string('1', 10_000)),
            Apply("apply: control char", "2015269123456789\u0001"),
            Apply("apply: negative", "-2015269123456789"),
            new("apply: malformed class", "POST", "/classes/abc/holds", StudentId: Valid, OmitStudentId: false, ExpectedStatus: 404, ExpectedCode: "CLASS_NOT_FOUND"),
            new("apply: unknown class", "POST", $"/classes/{Unknown}/holds", StudentId: Valid, OmitStudentId: false, ExpectedStatus: 404, ExpectedCode: "CLASS_NOT_FOUND"),
            // ---- GET /holds/{id} ----
            new("hold: malformed", "GET", "/holds/abc", ExpectedStatus: 404, ExpectedCode: "HOLD_NOT_FOUND"),
            new("hold: unknown", "GET", $"/holds/{Unknown}", ExpectedStatus: 404, ExpectedCode: "HOLD_NOT_FOUND"),
            new("hold: very long", "GET", $"/holds/{new string('9', 4000)}", ExpectedStatus: 404, ExpectedCode: "HOLD_NOT_FOUND"),
            // ---- POST /holds/{id}/confirm and DELETE /holds/{id} ----
            new("confirm: missing header", "POST", "/holds/{hold}/confirm", ExpectedStatus: 400, ExpectedCode: "INVALID_STUDENT_ID"),
            new("confirm: bad header", "POST", "/holds/{hold}/confirm", StudentId: "123", OmitStudentId: false, ExpectedStatus: 400, ExpectedCode: "INVALID_STUDENT_ID"),
            new("confirm: malformed id", "POST", "/holds/abc/confirm", StudentId: Valid, OmitStudentId: false, ExpectedStatus: 404, ExpectedCode: "HOLD_NOT_FOUND"),
            new("confirm: unknown id", "POST", $"/holds/{Unknown}/confirm", StudentId: Valid, OmitStudentId: false, ExpectedStatus: 404, ExpectedCode: "HOLD_NOT_FOUND"),
            new("confirm: not owner", "POST", "/holds/{hold}/confirm", StudentId: "20152691234567899", OmitStudentId: false, ExpectedStatus: 403, ExpectedCode: "HOLD_NOT_OWNED"),
            new("confirm: with garbage body", "POST", "/holds/{hold}/confirm", "{garbage", StudentId: "20152691234567899", OmitStudentId: false, ExpectedStatus: 403, ExpectedCode: "HOLD_NOT_OWNED"),
            new("cancel: missing header", "DELETE", "/holds/{hold}", ExpectedStatus: 400, ExpectedCode: "INVALID_STUDENT_ID"),
            new("cancel: bad header", "DELETE", "/holds/{hold}", StudentId: "x", OmitStudentId: false, ExpectedStatus: 400, ExpectedCode: "INVALID_STUDENT_ID"),
            new("cancel: malformed id", "DELETE", "/holds/abc", StudentId: Valid, OmitStudentId: false, ExpectedStatus: 404, ExpectedCode: "HOLD_NOT_FOUND"),
            new("cancel: unknown id", "DELETE", $"/holds/{Unknown}", StudentId: Valid, OmitStudentId: false, ExpectedStatus: 404, ExpectedCode: "HOLD_NOT_FOUND"),
            new("cancel: not owner", "DELETE", "/holds/{hold}", StudentId: "20152691234567899", OmitStudentId: false, ExpectedStatus: 403, ExpectedCode: "HOLD_NOT_OWNED"),
            // ---- GET /me/holds ----
            new("me: missing header", "GET", "/me/holds", ExpectedStatus: 400, ExpectedCode: "INVALID_STUDENT_ID"),
            new("me: bad header", "GET", "/me/holds", StudentId: "1234", OmitStudentId: false, ExpectedStatus: 400, ExpectedCode: "INVALID_STUDENT_ID"),
            new("me: huge header", "GET", "/me/holds", StudentId: new string('7', 10_000), OmitStudentId: false, ExpectedStatus: 400, ExpectedCode: "INVALID_STUDENT_ID"),
            // ---- Unknown routes and methods ----
            new("route: GET /nope", "GET", "/nope", ExpectedStatus: 404, ExpectedCode: "NOT_FOUND"),
            new("route: GET /", "GET", "/", ExpectedStatus: 404, ExpectedCode: "NOT_FOUND"),
            new("route: deep unknown", "GET", "/classes/{class}/holds/x/y", ExpectedStatus: 404, ExpectedCode: "NOT_FOUND"),
            new("method: PUT /classes", "PUT", "/classes", ExpectedStatus: 405, ExpectedCode: "METHOD_NOT_ALLOWED"),
            new("method: PATCH /holds/{id}", "PATCH", "/holds/{hold}", ExpectedStatus: 405, ExpectedCode: "METHOD_NOT_ALLOWED"),
            new("method: DELETE /classes/{id}", "DELETE", "/classes/{class}", ExpectedStatus: 405, ExpectedCode: "METHOD_NOT_ALLOWED"),
            new("method: POST /me/holds", "POST", "/me/holds", ExpectedStatus: 405, ExpectedCode: "METHOD_NOT_ALLOWED"),
        };
        return data;
    }

    private static Case Apply(string name, string header) =>
        new(name, "POST", "/classes/{class}/holds", StudentId: header, OmitStudentId: false,
            ExpectedStatus: 400, ExpectedCode: "INVALID_STUDENT_ID");

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task BadInput_Is4xx_WithProblemJson_NeverServerError(Case c)
    {
        var path = c.Path.Replace("{class}", api.ClassId.ToString()).Replace("{hold}", api.HoldId.ToString());
        using var request = new HttpRequestMessage(new HttpMethod(c.Method), path);
        if (c.Body is not null)
        {
            request.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(c.Body));
            if (c.ContentType is not null) request.Content.Headers.TryAddWithoutValidation("Content-Type", c.ContentType);
        }
        if (!c.OmitStudentId && c.StudentId is not null) request.Headers.TryAddWithoutValidation("X-Student-Id", c.StudentId);

        using var res = await api.Client.SendAsync(request);
        var raw = await res.Content.ReadAsStringAsync();

        var status = (int)res.StatusCode;
        Assert.True(status is >= 400 and < 500, $"{c.Name}: expected 4xx, got {status}: {raw}");
        Assert.Equal("application/problem+json", res.Content.Headers.ContentType?.MediaType);
        if (c.ExpectedStatus is { } expected) Assert.Equal(expected, status);
        if (c.ExpectedCode is { } code) Assert.Equal(code, (await res.ReadJsonAsync()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task InvalidUtf8Bytes_InBody_Is400()
    {
        var bytes = Encoding.UTF8.GetBytes("""{"name":"ab","totalSeats":5}""").ToList();
        bytes.InsertRange(10, new byte[] { 0xC3, 0x28, 0xFF }); // invalid sequences inside the name
        using var content = new ByteArrayContent(bytes.ToArray());
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using var res = await api.Client.PostAsync("/classes", content);

        var status = (int)res.StatusCode;
        Assert.True(status is >= 400 and < 500, $"got {status}: {await res.Content.ReadAsStringAsync()}");
    }

    [Fact]
    public async Task OneMegabyteStudentIdHeader_NeverServerError()
    {
        using var res = await api.Client.MyHoldsAsync(new string('1', 1_000_000));
        Assert.True((int)res.StatusCode is >= 400 and < 500, $"got {(int)res.StatusCode}");
    }

    [Fact]
    public async Task HeadAndOptionsWithoutPreflight_NeverServerError()
    {
        foreach (var (method, path) in new[] { ("HEAD", "/classes"), ("OPTIONS", "/classes"), ("HEAD", "/me/holds"), ("TRACE", "/classes") })
        {
            using var res = await api.Client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));
            Assert.True((int)res.StatusCode < 500, $"{method} {path} → {(int)res.StatusCode}");
        }
    }

    [Fact]
    public async Task AfterTheMatrix_DataIsStillConsistent()
    {
        foreach (Case c in Cases()) await BadInput_Is4xx_WithProblemJson_NeverServerError(c);

        Assert.Equal((1, 0, 4), await api.Client.SeatCountsAsync(api.ClassId)); // only the seeded hold
        await Db.AssertInvariantAsync(api.Database.ConnectionString, api.Factory.Clock.GetUtcNow());
    }
}

/// <summary>One database with a class (5 seats) and one hold, so {class}/{hold} cases hit real rows.</summary>
public sealed class RobustnessFixture : ApiFixture
{
    public Guid ClassId { get; private set; }
    public Guid HoldId { get; private set; }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        ClassId = await Client.CreateClassIdAsync(TestData.ClassName("Robust"), 5);
        HoldId = await Client.CreateHoldIdAsync(ClassId, "20152691234567890");
    }
}
