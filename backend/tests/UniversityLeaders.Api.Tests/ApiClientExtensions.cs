using System.Net.Http.Json;
using System.Text.Json;

namespace UniversityLeaders.Api.Tests;

internal static class ApiClientExtensions
{
    public static async Task<string> GetCsrfTokenAsync(this HttpClient client)
    {
        JsonElement body = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        return body.GetProperty("csrfToken").GetString()!;
    }

    /// <summary>POST JSON with an optional X-CSRF-TOKEN header.</summary>
    public static Task<HttpResponseMessage> PostAsync(this HttpClient client, string url, object? body, string? csrfToken)
    {
        return client.SendJsonAsync(HttpMethod.Post, url, body, csrfToken);
    }

    public static async Task<HttpResponseMessage> SendJsonAsync(this HttpClient client, HttpMethod method, string url, object? body, string? csrfToken)
    {
        using HttpRequestMessage request = new HttpRequestMessage(method, url)
        {
            Content = JsonContent.Create(body ?? new { }),
        };
        if (csrfToken is not null)
        {
            request.Headers.Add("X-CSRF-TOKEN", csrfToken);
        }

        return await client.SendAsync(request);
    }

    /// <summary>Logs in and returns the CSRF token bound to the new session.</summary>
    public static async Task<(HttpResponseMessage Response, string? CsrfToken)> LoginAsync(this HttpClient client, string userName, string password)
    {
        string csrf = await client.GetCsrfTokenAsync();
        HttpResponseMessage response = await client.PostAsync("/api/auth/login", new { userName, password }, csrf);
        if (!response.IsSuccessStatusCode)
        {
            return (response, null);
        }

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (response, body.GetProperty("csrfToken").GetString());
    }

    public static async Task<string?> ErrorCodeAsync(this HttpResponseMessage response)
    {
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.TryGetProperty("code", out JsonElement code) ? code.GetString() : null;
    }
}
