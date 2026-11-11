using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UniversityLeaders.Domain.Entities;
using UniversityLeaders.Domain.Enums;

namespace UniversityLeaders.Api.Tests;

/// <summary>
/// Faculty leaders managing the coordinator profiles (no login) of their own faculty, including photos and phone numbers,
/// and how those profiles appear on the public site. Fresh database per test.
/// </summary>
public sealed class FacultyCoordinatorTests : IAsyncLifetime
{
    private const string Coordinators = "/api/faculty/coordinators";
    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01 };
    private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0xFF, 0xD9 };

    private readonly AuthApiFactory _factory = new AuthApiFactory();

    public Task InitializeAsync() => _factory.InitializeAsync();

    public Task DisposeAsync() => ((IAsyncLifetime)_factory).DisposeAsync();

    [Fact]
    public async Task Leader_creates_own_coordinator_profile_with_consented_phone_ignoring_any_client_faculty_id()
    {
        Scenario s = await ArrangeAsync();

        // A forged facultyId in the body is not part of the contract and must have no effect.
        HttpResponseMessage created = await s.LeaderA.PostAsync(Coordinators,
            new { fullName = "Coordinator A1", directionId = 1, facultyId = s.FacultyB, phone = "+998 90 123-45-67", phoneIsPublic = true }, s.CsrfA);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        JsonElement user = await created.Content.ReadFromJsonAsync<JsonElement>();
        string id = user.GetProperty("id").GetString()!;
        Assert.Equal(s.FacultyA, user.GetProperty("facultyId").GetInt32());
        Assert.Equal(1, user.GetProperty("directionId").GetInt32());
        Assert.Equal("FacultyCoordinator", user.GetProperty("role").GetString());
        Assert.False(user.GetProperty("hasLogin").GetBoolean());
        Assert.False(user.GetProperty("mustChangePassword").GetBoolean());
        Assert.False(user.TryGetProperty("temporaryPassword", out _));
        Assert.Equal("+998 90 123-45-67", user.GetProperty("phone").GetString());

        (string? hash, AuditLog? audit) = await _factory.WithDbAsync(async (db) =>
            (await db.Users.Where((User u) => u.Id == Guid.Parse(id)).Select((User u) => u.PasswordHash).SingleAsync(),
             await db.AuditLogs.SingleOrDefaultAsync((AuditLog a) => a.Action == "user.created" && a.EntityId == id)));
        Assert.Null(hash); // no credentials at all
        Assert.NotNull(audit);
        Assert.Equal(s.LeaderAId, audit.ActorId);

        HttpResponseMessage badPhone = await s.LeaderA.PostAsync(Coordinators, new { fullName = "Bad phone", directionId = 2, phone = "call me" }, s.CsrfA);
        Assert.Equal("validation_failed", await ManagementSteps.AssertStatusAsync(badPhone, HttpStatusCode.BadRequest));

        // Public profile: name and consented phone; every unassigned direction is a vacant slot.
        JsonElement slot = await PublicSlotAsync(s.FacultyA, 1);
        Assert.Equal("Coordinator A1", slot.GetProperty("coordinatorName").GetString());
        Assert.Equal("+998 90 123-45-67", slot.GetProperty("phone").GetString());
        Assert.Equal(JsonValueKind.Null, (await PublicSlotAsync(s.FacultyA, 2)).GetProperty("coordinatorName").ValueKind);

        // Changes by the leader show up publicly right away; withdrawn consent hides the phone.
        HttpResponseMessage edited = await s.LeaderA.SendJsonAsync(HttpMethod.Put, $"{Coordinators}/{id}",
            new { fullName = "Renamed Coordinator", directionId = 1, phone = "+998 90 123-45-67", phoneIsPublic = false, version = user.GetProperty("version").GetUInt32() }, s.CsrfA);
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        slot = await PublicSlotAsync(s.FacultyA, 1);
        Assert.Equal("Renamed Coordinator", slot.GetProperty("coordinatorName").GetString());
        Assert.Equal(JsonValueKind.Null, slot.GetProperty("phone").ValueKind);
        Assert.Equal("+998 90 123-45-67", (await s.LeaderA.GetFromJsonAsync<JsonElement>($"{Coordinators}/{id}")).GetProperty("phone").GetString()); // kept privately
    }

    [Fact]
    public async Task Leaders_cannot_see_or_change_other_faculties_and_other_roles_cannot_manage()
    {
        Scenario s = await ArrangeAsync();
        JsonElement coordinatorA = await CreateCoordinatorAsync(s.LeaderA, s.CsrfA, 1);
        string id = coordinatorA.GetProperty("id").GetString()!;
        uint version = coordinatorA.GetProperty("version").GetUInt32();

        // Leader B: someone else's coordinator does not exist for them.
        Assert.Equal("user_not_found", await ManagementSteps.AssertStatusAsync(await s.LeaderB.GetAsync($"{Coordinators}/{id}"), HttpStatusCode.NotFound));
        HttpResponseMessage update = await s.LeaderB.SendJsonAsync(HttpMethod.Put, $"{Coordinators}/{id}", new { fullName = "Hijacked", directionId = 1, version }, s.CsrfB);
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        HttpResponseMessage deactivate = await s.LeaderB.PostAsync($"{Coordinators}/{id}/deactivate", new { version }, s.CsrfB);
        Assert.Equal(HttpStatusCode.NotFound, deactivate.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await UploadPhotoAsync(s.LeaderB, s.CsrfB, $"{Coordinators}/{id}/photo", Png, "image/png")).StatusCode);
        JsonElement listB = await s.LeaderB.GetFromJsonAsync<JsonElement>($"{Coordinators}?includeInactive=true");
        Assert.Equal(0, listB.GetArrayLength());

        JsonElement stillActive = await s.LeaderA.GetFromJsonAsync<JsonElement>($"{Coordinators}/{id}");
        Assert.True(stillActive.GetProperty("isActive").GetBoolean());
        Assert.Equal("Coordinator", stillActive.GetProperty("fullName").GetString());

        // Faculty leaders cannot manage faculty leaders; the University Leader cannot use this faculty API.
        Assert.Equal(HttpStatusCode.Forbidden, (await s.LeaderA.PostAsync("/api/admin/faculty-leaders", new { userName = "fl.x", fullName = "X", facultyId = s.FacultyA }, s.CsrfA)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Admin.GetAsync(Coordinators)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateApiClient().GetAsync(Coordinators)).StatusCode);

    }

    [Fact]
    public async Task One_active_coordinator_per_direction_per_faculty_with_safe_reassignment_and_versions()
    {
        Scenario s = await ArrangeAsync();
        JsonElement first = await CreateCoordinatorAsync(s.LeaderA, s.CsrfA, 1);
        await CreateCoordinatorAsync(s.LeaderA, s.CsrfA, 2);

        HttpResponseMessage duplicateSlot = await s.LeaderA.PostAsync(Coordinators, new { fullName = "Dup", directionId = 1 }, s.CsrfA);
        Assert.Equal("coordinator_slot_taken", await ManagementSteps.AssertStatusAsync(duplicateSlot, HttpStatusCode.Conflict));
        await CreateCoordinatorAsync(s.LeaderB, s.CsrfB, 1); // same direction in another faculty is fine

        foreach (int badDirection in new[] { 0, 8 })
        {
            HttpResponseMessage invalid = await s.LeaderA.PostAsync(Coordinators, new { fullName = "Bad", directionId = badDirection }, s.CsrfA);
            Assert.Equal("validation_failed", await ManagementSteps.AssertStatusAsync(invalid, HttpStatusCode.BadRequest));
        }

        HttpResponseMessage missingFields = await s.LeaderA.PostAsync(Coordinators, new { }, s.CsrfA);
        JsonElement errors = (await missingFields.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.Equal(HttpStatusCode.BadRequest, missingFields.StatusCode);
        Assert.True(errors.TryGetProperty("fullName", out _) && errors.TryGetProperty("directionId", out _));

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
    public async Task Deactivation_frees_the_slot_hides_the_profile_and_preserves_reports_reviews_and_audit()
    {
        Scenario s = await ArrangeAsync();
        string id = (await CreateCoordinatorAsync(s.LeaderA, s.CsrfA, 1)).GetProperty("id").GetString()!;

        // A report from before coordinators became profiles must survive.
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
        Assert.Equal(JsonValueKind.Null, (await PublicSlotAsync(s.FacultyA, 1)).GetProperty("coordinatorName").ValueKind);

        HttpResponseMessage edit = await s.LeaderA.SendJsonAsync(HttpMethod.Put, $"{Coordinators}/{id}",
            new { fullName = "Edited", directionId = 1, version = inactive.GetProperty("version").GetUInt32() }, s.CsrfA);
        Assert.Equal("user_inactive", await ManagementSteps.AssertStatusAsync(edit, HttpStatusCode.Conflict));

        (int reports, int reviews, bool audited) = await _factory.WithDbAsync(async (db) =>
            (await db.Reports.CountAsync((Report r) => r.AuthorId == coordinatorId),
             await db.ReportReviews.CountAsync((ReportReview r) => r.ReportId == reportId),
             await db.AuditLogs.AnyAsync((AuditLog a) => a.Action == "user.deactivated" && a.EntityId == id && a.ActorId == s.LeaderAId)));
        Assert.Equal((1, 1, true), (reports, reviews, audited));

        // The slot is free for a successor; the deactivated coordinator only appears when asked for.
        await CreateCoordinatorAsync(s.LeaderA, s.CsrfA, 1);
        Assert.Equal(1, (await s.LeaderA.GetFromJsonAsync<JsonElement>(Coordinators)).GetArrayLength());
        Assert.Equal(2, (await s.LeaderA.GetFromJsonAsync<JsonElement>($"{Coordinators}?includeInactive=true")).GetArrayLength());
    }

    [Fact]
    public async Task Photos_are_validated_scoped_served_publicly_and_withdrawn_when_replaced_or_deactivated()
    {
        Scenario s = await ArrangeAsync();
        JsonElement coordinator = await CreateCoordinatorAsync(s.LeaderA, s.CsrfA, 1);
        string url = $"{Coordinators}/{coordinator.GetProperty("id").GetString()}";
        HttpClient anonymous = _factory.CreateApiClient();

        Assert.Equal("validation_failed", await ManagementSteps.AssertStatusAsync(
            await UploadPhotoAsync(s.LeaderA, s.CsrfA, $"{url}/photo", System.Text.Encoding.ASCII.GetBytes("%PDF-1.7 not an image"), "image/png"), HttpStatusCode.BadRequest));
        Assert.Equal("validation_failed", await ManagementSteps.AssertStatusAsync(
            await UploadPhotoAsync(s.LeaderA, s.CsrfA, $"{url}/photo", Png.Concat(new byte[1024 * 1024]).ToArray(), "image/png"), HttpStatusCode.BadRequest));

        HttpResponseMessage uploaded = await UploadPhotoAsync(s.LeaderA, s.CsrfA, $"{url}/photo", Png, "image/jpeg"); // type comes from the bytes
        Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
        string firstPhoto = (await uploaded.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("photoId").GetString()!;
        Assert.Equal(firstPhoto, (await PublicSlotAsync(s.FacultyA, 1)).GetProperty("photoId").GetString());

        HttpResponseMessage served = await anonymous.GetAsync($"/api/public/photos/{firstPhoto}");
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
        Assert.Equal("image/png", served.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Png, await served.Content.ReadAsByteArrayAsync());
        Assert.Contains("immutable", served.Headers.CacheControl?.ToString());
        Assert.Equal("nosniff", served.Headers.GetValues("X-Content-Type-Options").Single());

        // Replacing creates a new public id and retires the old one.
        string secondPhoto = (await (await UploadPhotoAsync(s.LeaderA, s.CsrfA, $"{url}/photo", Jpeg, "image/jpeg")).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("photoId").GetString()!;
        Assert.NotEqual(firstPhoto, secondPhoto);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/public/photos/{firstPhoto}")).StatusCode);
        Assert.Equal(1, await _factory.WithDbAsync((db) => db.ProfilePhotos.CountAsync()));

        // The University Leader sets faculty leaders' photos; they appear on the faculty card.
        HttpResponseMessage leaderPhoto = await UploadPhotoAsync(s.Admin, s.AdminCsrf, $"/api/admin/faculty-leaders/{s.LeaderAId}/photo", Png, "image/png");
        Assert.Equal(HttpStatusCode.OK, leaderPhoto.StatusCode);
        JsonElement facultyCard = (await anonymous.GetFromJsonAsync<JsonElement>("/api/public/directory")).GetProperty("faculties").EnumerateArray()
            .Single((JsonElement f) => f.GetProperty("id").GetInt32() == s.FacultyA);
        Assert.Equal((await leaderPhoto.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("photoId").GetString(), facultyCard.GetProperty("leaderPhotoId").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await UploadPhotoAsync(s.LeaderA, s.CsrfA, $"/api/admin/faculty-leaders/{s.LeaderAId}/photo", Png, "image/png")).StatusCode);

        // The University Leader sets only their own photo (account from the session); it is featured in the directory.
        HttpResponseMessage ownPhoto = await UploadPhotoAsync(s.Admin, s.AdminCsrf, "/api/admin/profile/photo", Jpeg, "image/jpeg");
        Assert.Equal(HttpStatusCode.OK, ownPhoto.StatusCode);
        string ownPhotoId = (await ownPhoto.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("photoId").GetString()!;
        Assert.Equal(ownPhotoId, (await anonymous.GetFromJsonAsync<JsonElement>("/api/public/directory")).GetProperty("universityLeaderPhotoId").GetString());
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync($"/api/public/photos/{ownPhotoId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await UploadPhotoAsync(s.LeaderA, s.CsrfA, "/api/admin/profile/photo", Png, "image/png")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await s.Admin.SendJsonAsync(HttpMethod.Delete, "/api/admin/profile/photo", null, s.AdminCsrf)).StatusCode);
        Assert.Equal(JsonValueKind.Null, (await anonymous.GetFromJsonAsync<JsonElement>("/api/public/directory")).GetProperty("universityLeaderPhotoId").ValueKind);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/public/photos/{ownPhotoId}")).StatusCode);

        // Deactivated profiles stop serving their photo.
        uint version = (await s.LeaderA.GetFromJsonAsync<JsonElement>(url)).GetProperty("version").GetUInt32();
        Assert.Equal(HttpStatusCode.OK, (await s.LeaderA.PostAsync($"{url}/deactivate", new { version }, s.CsrfA)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/public/photos/{secondPhoto}")).StatusCode);
        Assert.True(await _factory.WithDbAsync((db) => db.AuditLogs.AnyAsync((AuditLog a) => a.Action == "user.photo_changed" && a.ActorId == s.LeaderAId)));
    }

    private sealed record Scenario(
        HttpClient Admin, string AdminCsrf, int FacultyA, int FacultyB,
        HttpClient LeaderA, string CsrfA, Guid LeaderAId, HttpClient LeaderB, string CsrfB);

    /// <summary>University leader, two faculties, one faculty leader each (passwords already changed).</summary>
    [Fact]
    public async Task University_leader_manages_own_seven_coordinators_shown_on_the_public_directory()
    {
        const string University = "/api/admin/university-coordinators";
        Scenario s = await ArrangeAsync();

        HttpResponseMessage created = await s.Admin.PostAsync(University,
            new { fullName = "Uni Coordinator", directionId = 3, facultyId = s.FacultyA, phone = "+998 90 765-43-21", phoneIsPublic = true }, s.AdminCsrf);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        JsonElement user = await created.Content.ReadFromJsonAsync<JsonElement>();
        string id = user.GetProperty("id").GetString()!;
        Assert.Equal("UniversityCoordinator", user.GetProperty("role").GetString());
        Assert.Equal(JsonValueKind.Null, user.GetProperty("facultyId").ValueKind); // a forged facultyId is ignored
        Assert.False(user.GetProperty("hasLogin").GetBoolean());

        // One active coordinator per direction; a direction outside 1-7 is rejected.
        Assert.Equal("coordinator_slot_taken", await ManagementSteps.AssertStatusAsync(
            await s.Admin.PostAsync(University, new { fullName = "Second", directionId = 3 }, s.AdminCsrf), HttpStatusCode.Conflict));
        Assert.Equal("validation_failed", await ManagementSteps.AssertStatusAsync(
            await s.Admin.PostAsync(University, new { fullName = "Bad", directionId = 99 }, s.AdminCsrf), HttpStatusCode.BadRequest));

        JsonElement directory = await _factory.CreateApiClient().GetFromJsonAsync<JsonElement>("/api/public/directory");
        JsonElement slots = directory.GetProperty("universityCoordinators");
        Assert.Equal(7, slots.GetArrayLength());
        JsonElement slot = slots.EnumerateArray().Single((JsonElement item) => item.GetProperty("directionId").GetInt32() == 3);
        Assert.Equal("Uni Coordinator", slot.GetProperty("coordinatorName").GetString());
        Assert.Equal("+998 90 765-43-21", slot.GetProperty("phone").GetString());
        Assert.All(directory.GetProperty("faculties").EnumerateArray(), (JsonElement faculty) => Assert.All(faculty.GetProperty("coordinators").EnumerateArray(),
            (JsonElement item) => Assert.Equal(JsonValueKind.Null, item.GetProperty("coordinatorName").ValueKind))); // not a faculty coordinator

        HttpResponseMessage edited = await s.Admin.SendJsonAsync(HttpMethod.Put, $"{University}/{id}",
            new { fullName = "Moved Coordinator", directionId = 5, version = user.GetProperty("version").GetUInt32() }, s.AdminCsrf);
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        user = await edited.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(5, user.GetProperty("directionId").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await UploadPhotoAsync(s.Admin, s.AdminCsrf, $"{University}/{id}/photo", Png, "image/png")).StatusCode);

        // Faculty leaders cannot manage them, and the faculty coordinator API cannot reach them.
        Assert.Equal(HttpStatusCode.Forbidden, (await s.LeaderA.GetAsync(University)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.LeaderA.GetAsync($"{Coordinators}/{id}")).StatusCode);

        user = await s.Admin.GetFromJsonAsync<JsonElement>($"{University}/{id}");
        HttpResponseMessage deactivated = await s.Admin.PostAsync($"{University}/{id}/deactivate", new { version = user.GetProperty("version").GetUInt32() }, s.AdminCsrf);
        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);
        directory = await _factory.CreateApiClient().GetFromJsonAsync<JsonElement>("/api/public/directory");
        Assert.All(directory.GetProperty("universityCoordinators").EnumerateArray(),
            (JsonElement item) => Assert.Equal(JsonValueKind.Null, item.GetProperty("coordinatorName").ValueKind));
    }

    [Fact]
    public async Task Leader_phones_are_public_only_with_consent()
    {
        Scenario s = await ArrangeAsync();
        string leaders = "/api/admin/faculty-leaders";
        JsonElement leader = await s.Admin.GetFromJsonAsync<JsonElement>($"{leaders}/{s.LeaderAId}");
        HttpResponseMessage edited = await s.Admin.SendJsonAsync(HttpMethod.Put, $"{leaders}/{s.LeaderAId}",
            new { fullName = "Leader A", facultyId = s.FacultyA, phone = "+998 91 111-22-33", phoneIsPublic = true, version = leader.GetProperty("version").GetUInt32() }, s.AdminCsrf);
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);

        HttpResponseMessage own = await s.Admin.SendJsonAsync(HttpMethod.Put, "/api/admin/profile/contact", new { phone = "+998 90 000-00-01", phoneIsPublic = true }, s.AdminCsrf);
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal("+998 90 000-00-01", (await s.Admin.GetFromJsonAsync<JsonElement>("/api/admin/profile")).GetProperty("phone").GetString());
        Assert.Equal("validation_failed", await ManagementSteps.AssertStatusAsync(
            await s.Admin.SendJsonAsync(HttpMethod.Put, "/api/admin/profile/contact", new { phone = "call me", phoneIsPublic = true }, s.AdminCsrf), HttpStatusCode.BadRequest));
        Assert.Equal(HttpStatusCode.Forbidden, (await s.LeaderA.SendJsonAsync(HttpMethod.Put, "/api/admin/profile/contact", new { phone = "+998 90 000-00-02" }, s.CsrfA)).StatusCode);

        JsonElement directory = await _factory.CreateApiClient().GetFromJsonAsync<JsonElement>("/api/public/directory");
        Assert.Equal("+998 90 000-00-01", directory.GetProperty("universityLeaderPhone").GetString());
        JsonElement facultyA = directory.GetProperty("faculties").EnumerateArray().Single((JsonElement f) => f.GetProperty("id").GetInt32() == s.FacultyA);
        Assert.Equal("+998 91 111-22-33", facultyA.GetProperty("leaderPhone").GetString());

        // Withdrawn consent hides the phone publicly but keeps it for management.
        leader = await edited.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, (await s.Admin.SendJsonAsync(HttpMethod.Put, $"{leaders}/{s.LeaderAId}",
            new { fullName = "Leader A", facultyId = s.FacultyA, phone = "+998 91 111-22-33", phoneIsPublic = false, version = leader.GetProperty("version").GetUInt32() }, s.AdminCsrf)).StatusCode);
        directory = await _factory.CreateApiClient().GetFromJsonAsync<JsonElement>("/api/public/directory");
        facultyA = directory.GetProperty("faculties").EnumerateArray().Single((JsonElement f) => f.GetProperty("id").GetInt32() == s.FacultyA);
        Assert.Equal(JsonValueKind.Null, facultyA.GetProperty("leaderPhone").ValueKind);
        Assert.Equal("+998 91 111-22-33", (await s.Admin.GetFromJsonAsync<JsonElement>($"{leaders}/{s.LeaderAId}")).GetProperty("phone").GetString());
    }

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

    /// <summary>Creates a coordinator profile in the caller's faculty and returns it.</summary>
    private static async Task<JsonElement> CreateCoordinatorAsync(HttpClient leader, string csrf, int directionId)
    {
        HttpResponseMessage response = await leader.PostAsync(Coordinators, new { fullName = "Coordinator", directionId }, csrf);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<JsonElement> PublicSlotAsync(int facultyId, int directionId)
    {
        JsonElement directory = await _factory.CreateApiClient().GetFromJsonAsync<JsonElement>("/api/public/directory");
        return directory.GetProperty("faculties").EnumerateArray().Single((JsonElement f) => f.GetProperty("id").GetInt32() == facultyId)
            .GetProperty("coordinators").EnumerateArray().Single((JsonElement d) => d.GetProperty("directionId").GetInt32() == directionId);
    }

    private static async Task<HttpResponseMessage> UploadPhotoAsync(HttpClient client, string csrf, string url, byte[] bytes, string contentType)
    {
        using MultipartFormDataContent form = new MultipartFormDataContent();
        ByteArrayContent file = new ByteArrayContent(bytes);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        form.Add(file, "file", "photo");
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, url) { Content = form };
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        return await client.SendAsync(request);
    }
}
