using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Tests.E2e.Helpers;

namespace Tests.E2e.Controllers;

public class SkillsControllerTests : E2eTestBase
{
    public SkillsControllerTests(WebAppFactory factory) : base(factory) { }

    // ── GET /api/skills ───────────────────────────────────────────────────

    [Fact]
    public async Task GetSkills_Anonymous_Returns200()
    {
        var resp = await Client.GetAsync("/api/skills");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSkills_ReturnsSeededSkills()
    {
        await SeedSkillAsync($"UniqueSkill_{Guid.NewGuid():N}");

        var resp = await Client.GetAsync("/api/skills");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetSkills_SearchFilter_Works()
    {
        var uniqueName = $"SearchMe_{Guid.NewGuid():N}";
        await SeedSkillAsync(uniqueName);

        var resp = await Client.GetAsync($"/api/skills?search={uniqueName}");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("totalCount").GetInt32().Should().Be(1);
        body.GetProperty("items")[0].GetProperty("skillName").GetString()
            .Should().Be(uniqueName);
    }

    // ── POST /api/skills (Admin only) ─────────────────────────────────────

    [Fact]
    public async Task CreateSkill_AsAdmin_Returns201()
    {
        var (_, adminToken) = await RegisterAndLoginAdminAsync();
        var client          = AuthorizedClient(adminToken);

        var resp = await client.PostAsJsonAsync("/api/skills",
            new { SkillName = $"AdminSkill_{Guid.NewGuid():N}", Epithet = 0 }); // 0 = IT

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateSkill_AsRegularUser_Returns403()
    {
        var (_, token) = await RegisterAndLoginAsync();
        var client     = AuthorizedClient(token);

        var resp = await client.PostAsJsonAsync("/api/skills",
            new { SkillName = "UnauthorizedSkill", Epithet = 0 });

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateSkill_Unauthenticated_Returns401()
    {
        var resp = await Client.PostAsJsonAsync("/api/skills",
            new { SkillName = "AnonSkill", Epithet = 0 });

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── DELETE /api/skills/{id} (Admin only) ──────────────────────────────

    [Fact]
    public async Task DeleteSkill_AsAdmin_Returns204()
    {
        var (_, adminToken) = await RegisterAndLoginAdminAsync();
        var client          = AuthorizedClient(adminToken);

        var skillId = await SeedSkillAsync($"ToDelete_{Guid.NewGuid():N}");

        var resp = await client.DeleteAsync($"/api/skills/{skillId}");

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteSkill_AsRegularUser_Returns403()
    {
        var skillId    = await SeedSkillAsync($"Protected_{Guid.NewGuid():N}");
        var (_, token) = await RegisterAndLoginAsync();
        var client     = AuthorizedClient(token);

        var resp = await client.DeleteAsync($"/api/skills/{skillId}");

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
