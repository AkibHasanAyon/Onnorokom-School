using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace SchoolAdmission.IntegrationTests.Infrastructure;

/// <summary>Typed shortcuts for API calls. Each returns the raw response unless the name says otherwise.</summary>
public static class ApiClientExtensions
{
    public static Task<HttpResponseMessage> CreateClassAsync(this HttpClient client, string? name, int? totalSeats) =>
        client.PostAsJsonAsync("/classes", new { name, totalSeats });

    public static Task<HttpResponseMessage> PostJsonAsync(this HttpClient client, string path, string rawJson) =>
        client.PostAsync(path, new StringContent(rawJson, Encoding.UTF8, "application/json"));

    /// <summary>POST /classes/{classId}/holds with the X-Student-Id header (omitted when null).</summary>
    public static Task<HttpResponseMessage> HoldAsync(this HttpClient client, object classId, string? studentId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/classes/{classId}/holds");
        if (studentId is not null) request.Headers.TryAddWithoutValidation("X-Student-Id", studentId);
        return client.SendAsync(request);
    }

    /// <summary>Applies and returns the new hold id; fails the test unless the API answers 201.</summary>
    public static async Task<Guid> CreateHoldIdAsync(this HttpClient client, Guid classId, string studentId)
    {
        using var res = await client.HoldAsync(classId, studentId);
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == HttpStatusCode.Created, $"Create hold failed: {(int)res.StatusCode} {body}");
        return Guid.Parse((await res.ReadJsonAsync()).GetProperty("holdId").GetString()!);
    }

    /// <summary>Creates a class and returns its id; fails the test unless the API answers 201.</summary>
    public static async Task<Guid> CreateClassIdAsync(this HttpClient client, string name, int totalSeats)
    {
        using var res = await client.CreateClassAsync(name, totalSeats);
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == HttpStatusCode.Created, $"Create class failed: {(int)res.StatusCode} {body}");
        return Guid.Parse((await res.ReadJsonAsync()).GetProperty("id").GetString()!);
    }
}
