using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace UniversityLeaders.Api.Tests;

/// <summary>Anonymous directory exposes active display names and vacant slots, never account details.</summary>
public sealed class PublicDirectoryTests : IAsyncLifetime
{
    private readonly AuthApiFactory _factory = new AuthApiFactory();

    public Task InitializeAsync() => _factory.InitializeAsync();

    public Task DisposeAsync() => ((IAsyncLifetime)_factory).DisposeAsync();

    [Fact]
    public async Task Directory_shows_only_active_names_and_all_seven_slots_without_private_fields()
    {
        HttpClient anonymous = _factory.CreateApiClient();
        JsonElement empty = await anonymous.GetFromJsonAsync<JsonElement>("/api/public/directory");
        Assert.Equal(0, empty.GetProperty("faculties").GetArrayLength());
        Assert.Equal(7, empty.GetProperty("universityCoordinators").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, empty.GetProperty("universityLeaderName").ValueKind);

        (HttpClient admin, string adminCsrf) = await _factory.SignInUniversityLeaderAsync();
        int facultyA = await ManagementSteps.CreateFacultyAsync(admin, adminCsrf, "Real Faculty A");
        await ManagementSteps.CreateFacultyAsync(admin, adminCsrf, "Real Faculty B");
        JsonElement leaderProvision = await ManagementSteps.CreateFacultyLeaderAsync(admin, adminCsrf, "fl.public", facultyA);
        string leaderId = leaderProvision.GetProperty("user").GetProperty("id").GetString()!;
        (HttpClient leader, string leaderCsrf) = await ManagementSteps.FirstLoginAsync(
            _factory, "fl.public", leaderProvision.GetProperty("temporaryPassword").GetString()!);
        HttpResponseMessage coordinatorCreated = await leader.PostAsync("/api/faculty/coordinators",
            new { userName = "fc.public", fullName = "Active Faculty Coordinator", directionId = 2 }, leaderCsrf);
        Assert.Equal(HttpStatusCode.Created, coordinatorCreated.StatusCode);
        JsonElement coordinator = (await coordinatorCreated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("user");
        string coordinatorId = coordinator.GetProperty("id").GetString()!;
        HttpResponseMessage universityCreated = await admin.PostAsync("/api/admin/university-coordinators",
            new { userName = "uc.public", fullName = "Active University Coordinator", directionId = 3 }, adminCsrf);
        Assert.Equal(HttpStatusCode.Created, universityCreated.StatusCode);
        JsonElement universityCoordinator = (await universityCreated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("user");
        string universityId = universityCoordinator.GetProperty("id").GetString()!;

        string json = await anonymous.GetStringAsync("/api/public/directory");
        JsonElement directory = JsonDocument.Parse(json).RootElement;
        Assert.Equal("Test University Leader", directory.GetProperty("universityLeaderName").GetString());
        JsonElement faculties = directory.GetProperty("faculties");
        Assert.Equal(2, faculties.GetArrayLength());
        Assert.Equal("Real Faculty A", faculties[0].GetProperty("name").GetString());
        Assert.Equal("Faculty Leader", faculties[0].GetProperty("leaderName").GetString());
        Assert.Equal(7, faculties[0].GetProperty("coordinators").GetArrayLength());
        Assert.Equal("Active Faculty Coordinator", faculties[0].GetProperty("coordinators")[1].GetProperty("coordinatorName").GetString());
        Assert.Equal(JsonValueKind.Null, faculties[1].GetProperty("leaderName").ValueKind);
        Assert.Equal(JsonValueKind.Null, faculties[1].GetProperty("coordinators")[1].GetProperty("coordinatorName").ValueKind);
        Assert.Equal("Active University Coordinator", directory.GetProperty("universityCoordinators")[2].GetProperty("coordinatorName").GetString());
        foreach (string privateField in new[] { "userName", "password", "hash", "securityStamp", "userId", "deactivatedAt", "version" })
        {
            Assert.DoesNotContain(privateField, json, StringComparison.OrdinalIgnoreCase);
        }
        Assert.DoesNotContain(leaderId, json);
        Assert.DoesNotContain(coordinatorId, json);
        Assert.DoesNotContain(universityId, json);

        Assert.Equal(HttpStatusCode.OK, (await leader.PostAsync($"/api/faculty/coordinators/{coordinatorId}/deactivate",
            new { version = coordinator.GetProperty("version").GetUInt32() }, leaderCsrf)).StatusCode);
        JsonElement leaderCurrent = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/faculty-leaders/{leaderId}");
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/admin/faculty-leaders/{leaderId}/deactivate",
            new { version = leaderCurrent.GetProperty("version").GetUInt32() }, adminCsrf)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/admin/university-coordinators/{universityId}/deactivate",
            new { version = universityCoordinator.GetProperty("version").GetUInt32() }, adminCsrf)).StatusCode);

        JsonElement inactive = await anonymous.GetFromJsonAsync<JsonElement>("/api/public/directory");
        Assert.Equal(JsonValueKind.Null, inactive.GetProperty("faculties")[0].GetProperty("leaderName").ValueKind);
        Assert.Equal(JsonValueKind.Null, inactive.GetProperty("faculties")[0].GetProperty("coordinators")[1].GetProperty("coordinatorName").ValueKind);
        Assert.Equal(JsonValueKind.Null, inactive.GetProperty("universityCoordinators")[2].GetProperty("coordinatorName").ValueKind);
    }
}
