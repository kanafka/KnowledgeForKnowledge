using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Tests.E2e.Helpers;

namespace Tests.E2e.Controllers;

public class EducationControllerTests : E2eTestBase
{
    public EducationControllerTests(WebAppFactory factory) : base(factory) { }

    // ── GET /api/education/{accountId} ────────────────────────────────────

    [Fact]
    public async Task GetEducation_Authenticated_Returns200()
    {
        var (id, token) = await RegisterAndLoginAsync();
        var resp        = await AuthorizedClient(token).GetAsync($"/api/education/{id}");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetEducation_ReturnsAddedEntry()
    {
        var (id, token) = await RegisterAndLoginAsync();
        var client      = AuthorizedClient(token);

        await client.PostAsJsonAsync("/api/education",
            new { InstitutionName = "MIT", DegreeField = "CS", YearCompleted = 2020 });

        var resp = await client.GetAsync($"/api/education/{id}");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        body.EnumerateArray().Should().HaveCount(1);
        body[0].GetProperty("institutionName").GetString().Should().Be("MIT");
    }

    // ── POST /api/education ───────────────────────────────────────────────

    [Fact]
    public async Task AddEducation_Valid_Returns201()
    {
        var (_, token) = await RegisterAndLoginAsync();

        var resp = await AuthorizedClient(token).PostAsJsonAsync("/api/education",
            new { InstitutionName = "Stanford", DegreeField = "ML" });

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task AddEducation_Unauthenticated_Returns401()
    {
        var resp = await Client.PostAsJsonAsync("/api/education",
            new { InstitutionName = "Harvard" });

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── DELETE /api/education/{id} ────────────────────────────────────────

    [Fact]
    public async Task DeleteEducation_Owner_Returns204()
    {
        var (id, token) = await RegisterAndLoginAsync();
        var client      = AuthorizedClient(token);

        var createResp = await client.PostAsJsonAsync("/api/education",
            new { InstitutionName = "Oxford" });
        var body       = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var eduId      = body.GetProperty("id").GetGuid();

        var resp = await client.DeleteAsync($"/api/education/{eduId}");

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteEducation_NonOwner_Returns403()
    {
        var (_, ownerToken) = await RegisterAndLoginAsync();
        var (_, otherToken) = await RegisterAndLoginAsync();
        var ownerClient     = AuthorizedClient(ownerToken);

        var createResp = await ownerClient.PostAsJsonAsync("/api/education",
            new { InstitutionName = "Cambridge" });
        var body  = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var eduId = body.GetProperty("id").GetGuid();

        var resp = await AuthorizedClient(otherToken).DeleteAsync($"/api/education/{eduId}");

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteEducation_NotFound_Returns404()
    {
        var (_, token) = await RegisterAndLoginAsync();

        var resp = await AuthorizedClient(token).DeleteAsync($"/api/education/{Guid.NewGuid()}");

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
