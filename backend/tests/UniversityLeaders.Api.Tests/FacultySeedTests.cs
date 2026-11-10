using System.Net.Http.Json;
using System.Text.Json;

namespace UniversityLeaders.Api.Tests;

/// <summary>The migrations seed the 13 confirmed faculties; the public site and the ranking always show all of them.</summary>
public sealed class FacultySeedTests : IAsyncLifetime
{
    private static readonly string[] ExpectedNames =
    {
        "Matematika fakulteti",
        "Amaliy matematika va intellektual texnologiyalar fakulteti",
        "Fizika fakulteti",
        "Kimyo fakulteti",
        "Biologiya va ekologiya fakulteti",
        "Geologiya va muhandislik geologiyasi fakulteti",
        "Geografiya va tabiiy resurslar fakulteti",
        "Tarix fakulteti",
        "Iqtisodiyot fakulteti",
        "Ijtimoiy fanlar fakulteti",
        "Xorijiy filologiya fakulteti",
        "Jurnalistika va oʻzbek filologiyasi fakulteti",
        "Taekvondo va sport faoliyati fakulteti",
    };

    private readonly AuthApiFactory _factory = new AuthApiFactory { KeepSeedData = true };

    public Task InitializeAsync() => _factory.InitializeAsync();

    public Task DisposeAsync() => ((IAsyncLifetime)_factory).DisposeAsync();

    [Fact]
    public async Task Thirteen_confirmed_faculties_are_seeded_in_order_and_always_listed_publicly_and_in_the_ranking()
    {
        HttpClient anonymous = _factory.CreateApiClient();
        JsonElement[] faculties = (await anonymous.GetFromJsonAsync<JsonElement>("/api/public/directory"))
            .GetProperty("faculties").EnumerateArray().ToArray();

        Assert.Equal(ExpectedNames, faculties.Select((JsonElement f) => f.GetProperty("name").GetString()));
        Assert.Equal("Sun'iy intellekt va axborot xavfsizligi yo‘nalishlari.", faculties[1].GetProperty("description").GetString());
        Assert.Equal("Falsafa, sotsiologiya, psixologiya va siyosatshunoslik.", faculties[9].GetProperty("description").GetString());
        Assert.Equal(11, faculties.Count((JsonElement f) => f.GetProperty("description").ValueKind == JsonValueKind.Null));
        Assert.All(faculties, (JsonElement f) =>
        {
            Assert.Equal(JsonValueKind.Null, f.GetProperty("leaderName").ValueKind); // vacant card, not a missing one
            Assert.Equal(7, f.GetProperty("coordinators").GetArrayLength());
        });

        JsonElement[] ranking = (await anonymous.GetFromJsonAsync<JsonElement>("/api/public/faculty-rankings")).EnumerateArray().ToArray();
        Assert.Equal(13, ranking.Length);
        Assert.All(ranking, (JsonElement r) =>
        {
            Assert.Equal(0, r.GetProperty("totalScore").GetInt32());
            Assert.Equal(0, r.GetProperty("approvedReports").GetInt32());
        });

        // The 13-faculty limit holds: no fourteenth faculty can be added.
        (HttpClient admin, string csrf) = await _factory.SignInUniversityLeaderAsync();
        HttpResponseMessage fourteenth = await admin.PostAsync("/api/admin/faculties", new { name = "Yangi fakultet" }, csrf);
        Assert.Equal("faculty_limit_reached", await ManagementSteps.AssertStatusAsync(fourteenth, System.Net.HttpStatusCode.Conflict));
    }
}
