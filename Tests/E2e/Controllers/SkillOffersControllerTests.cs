using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Tests.E2e.Helpers;

namespace Tests.E2e.Controllers;

public class SkillOffersControllerTests : E2eTestBase
{
    public SkillOffersControllerTests(WebAppFactory factory) : base(factory) { }

    // ── GET /api/skilloffers ──────────────────────────────────────────────

    [Fact]
    public async Task GetOffers_Anonymous_Returns200()
    {
        var resp = await Client.GetAsync("/api/skilloffers");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetOffers_ReturnsCreatedOffer()
    {
        var skillId          = await SeedSkillAsync($"SO_{Guid.NewGuid():N}");
        var (ownerId, token) = await RegisterAndLoginAsync();
        await SeedUserSkillAsync(ownerId, skillId);
        var client           = AuthorizedClient(token);

        var title = $"Offer_{Guid.NewGuid():N}";
        await client.PostAsJsonAsync("/api/skilloffers",
            new { SkillID = skillId, Title = title });

        var resp = await Client.GetAsync($"/api/skilloffers?search={title}");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("totalCount").GetInt32().Should().Be(1);
    }

    // ── GET /api/skilloffers/{id} ─────────────────────────────────────────

    [Fact]
    public async Task GetOfferById_Existing_Returns200()
    {
        var skillId          = await SeedSkillAsync($"SO2_{Guid.NewGuid():N}");
        var (ownerId, token) = await RegisterAndLoginAsync();
        await SeedUserSkillAsync(ownerId, skillId);
        var client           = AuthorizedClient(token);

        var createResp = await client.PostAsJsonAsync("/api/skilloffers",
            new { SkillID = skillId, Title = "Test offer" });
        var body    = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var offerId = body.GetProperty("id").GetGuid();

        var resp = await Client.GetAsync($"/api/skilloffers/{offerId}");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetOfferById_Unknown_Returns404()
    {
        var resp = await Client.GetAsync($"/api/skilloffers/{Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /api/skilloffers ─────────────────────────────────────────────

    [Fact]
    public async Task CreateOffer_Valid_Returns201WithId()
    {
        var skillId          = await SeedSkillAsync($"SO3_{Guid.NewGuid():N}");
        var (ownerId, token) = await RegisterAndLoginAsync();
        await SeedUserSkillAsync(ownerId, skillId);
        var client           = AuthorizedClient(token);

        var resp = await client.PostAsJsonAsync("/api/skilloffers",
            new { SkillID = skillId, Title = "My offer", Details = "Details here" });

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("id").GetGuid().Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateOffer_Unauthenticated_Returns401()
    {
        var skillId = await SeedSkillAsync();
        var resp    = await Client.PostAsJsonAsync("/api/skilloffers",
            new { SkillID = skillId, Title = "Anon offer" });

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateOffer_UnknownSkill_Returns404()
    {
        var (_, token) = await RegisterAndLoginAsync();
        var client     = AuthorizedClient(token);

        var resp = await client.PostAsJsonAsync("/api/skilloffers",
            new { SkillID = Guid.NewGuid(), Title = "Offer with bad skill" });

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── PUT /api/skilloffers/{id} ─────────────────────────────────────────

    [Fact]
    public async Task UpdateOffer_Owner_Returns204()
    {
        var skillId          = await SeedSkillAsync($"SO4_{Guid.NewGuid():N}");
        var (ownerId, token) = await RegisterAndLoginAsync();
        await SeedUserSkillAsync(ownerId, skillId);
        var client           = AuthorizedClient(token);

        var createResp = await client.PostAsJsonAsync("/api/skilloffers",
            new { SkillID = skillId, Title = "Old title" });
        var offerId    = (await createResp.Content.ReadFromJsonAsync<JsonElement>())
                         .GetProperty("id").GetGuid();

        var resp = await client.PutAsJsonAsync($"/api/skilloffers/{offerId}",
            new { Title = "New title", IsActive = true });

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateOffer_NonOwner_Returns403()
    {
        var skillId            = await SeedSkillAsync($"SO5_{Guid.NewGuid():N}");
        var (ownerId, ownerToken) = await RegisterAndLoginAsync();
        var (_, otherToken)    = await RegisterAndLoginAsync();
        await SeedUserSkillAsync(ownerId, skillId);
        var ownerClient        = AuthorizedClient(ownerToken);
        var otherClient        = AuthorizedClient(otherToken);

        var createResp = await ownerClient.PostAsJsonAsync("/api/skilloffers",
            new { SkillID = skillId, Title = "Owner's offer" });
        var offerId    = (await createResp.Content.ReadFromJsonAsync<JsonElement>())
                         .GetProperty("id").GetGuid();

        var resp = await otherClient.PutAsJsonAsync($"/api/skilloffers/{offerId}",
            new { Title = "Hijacked", IsActive = false });

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── DELETE /api/skilloffers/{id} ──────────────────────────────────────

    [Fact]
    public async Task DeleteOffer_Owner_Returns204()
    {
        var skillId          = await SeedSkillAsync($"SO6_{Guid.NewGuid():N}");
        var (ownerId, token) = await RegisterAndLoginAsync();
        await SeedUserSkillAsync(ownerId, skillId);
        var client           = AuthorizedClient(token);

        var createResp = await client.PostAsJsonAsync("/api/skilloffers",
            new { SkillID = skillId, Title = "To delete" });
        var offerId    = (await createResp.Content.ReadFromJsonAsync<JsonElement>())
                         .GetProperty("id").GetGuid();

        var resp = await client.DeleteAsync($"/api/skilloffers/{offerId}");

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteOffer_NonOwner_Returns403()
    {
        var skillId               = await SeedSkillAsync($"SO7_{Guid.NewGuid():N}");
        var (ownerId, ownerToken) = await RegisterAndLoginAsync();
        var (_, otherToken)       = await RegisterAndLoginAsync();
        await SeedUserSkillAsync(ownerId, skillId);
        var ownerClient           = AuthorizedClient(ownerToken);
        var otherClient           = AuthorizedClient(otherToken);

        var createResp = await ownerClient.PostAsJsonAsync("/api/skilloffers",
            new { SkillID = skillId, Title = "Protected offer" });
        var offerId    = (await createResp.Content.ReadFromJsonAsync<JsonElement>())
                         .GetProperty("id").GetGuid();

        var resp = await otherClient.DeleteAsync($"/api/skilloffers/{offerId}");

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
