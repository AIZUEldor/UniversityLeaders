using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace UniversityLeaders.Api.Tests;

/// <summary>A signed-in user (password already changed) with their CSRF token.</summary>
internal sealed record Actor(HttpClient Client, string Csrf, Guid Id);

/// <summary>University Leader, two faculties with leaders, three faculty coordinators and one university coordinator.</summary>
internal sealed record Cast(
    Actor Admin, int FacultyA, int FacultyB, Actor LeaderA, Actor LeaderB,
    Actor CoordinatorA1, Actor CoordinatorA2, Actor CoordinatorB1, Actor UniversityCoordinator)
{
    public static async Task<Cast> ArrangeAsync(AuthApiFactory factory)
    {
        (HttpClient adminClient, string adminCsrf) = await factory.SignInUniversityLeaderAsync();
        Guid adminId = Guid.Parse((await adminClient.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("id").GetString()!);
        Actor admin = new Actor(adminClient, adminCsrf, adminId);

        int facultyA = await ManagementSteps.CreateFacultyAsync(adminClient, adminCsrf, "Faculty A");
        int facultyB = await ManagementSteps.CreateFacultyAsync(adminClient, adminCsrf, "Faculty B");
        Actor leaderA = await ProvisionAsync(factory, admin, "/api/admin/faculty-leaders", "fl.a", "Leader A", new { facultyId = facultyA });
        Actor leaderB = await ProvisionAsync(factory, admin, "/api/admin/faculty-leaders", "fl.b", "Leader B", new { facultyId = facultyB });
        Actor coordinatorA1 = await ProvisionAsync(factory, leaderA, "/api/faculty/coordinators", "fc.a1", "Coordinator A1", new { directionId = 1 });
        Actor coordinatorA2 = await ProvisionAsync(factory, leaderA, "/api/faculty/coordinators", "fc.a2", "Coordinator A2", new { directionId = 2 });
        Actor coordinatorB1 = await ProvisionAsync(factory, leaderB, "/api/faculty/coordinators", "fc.b1", "Coordinator B1", new { directionId = 1 });
        Actor universityCoordinator = await ProvisionAsync(factory, admin, "/api/admin/university-coordinators", "uc.three", "University Coordinator", new { directionId = 3 });
        return new Cast(admin, facultyA, facultyB, leaderA, leaderB, coordinatorA1, coordinatorA2, coordinatorB1, universityCoordinator);
    }

    private static async Task<Actor> ProvisionAsync(AuthApiFactory factory, Actor creator, string url, string userName, string fullName, object seat)
    {
        Dictionary<string, object?> body = JsonSerializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(seat))!;
        body["userName"] = userName;
        body["fullName"] = fullName;
        HttpResponseMessage created = await creator.Client.PostAsync(url, body, creator.Csrf);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        JsonElement provisioned = await created.Content.ReadFromJsonAsync<JsonElement>();
        (HttpClient client, string csrf) = await ManagementSteps.FirstLoginAsync(factory, userName, provisioned.GetProperty("temporaryPassword").GetString()!);
        return new Actor(client, csrf, Guid.Parse(provisioned.GetProperty("user").GetProperty("id").GetString()!));
    }
}
