using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Tests.E2e.Helpers;

namespace Tests.E2e.Controllers;

public class MatchesControllerTests : E2eTestBase
{
    public MatchesControllerTests(WebAppFactory factory) : base(factory) { }

    // ── GET /api/matches ──────────────────────────────────────────────────

    [Fact]
    public async Task GetMatches_Unauthenticated_Returns401()
    {
        var resp = await Client.GetAsync("/api/matches");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMatches_NoSkillsNoRequests_ReturnsEmptyList()
    {
        var (_, token) = await RegisterAndLoginAsync();
        var resp       = await AuthorizedClient(token).GetAsync("/api/matches");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.EnumerateArray().Should().BeEmpty();
    }

    [Fact]
    public async Task GetMatches_MutualSkillExchange_ReturnsMatch()
    {
        // User A has Python, wants Java
        // User B has Java, wants Python
        // They should match each other

        var pythonId = await SeedSkillAsync($"Python_{Guid.NewGuid():N}");
        var javaId   = await SeedSkillAsync($"Java_{Guid.NewGuid():N}");

        var (_, tokenA) = await RegisterAndLoginAsync();
        var (_, tokenB) = await RegisterAndLoginAsync();
        var clientA     = AuthorizedClient(tokenA);
        var clientB     = AuthorizedClient(tokenB);

        // A: add Python skill, create Java request
        await clientA.PostAsJsonAsync("/api/userskills", new { SkillID = pythonId, Level = 1 });
        await clientA.PostAsJsonAsync("/api/skillrequests", new { SkillID = javaId, Title = "Need Java" });

        // B: add Java skill, create Python request
        await clientB.PostAsJsonAsync("/api/userskills", new { SkillID = javaId, Level = 1 });
        await clientB.PostAsJsonAsync("/api/skillrequests", new { SkillID = pythonId, Title = "Need Python" });

        var resp = await clientA.GetAsync("/api/matches");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        body.EnumerateArray().Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task GetMatches_UserWithSkillOtherWants_ReturnsMatch()
    {
        var skillId     = await SeedSkillAsync($"Skill_{Guid.NewGuid():N}");
        var (_, tokenA) = await RegisterAndLoginAsync();
        var (_, tokenB) = await RegisterAndLoginAsync();
        var clientA     = AuthorizedClient(tokenA);
        var clientB     = AuthorizedClient(tokenB);

        // A has the skill
        await clientA.PostAsJsonAsync("/api/userskills", new { SkillID = skillId, Level = 2 });

        // B wants the skill
        await clientB.PostAsJsonAsync("/api/skillrequests", new { SkillID = skillId, Title = "Learn it" });

        // A should match B
        var resp = await clientA.GetAsync("/api/matches");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        body.EnumerateArray().Should().HaveCountGreaterThan(0);
    }
}
