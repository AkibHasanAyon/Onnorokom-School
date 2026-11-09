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

    /// <summary>Creates a class and returns its id; fails the test unless the API answers 201.</summary>
    public static async Task<Guid> CreateClassIdAsync(this HttpClient client, string name, int totalSeats)
    {
        using var res = await client.CreateClassAsync(name, totalSeats);
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == HttpStatusCode.Created, $"Create class failed: {(int)res.StatusCode} {body}");
        return Guid.Parse((await res.ReadJsonAsync()).GetProperty("id").GetString()!);
    }
}
