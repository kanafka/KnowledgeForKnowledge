using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Tests.E2e.Helpers;

namespace Tests.E2e.Controllers;

public class UserSkillsControllerTests : E2eTestBase
{
    public UserSkillsControllerTests(WebAppFactory factory) : base(factory) { }

    // ── GET /api/userskills/{accountId} ───────────────────────────────────

    [Fact]
    public async Task GetUserSkills_Anonymous_Returns200()
    {
        var (id, _) = await RegisterAndLoginAsync();
        var resp    = await Client.GetAsync($"/api/userskills/{id}");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetUserSkills_ReturnsAddedSkill()
    {
        var skillId    = await SeedSkillAsync($"US_{Guid.NewGuid():N}");
        var (id, token) = await RegisterAndLoginAsync();
        var client      = AuthorizedClient(token);

        await client.PostAsJsonAsync("/api/userskills",
            new { SkillID = skillId, Level = 1 }); // Middle

        var resp = await Client.GetAsync($"/api/userskills/{id}");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        body.EnumerateArray().Should().HaveCount(1);
    }

    // ── POST /api/userskills ──────────────────────────────────────────────

    [Fact]
    public async Task AddSkill_Valid_Returns204()
    {
        var skillId    = await SeedSkillAsync($"US2_{Guid.NewGuid():N}");
        var (_, token) = await RegisterAndLoginAsync();

        var resp = await AuthorizedClient(token).PostAsJsonAsync("/api/userskills",
            new { SkillID = skillId, Level = 0 }); // Junior

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AddSkill_Unauthenticated_Returns401()
    {
        var skillId = await SeedSkillAsync();
        var resp    = await Client.PostAsJsonAsync("/api/userskills",
            new { SkillID = skillId, Level = 1 });

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AddSkill_EmptySkillId_Returns400()
    {
        var (_, token) = await RegisterAndLoginAsync();

        var resp = await AuthorizedClient(token).PostAsJsonAsync("/api/userskills",
            new { SkillID = Guid.Empty, Level = 1 });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── DELETE /api/userskills/{skillId} ──────────────────────────────────

    [Fact]
    public async Task RemoveSkill_Existing_Returns204()
    {
        var skillId    = await SeedSkillAsync($"US3_{Guid.NewGuid():N}");
        var (_, token) = await RegisterAndLoginAsync();
        var client     = AuthorizedClient(token);

        await client.PostAsJsonAsync("/api/userskills", new { SkillID = skillId, Level = 1 });

        var resp = await client.DeleteAsync($"/api/userskills/{skillId}");

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RemoveSkill_NotOwned_Returns404()
    {
        var skillId    = await SeedSkillAsync($"US4_{Guid.NewGuid():N}");
        var (_, token) = await RegisterAndLoginAsync();

        // Don't add the skill, just try to remove it
        var resp = await AuthorizedClient(token).DeleteAsync($"/api/userskills/{skillId}");

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
