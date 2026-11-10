using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UniversityLeaders.Domain.Entities;
using UniversityLeaders.Domain.Enums;

namespace UniversityLeaders.Api.Tests;

/// <summary>University Leader management of faculties, faculty leaders and university coordinators. Fresh database per test.</summary>
public sealed class AdminManagementTests : IAsyncLifetime
{
    private readonly AuthApiFactory _factory = new AuthApiFactory();

    public Task InitializeAsync() => _factory.InitializeAsync();

    public Task DisposeAsync() => ((IAsyncLifetime)_factory).DisposeAsync();

    [Fact]
    public async Task Only_a_university_leader_with_a_changed_password_can_use_admin_endpoints()
    {
        HttpClient anonymous = _factory.CreateApiClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/admin/faculties")).StatusCode);

        // University leader who has not yet replaced the bootstrap password.
        HttpClient pending = _factory.CreateApiClient();
        await _factory.BootstrapAsync(pending, "rector", "Temporary-Pass-2026!");
        await pending.LoginAsync("rector", "Temporary-Pass-2026!");
        HttpResponseMessage pendingResponse = await pending.GetAsync("/api/admin/faculties");
        Assert.Equal(HttpStatusCode.Forbidden, pendingResponse.StatusCode);
        Assert.Equal("password_change_required", await pendingResponse.ErrorCodeAsync());

        // A faculty leader (other role) is forbidden even after setting a permanent password.
        (HttpClient admin, string csrf) = await SignInAdminWithChangedPasswordAsync(pending);
        int facultyId = await ManagementSteps.CreateFacultyAsync(admin, csrf, "Fizika fakulteti");
        JsonElement provisioned = await ManagementSteps.CreateFacultyLeaderAsync(admin, csrf, "fl.fizika", facultyId);
        (HttpClient facultyLeader, string flCsrf) = await ManagementSteps.FirstLoginAsync(_factory, "fl.fizika", provisioned.GetProperty("temporaryPassword").GetString()!);

        HttpResponseMessage forbidden = await facultyLeader.GetAsync("/api/admin/faculty-leaders");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal("forbidden", await forbidden.ErrorCodeAsync());
        HttpResponseMessage forbiddenCreate = await facultyLeader.PostAsync("/api/admin/faculties", new { name = "Hack" }, flCsrf);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenCreate.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/admin/faculty-leaders")).StatusCode);
    }

    [Fact]
    public async Task Provisioning_returns_the_temporary_password_once_and_first_login_forces_a_change()
    {
        (HttpClient admin, string csrf) = await _factory.SignInUniversityLeaderAsync();
        int facultyId = await ManagementSteps.CreateFacultyAsync(admin, csrf, "Kimyo fakulteti");

        HttpResponseMessage created = await admin.PostAsync("/api/admin/faculty-leaders",
            new { userName = "FL.Kimyo", fullName = "Kimyo Rahbari", facultyId }, csrf);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.True(created.Headers.CacheControl?.NoStore);
        JsonElement body = await created.Content.ReadFromJsonAsync<JsonElement>();
        string temporaryPassword = body.GetProperty("temporaryPassword").GetString()!;
        JsonElement user = body.GetProperty("user");
        Assert.Equal(20, temporaryPassword.Length);
        Assert.Equal("fl.kimyo", user.GetProperty("userName").GetString());
        Assert.Equal("FacultyLeader", user.GetProperty("role").GetString());
        Assert.Equal(facultyId, user.GetProperty("facultyId").GetInt32());
        Assert.True(user.GetProperty("mustChangePassword").GetBoolean());
        string id = user.GetProperty("id").GetString()!;

        // Never retrievable again, never stored in plain text, never in the audit log.
        string list = await admin.GetStringAsync("/api/admin/faculty-leaders");
        string detail = await admin.GetStringAsync($"/api/admin/faculty-leaders/{id}");
        foreach (string json in new[] { list, detail })
        {
            Assert.DoesNotContain(temporaryPassword, json);
            Assert.DoesNotContain("temporaryPassword", json);
            Assert.DoesNotContain("hash", json, StringComparison.OrdinalIgnoreCase);
        }

        (string? hash, List<AuditLog> audit) = await _factory.WithDbAsync(async (db) =>
            (await db.Users.Where((User u) => u.UserName == "fl.kimyo").Select((User u) => u.PasswordHash).SingleAsync(),
             await db.AuditLogs.OrderBy((AuditLog a) => a.Id).ToListAsync()));
        Assert.NotEqual(temporaryPassword, hash);
        Assert.Contains(audit, (AuditLog a) => a.Action == "faculty.created");
        AuditLog userCreated = Assert.Single(audit, (AuditLog a) => a.Action == "user.created");
        Assert.Equal(id, userCreated.EntityId);
        Assert.NotNull(userCreated.ActorId);
        Assert.All(audit, (AuditLog a) => Assert.DoesNotContain(temporaryPassword, a.Details ?? string.Empty));

        // First login: only the password change is allowed, then the account works with the user's own password.
        HttpClient facultyLeader = _factory.CreateApiClient();
        (HttpResponseMessage login, string? flCsrf) = await facultyLeader.LoginAsync("FL.KIMYO", temporaryPassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        JsonElement me = await facultyLeader.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.True(me.GetProperty("mustChangePassword").GetBoolean());

        HttpResponseMessage changed = await facultyLeader.PostAsync("/api/auth/change-password",
            new { currentPassword = temporaryPassword, newPassword = ManagementSteps.PermanentPassword }, flCsrf);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        (HttpResponseMessage oldPassword, _) = await _factory.CreateApiClient().LoginAsync("fl.kimyo", temporaryPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);
    }

    [Fact]
    public async Task Seats_user_names_inputs_and_versions_are_enforced()
    {
        (HttpClient admin, string csrf) = await _factory.SignInUniversityLeaderAsync();
        int facultyId = await ManagementSteps.CreateFacultyAsync(admin, csrf, "Tarix fakulteti");

        HttpResponseMessage duplicateFacultyName = await admin.PostAsync("/api/admin/faculties", new { name = "TARIX FAKULTETI" }, csrf);
        Assert.Equal("faculty_name_taken", await ManagementSteps.AssertStatusAsync(duplicateFacultyName, HttpStatusCode.Conflict));

        JsonElement first = (await ManagementSteps.CreateFacultyLeaderAsync(admin, csrf, "fl.tarix", facultyId)).GetProperty("user");

        HttpResponseMessage secondLeader = await admin.PostAsync("/api/admin/faculty-leaders", new { userName = "fl.tarix2", fullName = "Second", facultyId }, csrf);
        Assert.Equal("faculty_leader_exists", await ManagementSteps.AssertStatusAsync(secondLeader, HttpStatusCode.Conflict));

        HttpResponseMessage sameNameOtherCase = await admin.PostAsync("/api/admin/faculty-leaders", new { userName = "FL.Tarix", fullName = "X", facultyId }, csrf);
        Assert.Equal("username_taken", await ManagementSteps.AssertStatusAsync(sameNameOtherCase, HttpStatusCode.Conflict));

        HttpResponseMessage badFaculty = await admin.PostAsync("/api/admin/faculty-leaders", new { userName = "fl.none", fullName = "None", facultyId = 999 }, csrf);
        Assert.Equal("validation_failed", await ManagementSteps.AssertStatusAsync(badFaculty, HttpStatusCode.BadRequest));
        HttpResponseMessage badUserName = await admin.PostAsync("/api/admin/faculty-leaders", new { userName = "bad name!", fullName = "", facultyId }, csrf);
        Assert.Equal("validation_failed", await ManagementSteps.AssertStatusAsync(badUserName, HttpStatusCode.BadRequest));

        // Optimistic concurrency: an update based on a stale version is rejected.
        string id = first.GetProperty("id").GetString()!;
        uint staleVersion = first.GetProperty("version").GetUInt32();
        HttpResponseMessage update = await admin.SendJsonAsync(HttpMethod.Put, $"/api/admin/faculty-leaders/{id}",
            new { fullName = "Renamed Leader", facultyId, version = staleVersion }, csrf);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        HttpResponseMessage stale = await admin.SendJsonAsync(HttpMethod.Put, $"/api/admin/faculty-leaders/{id}",
            new { fullName = "Lost Update", facultyId, version = staleVersion }, csrf);
        Assert.Equal("concurrent_update", await ManagementSteps.AssertStatusAsync(stale, HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task Deactivation_blocks_login_ends_sessions_frees_the_seat_and_keeps_history()
    {
        (HttpClient admin, string csrf) = await _factory.SignInUniversityLeaderAsync();
        int facultyId = await ManagementSteps.CreateFacultyAsync(admin, csrf, "Matematika fakulteti");
        JsonElement provisioned = await ManagementSteps.CreateFacultyLeaderAsync(admin, csrf, "fl.matem", facultyId);
        string id = provisioned.GetProperty("user").GetProperty("id").GetString()!;
        (HttpClient facultyLeader, _) = await ManagementSteps.FirstLoginAsync(_factory, "fl.matem", provisioned.GetProperty("temporaryPassword").GetString()!);

        // Historical work by this leader.
        Guid reportId = Guid.NewGuid();
        await _factory.WithDbAsync(async (db) =>
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            db.Reports.Add(new Report
            {
                Id = reportId, AuthorId = Guid.Parse(id), FacultyId = facultyId, Title = "Approved work",
                Status = ReportStatus.Approved, Score = 8, CreatedAt = now, UpdatedAt = now, SubmittedAt = now,
            });
            return await db.SaveChangesAsync();
        });

        JsonElement current = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/faculty-leaders/{id}");
        HttpResponseMessage deactivated = await admin.PostAsync($"/api/admin/faculty-leaders/{id}/deactivate",
            new { version = current.GetProperty("version").GetUInt32() }, csrf);
        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);
        JsonElement deactivatedUser = await deactivated.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(deactivatedUser.GetProperty("isActive").GetBoolean());

        Assert.Equal(HttpStatusCode.Unauthorized, (await facultyLeader.GetAsync("/api/auth/me")).StatusCode);
        (HttpResponseMessage login, _) = await _factory.CreateApiClient().LoginAsync("fl.matem", ManagementSteps.PermanentPassword);
        Assert.Equal("invalid_credentials", await ManagementSteps.AssertStatusAsync(login, HttpStatusCode.Unauthorized));

        // History intact: the report still points at its author, the faculty cannot be deleted, the action is audited.
        (Guid? authorId, bool auditExists) = await _factory.WithDbAsync(async (db) =>
            ((Guid?)await db.Reports.Where((Report r) => r.Id == reportId).Select((Report r) => r.AuthorId).SingleOrDefaultAsync(),
             await db.AuditLogs.AnyAsync((AuditLog a) => a.Action == "user.deactivated" && a.EntityId == id)));
        Assert.Equal(Guid.Parse(id), authorId);
        Assert.True(auditExists);
        HttpResponseMessage delete = await admin.SendJsonAsync(HttpMethod.Delete, $"/api/admin/faculties/{facultyId}", null, csrf);
        Assert.Equal("faculty_in_use", await ManagementSteps.AssertStatusAsync(delete, HttpStatusCode.Conflict));

        // The seat is free for a successor; the deactivated account is only listed on request.
        await ManagementSteps.CreateFacultyLeaderAsync(admin, csrf, "fl.matem.new", facultyId);
        JsonElement active = await admin.GetFromJsonAsync<JsonElement>("/api/admin/faculty-leaders");
        JsonElement all = await admin.GetFromJsonAsync<JsonElement>("/api/admin/faculty-leaders?includeInactive=true");
        Assert.Equal(1, active.GetArrayLength());
        Assert.Equal(2, all.GetArrayLength());

        HttpResponseMessage again = await admin.PostAsync($"/api/admin/faculty-leaders/{id}/deactivate",
            new { version = deactivatedUser.GetProperty("version").GetUInt32() }, csrf);
        Assert.Equal("already_inactive", await ManagementSteps.AssertStatusAsync(again, HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task Faculty_rename_persists_and_university_coordinators_cannot_be_managed()
    {
        (HttpClient admin, string csrf) = await _factory.SignInUniversityLeaderAsync();

        HttpResponseMessage createdFaculty = await admin.PostAsync("/api/admin/faculties", new { name = "Browser Test Faculty" }, csrf);
        Assert.Equal(HttpStatusCode.Created, createdFaculty.StatusCode);
        JsonElement faculty = await createdFaculty.Content.ReadFromJsonAsync<JsonElement>();
        int facultyId = faculty.GetProperty("id").GetInt32();
        HttpResponseMessage renamedFaculty = await admin.SendJsonAsync(HttpMethod.Put, $"/api/admin/faculties/{facultyId}",
            new { name = "Renamed Test Faculty", description = "  Programmes of the faculty.  ", version = faculty.GetProperty("version").GetUInt32() }, csrf);
        Assert.Equal(HttpStatusCode.OK, renamedFaculty.StatusCode);
        JsonElement faculties = await admin.GetFromJsonAsync<JsonElement>("/api/admin/faculties");
        JsonElement renamed = Assert.Single(faculties.EnumerateArray());
        Assert.Equal("Renamed Test Faculty", renamed.GetProperty("name").GetString());
        Assert.Equal("Programmes of the faculty.", renamed.GetProperty("description").GetString()); // trimmed

        // University coordinators are retired: there is no management API for them any more.
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/admin/university-coordinators")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync("/api/admin/university-coordinators", new { fullName = "X", directionId = 1 }, csrf)).StatusCode);
    }

    private async Task<(HttpClient Client, string Csrf)> SignInAdminWithChangedPasswordAsync(HttpClient pendingLeader)
    {
        string csrf = await pendingLeader.GetCsrfTokenAsync();
        HttpResponseMessage changed = await pendingLeader.PostAsync("/api/auth/change-password",
            new { currentPassword = "Temporary-Pass-2026!", newPassword = "Permanent-Leader-Pass-1!" }, csrf);
        changed.EnsureSuccessStatusCode();
        JsonElement body = await changed.Content.ReadFromJsonAsync<JsonElement>();
        return (pendingLeader, body.GetProperty("csrfToken").GetString()!);
    }
}
