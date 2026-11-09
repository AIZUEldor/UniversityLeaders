using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace UniversityLeaders.Api.Tests;

/// <summary>Reusable API steps for management scenarios.</summary>
internal static class ManagementSteps
{
    public const string PermanentPassword = "Faculty-Leader-Own-Pass-9!";

    public static async Task<int> CreateFacultyAsync(HttpClient admin, string csrf, string name)
    {
        HttpResponseMessage response = await admin.PostAsync("/api/admin/faculties", new { name }, csrf);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetInt32();
    }

    /// <summary>Returns the provisioning response: { user, temporaryPassword }.</summary>
    public static async Task<JsonElement> CreateFacultyLeaderAsync(HttpClient admin, string csrf, string userName, int facultyId)
    {
        HttpResponseMessage response = await admin.PostAsync("/api/admin/faculty-leaders", new { userName, fullName = "Faculty Leader", facultyId }, csrf);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>Signs in with the temporary password and sets <see cref="PermanentPassword"/>. Returns the client and its CSRF token.</summary>
    public static async Task<(HttpClient Client, string Csrf)> FirstLoginAsync(AuthApiFactory factory, string userName, string temporaryPassword)
    {
        HttpClient client = factory.CreateApiClient();
        (_, string? csrf) = await client.LoginAsync(userName, temporaryPassword);
        HttpResponseMessage changed = await client.PostAsync("/api/auth/change-password",
            new { currentPassword = temporaryPassword, newPassword = PermanentPassword }, csrf);
        changed.EnsureSuccessStatusCode();
        JsonElement body = await changed.Content.ReadFromJsonAsync<JsonElement>();
        return (client, body.GetProperty("csrfToken").GetString()!);
    }

    public static async Task<string?> AssertStatusAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        return await response.ErrorCodeAsync();
    }
}
