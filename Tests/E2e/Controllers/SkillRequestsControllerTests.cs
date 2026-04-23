using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Tests.E2e.Helpers;

namespace Tests.E2e.Controllers;

public class SkillRequestsControllerTests : E2eTestBase
{
    public SkillRequestsControllerTests(WebAppFactory factory) : base(factory) { }

    // ── GET /api/skillrequests ────────────────────────────────────────────

    [Fact]
    public async Task GetRequests_Anonymous_Returns200()
    {
        var resp = await Client.GetAsync("/api/skillrequests");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── GET /api/skillrequests/{id} ───────────────────────────────────────

    [Fact]
    public async Task GetById_Existing_Returns200()
    {
        var skillId    = await SeedSkillAsync($"SR_{Guid.NewGuid():N}");
        var (_, token) = await RegisterAndLoginAsync();
        var client     = AuthorizedClient(token);

        var createResp = await client.PostAsJsonAsync("/api/skillrequests",
            new { SkillID = skillId, Title = "Need help" });
        var requestId  = (await createResp.Content.ReadFromJsonAsync<JsonElement>())
                         .GetProperty("id").GetGuid();

        var resp = await Client.GetAsync($"/api/skillrequests/{requestId}");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetById_Unknown_Returns404()
    {
        var resp = await Client.GetAsync($"/api/skillrequests/{Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /api/skillrequests ───────────────────────────────────────────

    [Fact]
    public async Task CreateRequest_Valid_Returns201()
    {
        var skillId    = await SeedSkillAsync($"SR2_{Guid.NewGuid():N}");
        var (_, token) = await RegisterAndLoginAsync();
        var client     = AuthorizedClient(token);

        var resp = await client.PostAsJsonAsync("/api/skillrequests",
            new { SkillID = skillId, Title = "Teach me Python" });

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("id").GetGuid().Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateRequest_Unauthenticated_Returns401()
    {
        var skillId = await SeedSkillAsync();
        var resp    = await Client.PostAsJsonAsync("/api/skillrequests",
            new { SkillID = skillId, Title = "Anon request" });

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateRequest_EmptyTitle_Returns400()
    {
        var skillId    = await SeedSkillAsync();
        var (_, token) = await RegisterAndLoginAsync();
        var client     = AuthorizedClient(token);

        var resp = await client.PostAsJsonAsync("/api/skillrequests",
            new { SkillID = skillId, Title = "" });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── PUT /api/skillrequests/{id} (update status) ───────────────────────

    [Fact]
    public async Task UpdateStatus_Owner_Returns204()
    {
        var skillId    = await SeedSkillAsync($"SR3_{Guid.NewGuid():N}");
        var (_, token) = await RegisterAndLoginAsync();
        var client     = AuthorizedClient(token);

        var createResp = await client.PostAsJsonAsync("/api/skillrequests",
            new { SkillID = skillId, Title = "Status test" });
        var requestId  = (await createResp.Content.ReadFromJsonAsync<JsonElement>())
                         .GetProperty("id").GetGuid();

        var resp = await client.PutAsJsonAsync($"/api/skillrequests/{requestId}",
            new { Status = 1 }); // 1 = Closed

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateStatus_NonOwner_Returns403()
    {
        var skillId         = await SeedSkillAsync($"SR4_{Guid.NewGuid():N}");
        var (_, ownerToken) = await RegisterAndLoginAsync();
        var (_, otherToken) = await RegisterAndLoginAsync();
        var ownerClient     = AuthorizedClient(ownerToken);
        var otherClient     = AuthorizedClient(otherToken);

        var createResp = await ownerClient.PostAsJsonAsync("/api/skillrequests",
            new { SkillID = skillId, Title = "My request" });
        var requestId  = (await createResp.Content.ReadFromJsonAsync<JsonElement>())
                         .GetProperty("id").GetGuid();

        var resp = await otherClient.PutAsJsonAsync($"/api/skillrequests/{requestId}",
            new { Status = 1 });

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── DELETE /api/skillrequests/{id} ────────────────────────────────────

    [Fact]
    public async Task DeleteRequest_Owner_Returns204()
    {
        var skillId    = await SeedSkillAsync($"SR5_{Guid.NewGuid():N}");
        var (_, token) = await RegisterAndLoginAsync();
        var client     = AuthorizedClient(token);

        var createResp = await client.PostAsJsonAsync("/api/skillrequests",
            new { SkillID = skillId, Title = "To delete" });
        var requestId  = (await createResp.Content.ReadFromJsonAsync<JsonElement>())
                         .GetProperty("id").GetGuid();

        var resp = await client.DeleteAsync($"/api/skillrequests/{requestId}");

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
