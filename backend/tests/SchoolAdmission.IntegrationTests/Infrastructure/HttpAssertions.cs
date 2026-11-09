using System.Net;
using System.Text.Json;

namespace SchoolAdmission.IntegrationTests.Infrastructure;

/// <summary>Response helpers. Tests read JSON as <see cref="JsonElement"/> to check exact contract property names.</summary>
public static class HttpAssertions
{
    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    /// <summary>The problem+json <c>code</c>, or null if the body has none.</summary>
    public static async Task<string?> ProblemCodeAsync(this HttpResponseMessage response)
    {
        var json = await response.ReadJsonAsync();
        return json.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    /// <summary>
    /// Asserts status, <c>application/problem+json</c>, and the API.md §3 shape (type, title, status, code, traceId).
    /// Returns the body for further checks.
    /// </summary>
    public static async Task<JsonElement> AssertProblemAsync(this HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var raw = await response.Content.ReadAsStringAsync();
        Assert.True(status == response.StatusCode, $"Expected {(int)status} {code} but got {(int)response.StatusCode}: {raw}");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var json = JsonDocument.Parse(raw).RootElement.Clone();
        Assert.Equal(code, json.GetProperty("code").GetString());
        Assert.Equal((int)status, json.GetProperty("status").GetInt32());
        Assert.Equal($"https://httpstatuses.io/{(int)status}", json.GetProperty("type").GetString());
        Assert.True(json.TryGetProperty("title", out _), "problem+json has no title");
        Assert.False(string.IsNullOrEmpty(json.GetProperty("traceId").GetString()), "problem+json has no traceId");
        return json;
    }

    /// <summary>Asserts the object has exactly these property names (plus optional extras) — catches renames/casing.</summary>
    public static void AssertHasProperties(this JsonElement json, params string[] names)
    {
        foreach (var name in names)
            Assert.True(json.TryGetProperty(name, out _), $"Missing property '{name}' in {json.GetRawText()}");
    }
}
