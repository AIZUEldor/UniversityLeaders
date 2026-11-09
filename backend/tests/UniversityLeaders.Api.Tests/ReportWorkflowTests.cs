using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UniversityLeaders.Domain.Entities;
using UniversityLeaders.Domain.Enums;

namespace UniversityLeaders.Api.Tests;

/// <summary>Report submission, routing, review and public visibility. Fresh database per test.</summary>
public sealed class ReportWorkflowTests : IAsyncLifetime
{
    private const string Reports = "/api/reports";

    private readonly AuthApiFactory _factory = new AuthApiFactory();

    public Task InitializeAsync() => _factory.InitializeAsync();

    public Task DisposeAsync() => ((IAsyncLifetime)_factory).DisposeAsync();

    [Fact]
    public async Task Coordinator_report_goes_draft_submitted_rejected_resubmitted_approved_and_is_then_immutable()
    {
        Cast c = await Cast.ArrangeAsync(_factory);

        JsonElement draft = await CreateAsync(c.CoordinatorA1, "Volunteer day", complete: false);
        JsonElement summary = draft.GetProperty("summary");
        string id = summary.GetProperty("id").GetString()!;
        Assert.Equal("Draft", summary.GetProperty("status").GetString());
        Assert.Equal(c.FacultyA, summary.GetProperty("facultyId").GetInt32()); // derived from the author
        Assert.Equal(1, summary.GetProperty("directionId").GetInt32());

        // Drafts are private and incomplete drafts cannot be submitted.
        Assert.Equal(HttpStatusCode.NotFound, (await c.LeaderA.Client.GetAsync($"{Reports}/{id}")).StatusCode);
        HttpResponseMessage incomplete = await Send(c.CoordinatorA1, HttpMethod.Post, $"{Reports}/{id}/submit", new { version = Version(draft) });
        Assert.Equal("validation_failed", await ManagementSteps.AssertStatusAsync(incomplete, HttpStatusCode.BadRequest));

        JsonElement edited = await ReadAsync(await Send(c.CoordinatorA1, HttpMethod.Put, $"{Reports}/{id}", Content("Volunteer day", Version(draft))));
        JsonElement submitted = await ReadAsync(await Send(c.CoordinatorA1, HttpMethod.Post, $"{Reports}/{id}/submit", new { version = Version(edited) }));
        Assert.Equal("Submitted", submitted.GetProperty("summary").GetProperty("status").GetString());
        HttpResponseMessage editWhileSubmitted = await Send(c.CoordinatorA1, HttpMethod.Put, $"{Reports}/{id}", Content("Changed", Version(submitted)));
        Assert.Equal("report_locked", await ManagementSteps.AssertStatusAsync(editWhileSubmitted, HttpStatusCode.Conflict));

        // Routed to the own faculty leader only.
        Assert.Contains(id, await c.LeaderA.Client.GetStringAsync($"{Reports}/review-queue"));
        Assert.DoesNotContain(id, await c.Admin.Client.GetStringAsync($"{Reports}/review-queue"));

        HttpResponseMessage rejectWithoutReason = await Send(c.LeaderA, HttpMethod.Post, $"{Reports}/{id}/reject", new { version = Version(submitted) });
        Assert.Equal("validation_failed", await ManagementSteps.AssertStatusAsync(rejectWithoutReason, HttpStatusCode.BadRequest));
        JsonElement rejected = await ReadAsync(await Send(c.LeaderA, HttpMethod.Post, $"{Reports}/{id}/reject", new { version = Version(submitted), comment = "Add participant numbers." }));
        Assert.Equal("Rejected", rejected.GetProperty("summary").GetProperty("status").GetString());

        // The author sees the private explanation, edits and resubmits.
        JsonElement authorView = await c.CoordinatorA1.Client.GetFromJsonAsync<JsonElement>($"{Reports}/{id}");
        Assert.Equal("Add participant numbers.", authorView.GetProperty("reviews")[0].GetProperty("comment").GetString());
        JsonElement fixedDraft = await ReadAsync(await Send(c.CoordinatorA1, HttpMethod.Put, $"{Reports}/{id}", Content("Volunteer day (120 people)", Version(rejected), participants: 120)));
        JsonElement resubmitted = await ReadAsync(await Send(c.CoordinatorA1, HttpMethod.Post, $"{Reports}/{id}/submit", new { version = Version(fixedDraft) }));

        HttpResponseMessage badBreakdown = await Send(c.LeaderA, HttpMethod.Post, $"{Reports}/{id}/approve",
            new { version = Version(resubmitted), score = 8, scoreBreakdown = new[] { new { criterion = "Impact", points = 5 } } });
        Assert.Equal("validation_failed", await ManagementSteps.AssertStatusAsync(badBreakdown, HttpStatusCode.BadRequest));
        HttpResponseMessage outOfRange = await Send(c.LeaderA, HttpMethod.Post, $"{Reports}/{id}/approve",
            new { version = Version(resubmitted), score = 11, scoreBreakdown = new[] { new { criterion = "Impact", points = 11 } } });
        Assert.Equal(HttpStatusCode.BadRequest, outOfRange.StatusCode);

        JsonElement approved = await ReadAsync(await Send(c.LeaderA, HttpMethod.Post, $"{Reports}/{id}/approve", Approval(Version(resubmitted), 8)));
        Assert.Equal("Approved", approved.GetProperty("summary").GetProperty("status").GetString());
        Assert.Equal(8, approved.GetProperty("summary").GetProperty("score").GetInt32());
        JsonElement reviews = approved.GetProperty("reviews");
        Assert.Equal(2, reviews.GetArrayLength());
        Assert.Equal(c.LeaderA.Id.ToString(), reviews[1].GetProperty("reviewerId").GetString());
        Assert.Equal(2, reviews[1].GetProperty("scoreBreakdown").GetArrayLength());

        // Approved is final for every normal endpoint.
        Assert.Equal("report_locked", await ManagementSteps.AssertStatusAsync(
            await Send(c.CoordinatorA1, HttpMethod.Put, $"{Reports}/{id}", Content("Rewrite", Version(approved))), HttpStatusCode.Conflict));
        Assert.Equal("report_locked", await ManagementSteps.AssertStatusAsync(
            await Send(c.CoordinatorA1, HttpMethod.Post, $"{Reports}/{id}/submit", new { version = Version(approved) }), HttpStatusCode.Conflict));
        Assert.Equal("report_not_submitted", await ManagementSteps.AssertStatusAsync(
            await Send(c.LeaderA, HttpMethod.Post, $"{Reports}/{id}/reject", new { version = Version(approved), comment = "Too late" }), HttpStatusCode.Conflict));

        List<string> audit = await _factory.WithDbAsync((db) =>
            db.AuditLogs.Where((AuditLog a) => a.EntityId == id).OrderBy((AuditLog a) => a.Id).Select((AuditLog a) => a.Action).ToListAsync());
        Assert.Equal(new[] { "report.created", "report.updated", "report.submitted", "report.rejected", "report.updated", "report.submitted", "report.approved" }, audit);
    }

    [Fact]
    public async Task Reports_are_isolated_between_faculties_and_authors()
    {
        Cast c = await Cast.ArrangeAsync(_factory);
        JsonElement report = await SubmitNewAsync(c.CoordinatorA1, "Faculty A work");
        string id = report.GetProperty("summary").GetProperty("id").GetString()!;
        uint version = Version(report);

        foreach (Actor outsider in new[] { c.LeaderB, c.CoordinatorA2, c.CoordinatorB1, c.UniversityCoordinator, c.Admin })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await outsider.Client.GetAsync($"{Reports}/{id}")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.NotFound, (await Send(c.LeaderB, HttpMethod.Post, $"{Reports}/{id}/approve", Approval(version, 5))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(c.Admin, HttpMethod.Post, $"{Reports}/{id}/approve", Approval(version, 5))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(c.CoordinatorA2, HttpMethod.Put, $"{Reports}/{id}", Content("Hijack", version))).StatusCode);
        Assert.Equal(0, (await c.LeaderB.Client.GetFromJsonAsync<JsonElement>($"{Reports}/review-queue")).GetArrayLength());
        Assert.Equal(HttpStatusCode.Forbidden, (await c.CoordinatorA2.Client.GetAsync($"{Reports}/review-queue")).StatusCode);

        // Not public until approved.
        Assert.DoesNotContain(id, await _factory.CreateApiClient().GetStringAsync("/api/public/completed-work"));
    }

    [Fact]
    public async Task Faculty_leader_and_university_coordinator_reports_go_to_the_university_leader()
    {
        Cast c = await Cast.ArrangeAsync(_factory);
        JsonElement leaderReport = await SubmitNewAsync(c.LeaderA, "Faculty-wide forum");
        JsonElement ucReport = await SubmitNewAsync(c.UniversityCoordinator, "University festival");
        string leaderReportId = leaderReport.GetProperty("summary").GetProperty("id").GetString()!;
        string ucReportId = ucReport.GetProperty("summary").GetProperty("id").GetString()!;
        Assert.Equal(c.FacultyA, leaderReport.GetProperty("summary").GetProperty("facultyId").GetInt32());
        Assert.Equal(JsonValueKind.Null, ucReport.GetProperty("summary").GetProperty("facultyId").ValueKind);
        Assert.Equal(3, ucReport.GetProperty("summary").GetProperty("directionId").GetInt32());

        string adminQueue = await c.Admin.Client.GetStringAsync($"{Reports}/review-queue");
        Assert.Contains(leaderReportId, adminQueue);
        Assert.Contains(ucReportId, adminQueue);
        Assert.DoesNotContain(leaderReportId, await c.LeaderA.Client.GetStringAsync($"{Reports}/review-queue")); // no self-review

        HttpResponseMessage selfApprove = await Send(c.LeaderA, HttpMethod.Post, $"{Reports}/{leaderReportId}/approve", Approval(Version(leaderReport), 9));
        Assert.Equal(HttpStatusCode.NotFound, selfApprove.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(c.Admin, HttpMethod.Post, $"{Reports}/{leaderReportId}/approve", Approval(Version(leaderReport), 9))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(c.Admin, HttpMethod.Post, $"{Reports}/{ucReportId}/reject", new { version = Version(ucReport), comment = "Needs photos." })).StatusCode);

        HttpResponseMessage adminCreates = await Send(c.Admin, HttpMethod.Post, Reports, Content("Admin report", null));
        Assert.Equal("forbidden", await ManagementSteps.AssertStatusAsync(adminCreates, HttpStatusCode.Forbidden));
    }

    [Fact]
    public async Task Concurrent_approvals_produce_exactly_one_review_and_a_consistent_status()
    {
        Cast c = await Cast.ArrangeAsync(_factory);
        JsonElement report = await SubmitNewAsync(c.CoordinatorA1, "Contested report");
        string id = report.GetProperty("summary").GetProperty("id").GetString()!;
        uint version = Version(report);

        HttpResponseMessage[] responses = await Task.WhenAll(Enumerable.Range(0, 5).Select((int i) =>
            Send(c.LeaderA, HttpMethod.Post, $"{Reports}/{id}/approve", Approval(version, 4 + i))));

        Assert.Single(responses, (HttpResponseMessage r) => r.StatusCode == HttpStatusCode.OK);
        Assert.All(responses.Where((HttpResponseMessage r) => r.StatusCode != HttpStatusCode.OK),
            (HttpResponseMessage r) => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));

        (int reviews, ReportStatus status, int? score, int? approvedReviewScore) = await _factory.WithDbAsync(async (db) =>
        {
            Report stored = await db.Reports.AsNoTracking().SingleAsync((Report r) => r.Id == Guid.Parse(id));
            List<ReportReview> rows = await db.ReportReviews.AsNoTracking().Where((ReportReview r) => r.ReportId == stored.Id).ToListAsync();
            return (rows.Count, stored.Status, stored.Score, rows.Single().Score);
        });
        Assert.Equal(1, reviews);
        Assert.Equal(ReportStatus.Approved, status);
        Assert.Equal(approvedReviewScore, score); // report and review agree on the winning score
    }

    [Fact]
    public async Task Only_approved_work_is_public_without_private_data_and_rankings_ignore_university_coordinators()
    {
        Cast c = await Cast.ArrangeAsync(_factory);
        string approvedId = await ApproveNewAsync(c.CoordinatorA1, c.LeaderA, "Approved faculty work", 7);
        string ucApprovedId = await ApproveNewAsync(c.UniversityCoordinator, c.Admin, "Approved university work", 9);
        JsonElement pending = await SubmitNewAsync(c.CoordinatorB1, "Pending work");
        await CreateAsync(c.CoordinatorA2, "Draft work", complete: true);

        HttpClient anonymous = _factory.CreateApiClient();
        HttpResponseMessage response = await anonymous.GetAsync("/api/public/completed-work");
        string json = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement page = JsonDocument.Parse(json).RootElement;
        Assert.Equal(2, page.GetProperty("total").GetInt32());
        Assert.Contains(approvedId, json);
        Assert.Contains(ucApprovedId, json);
        Assert.DoesNotContain(pending.GetProperty("summary").GetProperty("id").GetString()!, json);
        foreach (string privateField in new[] { "comment", "review", "breakdown", "attachment", "storageKey", "version" })
        {
            Assert.DoesNotContain(privateField, json, StringComparison.OrdinalIgnoreCase);
        }

        JsonElement facultyFilter = await anonymous.GetFromJsonAsync<JsonElement>($"/api/public/completed-work?facultyId={c.FacultyA}");
        Assert.Equal(1, facultyFilter.GetProperty("total").GetInt32());

        JsonElement ranking = await anonymous.GetFromJsonAsync<JsonElement>("/api/public/faculty-rankings");
        Assert.Equal(2, ranking.GetArrayLength()); // every faculty, university coordinator work excluded
        Assert.Equal(c.FacultyA, ranking[0].GetProperty("facultyId").GetInt32());
        Assert.Equal(7, ranking[0].GetProperty("totalScore").GetInt32());
        Assert.Equal(1, ranking[0].GetProperty("approvedReports").GetInt32());
        Assert.Equal(0, ranking[1].GetProperty("totalScore").GetInt32());

        // Deactivating the author keeps the work and its historical author identity.
        JsonElement coordinator = await c.LeaderA.Client.GetFromJsonAsync<JsonElement>($"/api/faculty/coordinators/{c.CoordinatorA1.Id}");
        HttpResponseMessage deactivated = await Send(c.LeaderA, HttpMethod.Post, $"/api/faculty/coordinators/{c.CoordinatorA1.Id}/deactivate",
            new { version = coordinator.GetProperty("version").GetUInt32() });
        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);

        string afterDeactivation = await anonymous.GetStringAsync("/api/public/completed-work");
        Assert.Contains(approvedId, afterDeactivation);
        Assert.Contains("Coordinator A1", afterDeactivation);
        JsonElement leaderView = await c.LeaderA.Client.GetFromJsonAsync<JsonElement>($"{Reports}/{approvedId}");
        Assert.False(leaderView.GetProperty("summary").GetProperty("authorIsActive").GetBoolean());
        Assert.Equal(c.CoordinatorA1.Id.ToString(), leaderView.GetProperty("summary").GetProperty("authorId").GetString());
    }

    // ------------------------------------------------------------------ helpers

    private static object Content(string title, uint? version, int? participants = 25)
    {
        return new { title, description = "What happened", activityDate = "2026-10-15", outcome = "What it achieved", participantCount = participants, version };
    }

    private static object Approval(uint version, int score)
    {
        int first = score / 2;
        return new { version, score, comment = "Well done", scoreBreakdown = new[] { new { criterion = "Impact", points = first }, new { criterion = "Reach", points = score - first } } };
    }

    private static Task<HttpResponseMessage> Send(Actor actor, HttpMethod method, string url, object body)
    {
        return actor.Client.SendJsonAsync(method, url, body, actor.Csrf);
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static uint Version(JsonElement detail)
    {
        return detail.GetProperty("summary").GetProperty("version").GetUInt32();
    }

    private static async Task<JsonElement> CreateAsync(Actor author, string title, bool complete)
    {
        object body = complete ? Content(title, null) : new { title };
        HttpResponseMessage created = await Send(author, HttpMethod.Post, Reports, body);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return await created.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> SubmitNewAsync(Actor author, string title)
    {
        JsonElement draft = await CreateAsync(author, title, complete: true);
        string id = draft.GetProperty("summary").GetProperty("id").GetString()!;
        return await ReadAsync(await Send(author, HttpMethod.Post, $"{Reports}/{id}/submit", new { version = Version(draft) }));
    }

    private static async Task<string> ApproveNewAsync(Actor author, Actor reviewer, string title, int score)
    {
        JsonElement submitted = await SubmitNewAsync(author, title);
        string id = submitted.GetProperty("summary").GetProperty("id").GetString()!;
        await ReadAsync(await Send(reviewer, HttpMethod.Post, $"{Reports}/{id}/approve", Approval(Version(submitted), score)));
        return id;
    }
}
