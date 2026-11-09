using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UniversityLeaders.Domain.Entities;
using UniversityLeaders.Domain.Enums;

namespace UniversityLeaders.Api.Tests;

/// <summary>Faculty leaders managing the coordinators of their own faculty. Fresh database per test.</summary>
public sealed class FacultyCoordinatorTests : IAsyncLifetime
{
    private const string Coordinators = "/api/faculty/coordinators";

    private readonly AuthApiFactory _factory = new AuthApiFactory();

    public Task InitializeAsync() => _factory.InitializeAsync();

    public Task DisposeAsync() => ((IAsyncLifetime)_factory).DisposeAsync();

    [Fact]
    public async Task Leader_provisions_own_coordinator_with_one_time_password_ignoring_any_client_faculty_id()
    {
        Scenario s = await ArrangeAsync();

        // A forged facultyId in the body is not part of the contract and must have no effect.
        HttpResponseMessage created = await s.LeaderA.PostAsync(Coordinators,
            new { userName = "FC.A.Yoshlar", fullName = "Coordinator A1", directionId = 1, facultyId = s.FacultyB }, s.CsrfA);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.True(created.Headers.CacheControl?.NoStore);
        JsonElement body = await created.Content.ReadFromJsonAsync<JsonElement>();
        JsonElement user = body.GetProperty("user");
        string temporaryPassword = body.GetProperty("temporaryPassword").GetString()!;
        string id = user.GetProperty("id").GetString()!;
        Assert.Equal(s.FacultyA, user.GetProperty("facultyId").GetInt32());
        Assert.Equal(1, user.GetProperty("directionId").GetInt32());
        Assert.Equal("FacultyCoordinator", user.GetProperty("role").GetString());
        Assert.True(user.GetProperty("mustChangePassword").GetBoolean());

        string list = await s.LeaderA.GetStringAsync(Coordinators);
        string detail = await s.LeaderA.GetStringAsync($"{Coordinators}/{id}");
        Assert.Contains(id, list);
        foreach (string json in new[] { list, detail })
        {
            Assert.DoesNotContain(temporaryPassword, json);
            Assert.DoesNotContain("temporaryPassword", json);
        }

        (string? hash, AuditLog? audit) = await _factory.WithDbAsync(async (db) =>
            (await db.Users.Where((User u) => u.Id == Guid.Parse(id)).Select((User u) => u.PasswordHash).SingleAsync(),
             await db.AuditLogs.SingleOrDefaultAsync((AuditLog a) => a.Action == "user.created" && a.EntityId == id)));
        Assert.NotEqual(temporaryPassword, hash);
        Assert.NotNull(audit);
        Assert.Equal(s.LeaderAId, audit.ActorId);
        Assert.DoesNotContain(temporaryPassword, audit.Details ?? string.Empty);

        // First login forces the password change; afterwards the coordinator still cannot manage anyone.
        HttpClient coordinator = _factory.CreateApiClient();
        (HttpResponseMessage login, string? fcCsrf) = await coordinator.LoginAsync("fc.a.yoshlar", temporaryPassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        HttpResponseMessage beforeChange = await coordinator.GetAsync(Coordinators);
        Assert.Equal("password_change_required", await ManagementSteps.AssertStatusAsync(beforeChange, HttpStatusCode.Forbidden));
        HttpResponseMessage changed = await coordinator.PostAsync("/api/auth/change-password",
            new { currentPassword = temporaryPassword, newPassword = ManagementSteps.PermanentPassword }, fcCsrf);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        string newCsrf = (await changed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("csrfToken").GetString()!;

        Assert.Equal("forbidden", await ManagementSteps.AssertStatusAsync(await coordinator.GetAsync(Coordinators), HttpStatusCode.Forbidden));
        HttpResponseMessage coordinatorCreates = await coordinator.PostAsync(Coordinators, new { userName = "fc.x", fullName = "X", directionId = 2 }, newCsrf);
        Assert.Equal(HttpStatusCode.Forbidden, coordinatorCreates.StatusCode);
    }

    [Fact]
    public async Task Leaders_cannot_see_or_change_other_faculties_and_other_roles_cannot_manage()
    {
        Scenario s = await ArrangeAsync();
        JsonElement coordinatorA = await CreateCoordinatorAsync(s.LeaderA, s.CsrfA, "fc.a1", 1);
        string id = coordinatorA.GetProperty("id").GetString()!;
        uint version = coordinatorA.GetProperty("version").GetUInt32();

        // Leader B: someone else's coordinator does not exist for them.
        Assert.Equal("user_not_found", await ManagementSteps.AssertStatusAsync(await s.LeaderB.GetAsync($"{Coordinators}/{id}"), HttpStatusCode.NotFound));
        HttpResponseMessage update = await s.LeaderB.SendJsonAsync(HttpMethod.Put, $"{Coordinators}/{id}", new { fullName = "Hijacked", directionId = 1, version }, s.CsrfB);
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        HttpResponseMessage deactivate = await s.LeaderB.PostAsync($"{Coordinators}/{id}/deactivate", new { version }, s.CsrfB);
        Assert.Equal(HttpStatusCode.NotFound, deactivate.StatusCode);
        JsonElement listB = await s.LeaderB.GetFromJsonAsync<JsonElement>($"{Coordinators}?includeInactive=true");
        Assert.Equal(0, listB.GetArrayLength());

        JsonElement stillActive = await s.LeaderA.GetFromJsonAsync<JsonElement>($"{Coordinators}/{id}");
        Assert.True(stillActive.GetProperty("isActive").GetBoolean());
        Assert.Equal("Coordinator", stillActive.GetProperty("fullName").GetString());

        // Faculty leaders cannot create other roles; university coordinators and the university leader cannot use this API.
        Assert.Equal(HttpStatusCode.Forbidden, (await s.LeaderA.PostAsync("/api/admin/university-coordinators", new { userName = "uc.x", fullName = "X", directionId = 2 }, s.CsrfA)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.LeaderA.PostAsync("/api/admin/faculty-leaders", new { userName = "fl.x", fullName = "X", facultyId = s.FacultyA }, s.CsrfA)).StatusCode);

        HttpResponseMessage ucCreated = await s.Admin.PostAsync("/api/admin/university-coordinators", new { userName = "uc.one", fullName = "UC", directionId = 3 }, s.AdminCsrf);
        string ucPassword = (await ucCreated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("temporaryPassword").GetString()!;
        (HttpClient universityCoordinator, _) = await ManagementSteps.FirstLoginAsync(_factory, "uc.one", ucPassword);
        Assert.Equal(HttpStatusCode.Forbidden, (await universityCoordinator.GetAsync(Coordinators)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Admin.GetAsync(Coordinators)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateApiClient().GetAsync(Coordinators)).StatusCode);
    }

    [Fact]
    public async Task One_active_coordinator_per_direction_per_faculty_with_safe_reassignment_and_versions()
    {
        Scenario s = await ArrangeAsync();
        JsonElement first = await CreateCoordinatorAsync(s.LeaderA, s.CsrfA, "fc.a1", 1);
        await CreateCoordinatorAsync(s.LeaderA, s.CsrfA, "fc.a2", 2);

        HttpResponseMessage duplicateSlot = await s.LeaderA.PostAsync(Coordinators, new { userName = "fc.a1.bis", fullName = "Dup", directionId = 1 }, s.CsrfA);
        Assert.Equal("coordinator_slot_taken", await ManagementSteps.AssertStatusAsync(duplicateSlot, HttpStatusCode.Conflict));
        await CreateCoordinatorAsync(s.LeaderB, s.CsrfB, "fc.b1", 1); // same direction in another faculty is fine

        HttpResponseMessage takenName = await s.LeaderB.PostAsync(Coordinators, new { userName = "FC.A1", fullName = "Clash", directionId = 2 }, s.CsrfB);
        Assert.Equal("username_taken", await ManagementSteps.AssertStatusAsync(takenName, HttpStatusCode.Conflict));
        foreach (int badDirection in new[] { 0, 8 })
        {
            HttpResponseMessage invalid = await s.LeaderA.PostAsync(Coordinators, new { userName = $"fc.bad{badDirection}", fullName = "Bad", directionId = badDirection }, s.CsrfA);
            Assert.Equal("validation_failed", await ManagementSteps.AssertStatusAsync(invalid, HttpStatusCode.BadRequest));
        }

        HttpResponseMessage missingFields = await s.LeaderA.PostAsync(Coordinators, new { userName = "x" }, s.CsrfA);
        JsonElement errors = (await missingFields.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.Equal(HttpStatusCode.BadRequest, missingFields.StatusCode);
        Assert.True(errors.TryGetProperty("userName", out _) && errors.TryGetProperty("fullName", out _) && errors.TryGetProperty("directionId", out _));

        // Reassignment only to a vacant direction in the same faculty.
        string id = first.GetProperty("id").GetString()!;
        uint version = first.GetProperty("version").GetUInt32();
        HttpResponseMessage toOccupied = await s.LeaderA.SendJsonAsync(HttpMethod.Put, $"{Coordinators}/{id}", new { fullName = "Coordinator", directionId = 2, version }, s.CsrfA);
        Assert.Equal("coordinator_slot_taken", await ManagementSteps.AssertStatusAsync(toOccupied, HttpStatusCode.Conflict));
        HttpResponseMessage toVacant = await s.LeaderA.SendJsonAsync(HttpMethod.Put, $"{Coordinators}/{id}", new { fullName = "Moved Coordinator", directionId = 3, version }, s.CsrfA);
        Assert.Equal(HttpStatusCode.OK, toVacant.StatusCode);
        JsonElement moved = await toVacant.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, moved.GetProperty("directionId").GetInt32());
        Assert.Equal(s.FacultyA, moved.GetProperty("facultyId").GetInt32());

        // The version used above is now stale.
        HttpResponseMessage stale = await s.LeaderA.SendJsonAsync(HttpMethod.Put, $"{Coordinators}/{id}", new { fullName = "Lost update", directionId = 3, version }, s.CsrfA);
        Assert.Equal("concurrent_update", await ManagementSteps.AssertStatusAsync(stale, HttpStatusCode.Conflict));
        HttpResponseMessage noVersion = await s.LeaderA.SendJsonAsync(HttpMethod.Put, $"{Coordinators}/{id}", new { fullName = "No version", directionId = 3 }, s.CsrfA);
        Assert.Equal("validation_failed", await ManagementSteps.AssertStatusAsync(noVersion, HttpStatusCode.BadRequest));

        Assert.Equal(1, await _factory.WithDbAsync((db) => db.AuditLogs.CountAsync((AuditLog a) => a.Action == "user.updated" && a.EntityId == id)));
    }

    [Fact]
    public async Task Deactivation_ends_access_frees_the_slot_and_preserves_reports_attachments_reviews_and_audit()
    {
        Scenario s = await ArrangeAsync();
        HttpResponseMessage created = await s.LeaderA.PostAsync(Coordinators, new { userName = "fc.a1", fullName = "Coordinator", directionId = 1 }, s.CsrfA);
        JsonElement provisioned = await created.Content.ReadFromJsonAsync<JsonElement>();
        string id = provisioned.GetProperty("user").GetProperty("id").GetString()!;
        (HttpClient coordinator, _) = await ManagementSteps.FirstLoginAsync(_factory, "fc.a1", provisioned.GetProperty("temporaryPassword").GetString()!);

        Guid coordinatorId = Guid.Parse(id);
        Guid reportId = Guid.NewGuid();
        await _factory.WithDbAsync(async (db) =>
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            Report report = new Report
            {
                Id = reportId, AuthorId = coordinatorId, FacultyId = s.FacultyA, DirectionId = 1, Title = "Approved event",
                Status = ReportStatus.Approved, Score = 9, CreatedAt = now, UpdatedAt = now, SubmittedAt = now,
            };
            report.Attachments.Add(new ReportAttachment
            {
                Id = Guid.NewGuid(), FileName = "photo.jpg", ContentType = "image/jpeg", SizeBytes = 1024,
                StorageKey = $"reports/{reportId}/photo.jpg", UploadedById = coordinatorId, UploadedAt = now,
            });
            report.Reviews.Add(new ReportReview { Id = Guid.NewGuid(), ReviewerId = s.LeaderAId, Action = ReviewAction.Approved, Score = 9, Comment = "OK", CreatedAt = now });
            db.Reports.Add(report);
            return await db.SaveChangesAsync();
        });

        JsonElement current = await s.LeaderA.GetFromJsonAsync<JsonElement>($"{Coordinators}/{id}");
        HttpResponseMessage stale = await s.LeaderA.PostAsync($"{Coordinators}/{id}/deactivate", new { version = current.GetProperty("version").GetUInt32() + 1 }, s.CsrfA);
        Assert.Equal("concurrent_update", await ManagementSteps.AssertStatusAsync(stale, HttpStatusCode.Conflict));

        HttpResponseMessage deactivated = await s.LeaderA.PostAsync($"{Coordinators}/{id}/deactivate", new { version = current.GetProperty("version").GetUInt32() }, s.CsrfA);
        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);
        JsonElement inactive = await deactivated.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(inactive.GetProperty("isActive").GetBoolean());

        Assert.Equal(HttpStatusCode.Unauthorized, (await coordinator.GetAsync("/api/auth/me")).StatusCode);
        (HttpResponseMessage login, _) = await _factory.CreateApiClient().LoginAsync("fc.a1", ManagementSteps.PermanentPassword);
        Assert.Equal("invalid_credentials", await ManagementSteps.AssertStatusAsync(login, HttpStatusCode.Unauthorized));

        HttpResponseMessage edit = await s.LeaderA.SendJsonAsync(HttpMethod.Put, $"{Coordinators}/{id}",
            new { fullName = "Edited", directionId = 1, version = inactive.GetProperty("version").GetUInt32() }, s.CsrfA);
        Assert.Equal("user_inactive", await ManagementSteps.AssertStatusAsync(edit, HttpStatusCode.Conflict));

        (int reports, int attachments, int reviews, bool audited) = await _factory.WithDbAsync(async (db) =>
            (await db.Reports.CountAsync((Report r) => r.AuthorId == coordinatorId),
             await db.ReportAttachments.CountAsync((ReportAttachment a) => a.UploadedById == coordinatorId),
             await db.ReportReviews.CountAsync((ReportReview r) => r.ReportId == reportId),
             await db.AuditLogs.AnyAsync((AuditLog a) => a.Action == "user.deactivated" && a.EntityId == id && a.ActorId == s.LeaderAId)));
        Assert.Equal((1, 1, 1, true), (reports, attachments, reviews, audited));

        // The slot is free for a successor; the deactivated coordinator only appears when asked for.
        await CreateCoordinatorAsync(s.LeaderA, s.CsrfA, "fc.a1.successor", 1);
        Assert.Equal(1, (await s.LeaderA.GetFromJsonAsync<JsonElement>(Coordinators)).GetArrayLength());
        Assert.Equal(2, (await s.LeaderA.GetFromJsonAsync<JsonElement>($"{Coordinators}?includeInactive=true")).GetArrayLength());
    }

    private sealed record Scenario(
        HttpClient Admin, string AdminCsrf, int FacultyA, int FacultyB,
        HttpClient LeaderA, string CsrfA, Guid LeaderAId, HttpClient LeaderB, string CsrfB);

    /// <summary>University leader, two faculties, one faculty leader each (passwords already changed).</summary>
    private async Task<Scenario> ArrangeAsync()
    {
        (HttpClient admin, string adminCsrf) = await _factory.SignInUniversityLeaderAsync();
        int facultyA = await ManagementSteps.CreateFacultyAsync(admin, adminCsrf, "Faculty A");
        int facultyB = await ManagementSteps.CreateFacultyAsync(admin, adminCsrf, "Faculty B");
        JsonElement leaderA = await ManagementSteps.CreateFacultyLeaderAsync(admin, adminCsrf, "fl.a", facultyA);
        JsonElement leaderB = await ManagementSteps.CreateFacultyLeaderAsync(admin, adminCsrf, "fl.b", facultyB);
        (HttpClient clientA, string csrfA) = await ManagementSteps.FirstLoginAsync(_factory, "fl.a", leaderA.GetProperty("temporaryPassword").GetString()!);
        (HttpClient clientB, string csrfB) = await ManagementSteps.FirstLoginAsync(_factory, "fl.b", leaderB.GetProperty("temporaryPassword").GetString()!);
        Guid leaderAId = Guid.Parse(leaderA.GetProperty("user").GetProperty("id").GetString()!);
        return new Scenario(admin, adminCsrf, facultyA, facultyB, clientA, csrfA, leaderAId, clientB, csrfB);
    }

    /// <summary>Creates a coordinator in the caller's faculty and returns the user part of the response.</summary>
    private static async Task<JsonElement> CreateCoordinatorAsync(HttpClient leader, string csrf, string userName, int directionId)
    {
        HttpResponseMessage response = await leader.PostAsync(Coordinators, new { userName, fullName = "Coordinator", directionId }, csrf);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("user");
    }
}
