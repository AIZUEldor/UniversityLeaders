using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using UniversityLeaders.Application.Attachments;
using UniversityLeaders.Application.Common;
using UniversityLeaders.Domain.Entities;

namespace UniversityLeaders.Api.Tests;

/// <summary>Report attachments against an isolated MinIO bucket and database per test.</summary>
public sealed class AttachmentTests : IAsyncLifetime
{
    private const string Reports = "/api/reports";

    private readonly AuthApiFactory _factory = new AuthApiFactory();

    public async Task InitializeAsync()
    {
        await _factory.InitializeAsync();
    }

    public Task DisposeAsync() => ((IAsyncLifetime)_factory).DisposeAsync();

    [Fact]
    public async Task Accepts_only_real_jpeg_png_pdf_and_docx_and_stores_them_under_random_keys()
    {
        Cast c = await Cast.ArrangeAsync(_factory);
        string reportId = await CreateDraftAsync(c.LeaderA);

        (string Name, byte[] Bytes, string Type)[] valid =
        {
            ("photo.JPG", Files.Jpeg(), "image/jpeg"),
            ("chart.png", Files.Png(), "image/png"),
            ("minutes.pdf", Files.Pdf(), "application/pdf"),
            ("letter.docx", Files.Docx(), "application/octet-stream"),
        };
        foreach ((string name, byte[] bytes, string type) in valid)
        {
            HttpResponseMessage response = await UploadAsync(c.LeaderA, reportId, name, bytes, type);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        (string Name, byte[] Bytes, string Type, string Why)[] invalid =
        {
            ("notes.png", Encoding.UTF8.GetBytes("just text"), "image/png", "content is not a PNG"),
            ("photo.pdf", Files.Png(), "application/pdf", "PNG renamed to .pdf"),
            ("chart.png", Files.Png(), "application/pdf", "declared type contradicts content"),
            ("tool.exe", Files.Png(), "application/octet-stream", "unsupported extension"),
            ("archive.docx", Files.Zip(("readme.txt", "hi")), DocxType, "zip that is not a Word document"),
            ("macro.docx", Files.Docx(withMacros: true), DocxType, "macro-enabled document"),
            ("script.pdf", Files.Pdf("/OpenAction << /S /JavaScript /JS (app.alert(1)) >>"), "application/pdf", "PDF with JavaScript"),
            ("truncated.pdf", Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj"), "application/pdf", "PDF without end marker"),
            ("empty.png", Array.Empty<byte>(), "image/png", "empty file"),
        };
        foreach ((string name, byte[] bytes, string type, string why) in invalid)
        {
            HttpResponseMessage response = await UploadAsync(c.LeaderA, reportId, name, bytes, type);
            Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"{why}: expected 400, got {(int)response.StatusCode}");
            Assert.Equal("validation_failed", await response.ErrorCodeAsync());
        }

        List<ReportAttachment> rows = await _factory.WithDbAsync((db) => db.ReportAttachments.AsNoTracking().OrderBy((ReportAttachment a) => a.UploadedAt).ToListAsync());
        IReadOnlyList<string> keys = await _factory.ListStoredKeysAsync();
        Assert.Equal(4, rows.Count);
        Assert.Equal(rows.Select((ReportAttachment a) => a.StorageKey).OrderBy((string k) => k), keys.OrderBy((string k) => k)); // nothing stored for rejected files
        Assert.Equal(new[] { "image/jpeg", "image/png", "application/pdf", DocxType }, rows.Select((ReportAttachment a) => a.ContentType));
        foreach (ReportAttachment row in rows)
        {
            Assert.Matches("^reports/[0-9a-f]{32}/[0-9a-f]{32}$", row.StorageKey);
            Assert.DoesNotContain(Path.GetFileNameWithoutExtension(row.FileName), row.StorageKey, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal(valid[2].Bytes, await _factory.ReadStoredObjectAsync(rows[2].StorageKey));
    }

    [Fact]
    public async Task Enforces_ten_megabytes_per_file_and_ten_files_per_report()
    {
        Cast c = await Cast.ArrangeAsync(_factory);
        string reportId = await CreateDraftAsync(c.LeaderA);

        byte[] tooBig = Files.Pdf(padding: (int)AttachmentRules.MaxSizeBytes); // just over 10 MB
        HttpResponseMessage oversized = await UploadAsync(c.LeaderA, reportId, "big.pdf", tooBig, "application/pdf");
        Assert.Equal("validation_failed", await ManagementSteps.AssertStatusAsync(oversized, HttpStatusCode.BadRequest));

        byte[] wayTooBig = new byte[12 * 1024 * 1024];
        HttpResponseMessage request413 = await UploadAsync(c.LeaderA, reportId, "huge.png", wayTooBig, "image/png");
        Assert.Equal("payload_too_large", await ManagementSteps.AssertStatusAsync(request413, HttpStatusCode.RequestEntityTooLarge));

        for (int i = 1; i <= AttachmentRules.MaxFilesPerReport; i++)
        {
            Assert.Equal(HttpStatusCode.Created, (await UploadAsync(c.LeaderA, reportId, $"p{i}.png", Files.Png(), "image/png")).StatusCode);
        }

        HttpResponseMessage eleventh = await UploadAsync(c.LeaderA, reportId, "p11.png", Files.Png(), "image/png");
        Assert.Equal("attachment_limit_reached", await ManagementSteps.AssertStatusAsync(eleventh, HttpStatusCode.Conflict));
        Assert.Equal(AttachmentRules.MaxFilesPerReport, (await _factory.ListStoredKeysAsync()).Count);
    }

    [Fact]
    public async Task Uploads_stop_at_the_total_storage_quota_without_storing_anything()
    {
        Cast c = await Cast.ArrangeAsync(_factory);
        string reportId = await CreateDraftAsync(c.LeaderA);
        await UploadOkAsync(c.LeaderA, reportId, "first.pdf", Files.Pdf());
        // Pretend the stored files already fill the quota (only the recorded size counts, so no large upload is needed).
        await _factory.WithDbAsync((Infrastructure.Persistence.AppDbContext db) => db.ReportAttachments
            .ExecuteUpdateAsync(set =>
                set.SetProperty((ReportAttachment a) => a.SizeBytes, AttachmentRules.MaxTotalBytes)));

        HttpResponseMessage full = await UploadAsync(c.LeaderA, reportId, "second.png", Files.Png(), "image/png");
        Assert.Equal("storage_quota_reached", await ManagementSteps.AssertStatusAsync(full, HttpStatusCode.Conflict));
        Assert.Single(await _factory.ListStoredKeysAsync());
    }

    [Fact]
    public async Task Only_the_author_changes_files_and_only_author_and_responsible_reviewer_can_read_them()
    {
        Cast c = await Cast.ArrangeAsync(_factory);
        string reportId = await CreateDraftAsync(c.LeaderA);
        byte[] pdf = Files.Pdf();
        string attachmentId = await UploadOkAsync(c.LeaderA, reportId, "evidence.pdf", pdf);
        string download = $"{Reports}/{reportId}/attachments/{attachmentId}";

        // Draft: private to the author (not even the reviewer sees it).
        Assert.Equal(HttpStatusCode.NotFound, (await c.Admin.Client.GetAsync(download)).StatusCode);
        await SubmitAsync(c.LeaderA, reportId);

        // Responsible reviewer can list and download, with safe headers.
        JsonElement list = await c.Admin.Client.GetFromJsonAsync<JsonElement>($"{Reports}/{reportId}/attachments");
        Assert.Equal(1, list.GetArrayLength());
        Assert.DoesNotContain("storageKey", list.GetRawText(), StringComparison.OrdinalIgnoreCase);
        HttpResponseMessage file = await c.Admin.Client.GetAsync(download);
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        Assert.Equal(pdf, await file.Content.ReadAsByteArrayAsync());
        Assert.Equal("attachment", file.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("evidence.pdf", file.Content.Headers.ContentDisposition?.FileNameStar);
        Assert.Equal("application/pdf", file.Content.Headers.ContentType?.MediaType);
        Assert.Equal("nosniff", file.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("sandbox", file.Headers.GetValues("Content-Security-Policy").Single());
        Assert.True(file.Headers.CacheControl?.NoStore);

        // Everyone else: indistinguishable from a missing report.
        foreach (Actor outsider in new[] { c.LeaderB })
        {
            HttpResponseMessage denied = await outsider.Client.GetAsync(download);
            Assert.Equal("report_not_found", await ManagementSteps.AssertStatusAsync(denied, HttpStatusCode.NotFound));
            Assert.Equal(HttpStatusCode.NotFound, (await outsider.Client.GetAsync($"{Reports}/{reportId}/attachments")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateApiClient().GetAsync(download)).StatusCode);
        HttpResponseMessage unknown = await c.LeaderB.Client.GetAsync($"{Reports}/{Guid.NewGuid()}/attachments/{attachmentId}");
        Assert.Equal("report_not_found", await unknown.ErrorCodeAsync());

        // The reviewer may read but never change the evidence.
        Assert.Equal(HttpStatusCode.Forbidden, (await UploadAsync(c.Admin, reportId, "extra.png", Files.Png(), "image/png")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.Admin.Client.SendJsonAsync(HttpMethod.Delete, download, null, c.Admin.Csrf)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await UploadAsync(c.LeaderB, reportId, "intruder.png", Files.Png(), "image/png")).StatusCode);
    }

    [Fact]
    public async Task Attachments_are_frozen_while_submitted_and_after_approval_but_editable_after_rejection()
    {
        Cast c = await Cast.ArrangeAsync(_factory);
        string reportId = await CreateDraftAsync(c.LeaderA);
        string first = await UploadOkAsync(c.LeaderA, reportId, "first.pdf", Files.Pdf());
        uint version = await SubmitAsync(c.LeaderA, reportId);

        Assert.Equal("report_locked", await ManagementSteps.AssertStatusAsync(await UploadAsync(c.LeaderA, reportId, "late.png", Files.Png(), "image/png"), HttpStatusCode.Conflict));
        Assert.Equal("report_locked", await ManagementSteps.AssertStatusAsync(
            await c.LeaderA.Client.SendJsonAsync(HttpMethod.Delete, $"{Reports}/{reportId}/attachments/{first}", null, c.LeaderA.Csrf), HttpStatusCode.Conflict));

        HttpResponseMessage rejected = await c.Admin.Client.SendJsonAsync(HttpMethod.Post, $"{Reports}/{reportId}/reject", new { version, comment = "Wrong file." }, c.Admin.Csrf);
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);

        // Rejected: the author can replace evidence; removal deletes metadata and the stored object.
        string firstKey = await _factory.WithDbAsync((db) => db.ReportAttachments.Where((ReportAttachment a) => a.Id == Guid.Parse(first)).Select((ReportAttachment a) => a.StorageKey).SingleAsync());
        HttpResponseMessage removed = await c.LeaderA.Client.SendJsonAsync(HttpMethod.Delete, $"{Reports}/{reportId}/attachments/{first}", null, c.LeaderA.Csrf);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.DoesNotContain(firstKey, await _factory.ListStoredKeysAsync());
        string second = await UploadOkAsync(c.LeaderA, reportId, "second.pdf", Files.Pdf());

        uint resubmitted = await SubmitAsync(c.LeaderA, reportId);
        HttpResponseMessage approved = await c.Admin.Client.SendJsonAsync(HttpMethod.Post, $"{Reports}/{reportId}/approve",
            new { version = resubmitted, score = 6, scoreBreakdown = new[] { new { criterion = "Evidence", points = 6 } } }, c.Admin.Csrf);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);

        Assert.Equal("report_locked", await ManagementSteps.AssertStatusAsync(await UploadAsync(c.LeaderA, reportId, "after.png", Files.Png(), "image/png"), HttpStatusCode.Conflict));
        Assert.Equal("report_locked", await ManagementSteps.AssertStatusAsync(
            await c.LeaderA.Client.SendJsonAsync(HttpMethod.Delete, $"{Reports}/{reportId}/attachments/{second}", null, c.LeaderA.Csrf), HttpStatusCode.Conflict));

        List<string> audit = await _factory.WithDbAsync((db) =>
            db.AuditLogs.Where((AuditLog a) => a.EntityId == reportId && a.Action.StartsWith("attachment.")).OrderBy((AuditLog a) => a.Id).Select((AuditLog a) => a.Action).ToListAsync());
        Assert.Equal(new[] { "attachment.added", "attachment.removed", "attachment.added" }, audit);

        // Public views never expose attachments.
        string publicJson = await _factory.CreateApiClient().GetStringAsync("/api/public/completed-work");
        Assert.Contains(reportId, publicJson);
        Assert.DoesNotContain("second.pdf", publicJson);
    }

    [Fact]
    public async Task Storage_outage_and_failed_database_commit_leave_no_metadata_and_no_orphaned_object()
    {
        Cast c = await Cast.ArrangeAsync(_factory);
        string reportId = await CreateDraftAsync(c.LeaderA);

        // 1) Object storage down: 503, nothing recorded.
        RecordingStorage down = new RecordingStorage(failPut: true);
        HttpClient downClient = await LoginThroughAsync(down, "fl.a");
        HttpResponseMessage unavailable = await UploadAsync(downClient, await downClient.GetCsrfTokenAsync(), reportId, "a.pdf", Files.Pdf(), "application/pdf");
        Assert.Equal("storage_unavailable", await ManagementSteps.AssertStatusAsync(unavailable, HttpStatusCode.ServiceUnavailable));

        // 2) Object stored, then the database commit loses a race (report changed meanwhile): object is deleted again.
        RecordingStorage racing = new RecordingStorage(failPut: false, afterPut: () => TouchReportAsync(reportId));
        HttpClient racingClient = await LoginThroughAsync(racing, "fl.a");
        HttpResponseMessage conflict = await UploadAsync(racingClient, await racingClient.GetCsrfTokenAsync(), reportId, "b.pdf", Files.Pdf(), "application/pdf");
        Assert.Equal("concurrent_update", await ManagementSteps.AssertStatusAsync(conflict, HttpStatusCode.Conflict));
        Assert.Single(racing.Put);
        Assert.Equal(racing.Put, racing.Deleted); // compensation removed exactly the stored object

        Assert.Equal(0, await _factory.WithDbAsync((db) => db.ReportAttachments.CountAsync()));
        Assert.Empty(await _factory.ListStoredKeysAsync());
    }

    // ------------------------------------------------------------------ helpers

    private const string DocxType = AttachmentInspector.DocxContentType;

    private static async Task<string> CreateDraftAsync(Actor author)
    {
        HttpResponseMessage created = await author.Client.SendJsonAsync(HttpMethod.Post, Reports,
            new { title = "Report with evidence", description = "What happened", activityDate = "2026-10-20", outcome = "Result", participantCount = 10 }, author.Csrf);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("summary").GetProperty("id").GetString()!;
    }

    private static async Task<uint> SubmitAsync(Actor author, string reportId)
    {
        JsonElement current = await author.Client.GetFromJsonAsync<JsonElement>($"{Reports}/{reportId}");
        HttpResponseMessage submitted = await author.Client.SendJsonAsync(HttpMethod.Post, $"{Reports}/{reportId}/submit",
            new { version = current.GetProperty("summary").GetProperty("version").GetUInt32() }, author.Csrf);
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        return (await submitted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("summary").GetProperty("version").GetUInt32();
    }

    private static Task<HttpResponseMessage> UploadAsync(Actor actor, string reportId, string fileName, byte[] bytes, string contentType)
    {
        return UploadAsync(actor.Client, actor.Csrf, reportId, fileName, bytes, contentType);
    }

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, string csrf, string reportId, string fileName, byte[] bytes, string contentType)
    {
        using MultipartFormDataContent form = new MultipartFormDataContent();
        ByteArrayContent file = new ByteArrayContent(bytes);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        form.Add(file, "file", fileName);
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, $"{Reports}/{reportId}/attachments") { Content = form };
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        return await client.SendAsync(request);
    }

    private static async Task<string> UploadOkAsync(Actor actor, string reportId, string fileName, byte[] bytes)
    {
        HttpResponseMessage response = await UploadAsync(actor, reportId, fileName, bytes, "application/pdf");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    /// <summary>A second API host on the same test database whose storage is replaced by <paramref name="storage"/>.</summary>
    private async Task<HttpClient> LoginThroughAsync(IAttachmentStorage storage, string userName)
    {
        WebApplicationFactory<Program> host = _factory.WithWebHostBuilder((Microsoft.AspNetCore.Hosting.IWebHostBuilder builder) =>
            builder.ConfigureTestServices((IServiceCollection services) =>
            {
                services.RemoveAll<IAttachmentStorage>();
                services.AddSingleton(storage);
            }));
        HttpClient client = host.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true });
        (HttpResponseMessage login, _) = await client.LoginAsync(userName, ManagementSteps.PermanentPassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return client;
    }

    /// <summary>Changes the report row out of band, so the upload's guarded commit fails on the xmin check.</summary>
    private async Task TouchReportAsync(string reportId)
    {
        await using NpgsqlConnection connection = new NpgsqlConnection(_factory.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new NpgsqlCommand("UPDATE reports SET updated_at = now() WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", Guid.Parse(reportId));
        await command.ExecuteNonQueryAsync();
    }

    private sealed class RecordingStorage : IAttachmentStorage
    {
        private readonly bool _failPut;
        private readonly Func<Task>? _afterPut;

        public RecordingStorage(bool failPut, Func<Task>? afterPut = null)
        {
            _failPut = failPut;
            _afterPut = afterPut;
        }

        public List<string> Put { get; } = new List<string>();

        public List<string> Deleted { get; } = new List<string>();

        public async Task PutAsync(string storageKey, Stream content, long length, string contentType, CancellationToken cancellationToken)
        {
            if (_failPut)
            {
                throw new ServiceUnavailableException("storage_unavailable", "File storage is temporarily unavailable. Please retry.");
            }

            Put.Add(storageKey);
            if (_afterPut is not null)
            {
                await _afterPut();
            }
        }

        public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken) => Task.FromResult<Stream?>(null);

        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
        {
            Deleted.Add(storageKey);
            return Task.CompletedTask;
        }
    }

    /// <summary>Minimal but structurally valid sample files.</summary>
    private static class Files
    {
        public static byte[] Jpeg() => new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0xFF, 0xD9 };

        public static byte[] Png() => new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01 };

        public static byte[] Pdf(string extra = "", int padding = 0)
        {
            string body = "%PDF-1.7\n1 0 obj << /Type /Catalog " + extra + " >> endobj\n";
            return Encoding.ASCII.GetBytes(body + new string(' ', padding) + "\ntrailer << /Root 1 0 R >>\n%%EOF\n");
        }

        public static byte[] Docx(bool withMacros = false)
        {
            List<(string, string)> parts = new List<(string, string)>
            {
                ("[Content_Types].xml", "<?xml version=\"1.0\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"/>"),
                ("_rels/.rels", "<?xml version=\"1.0\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"/>"),
                ("word/document.xml", "<?xml version=\"1.0\"?><w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body/></w:document>"),
            };
            if (withMacros)
            {
                parts.Add(("word/vbaProject.bin", "binary macro"));
            }

            return Zip(parts.ToArray());
        }

        public static byte[] Zip(params (string Name, string Content)[] entries)
        {
            using MemoryStream buffer = new MemoryStream();
            using (ZipArchive zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach ((string name, string content) in entries)
                {
                    using StreamWriter writer = new StreamWriter(zip.CreateEntry(name).Open());
                    writer.Write(content);
                }
            }

            return buffer.ToArray();
        }
    }
}
