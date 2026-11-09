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

    /// <summary>POST /holds/{holdId}/confirm (header omitted when null).</summary>
    public static Task<HttpResponseMessage> ConfirmAsync(this HttpClient client, object holdId, string? studentId) =>
        client.SendAsync(WithStudent(new HttpRequestMessage(HttpMethod.Post, $"/holds/{holdId}/confirm"), studentId));

    /// <summary>DELETE /holds/{holdId} (header omitted when null).</summary>
    public static Task<HttpResponseMessage> CancelAsync(this HttpClient client, object holdId, string? studentId) =>
        client.SendAsync(WithStudent(new HttpRequestMessage(HttpMethod.Delete, $"/holds/{holdId}"), studentId));

    /// <summary>GET /me/holds (header omitted when null).</summary>
    public static Task<HttpResponseMessage> MyHoldsAsync(this HttpClient client, string? studentId) =>
        client.SendAsync(WithStudent(new HttpRequestMessage(HttpMethod.Get, "/me/holds"), studentId));

    /// <summary>GET /holds/{holdId} and return the effective status string.</summary>
    public static async Task<string> HoldStatusAsync(this HttpClient client, Guid holdId)
    {
        using var res = await client.GetAsync($"/holds/{holdId}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.ReadJsonAsync()).GetProperty("status").GetString()!;
    }

    /// <summary>GET /classes/{classId} → (heldSeats, confirmedSeats, availableSeats).</summary>
    public static async Task<(int held, int confirmed, int available)> SeatCountsAsync(this HttpClient client, Guid classId)
    {
        var json = await (await client.GetAsync($"/classes/{classId}")).ReadJsonAsync();
        return (json.GetProperty("heldSeats").GetInt32(), json.GetProperty("confirmedSeats").GetInt32(),
            json.GetProperty("availableSeats").GetInt32());
    }

    private static HttpRequestMessage WithStudent(HttpRequestMessage request, string? studentId)
    {
        if (studentId is not null) request.Headers.TryAddWithoutValidation("X-Student-Id", studentId);
        return request;
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
