using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Tests.E2e.Helpers;

namespace Tests.E2e.Controllers;

public class ApplicationsControllerTests : E2eTestBase
{
    public ApplicationsControllerTests(WebAppFactory factory) : base(factory) { }

    // Helper: creates offer owner + offer, returns (offerId, ownerToken, applicantToken)
    private async Task<(Guid offerId, string ownerToken, string applicantToken)> SetupOfferAsync()
    {
        var skillId                    = await SeedSkillAsync($"App_{Guid.NewGuid():N}");
        var (ownerId, ownerToken)      = await RegisterAndLoginAsync();
        var (_, applicantToken)        = await RegisterAndLoginAsync();
        await SeedUserSkillAsync(ownerId, skillId);
        var ownerClient                = AuthorizedClient(ownerToken);

        var createResp = await ownerClient.PostAsJsonAsync("/api/skilloffers",
            new { SkillID = skillId, Title = "Test offer" });
        var offerId    = (await createResp.Content.ReadFromJsonAsync<JsonElement>())
                         .GetProperty("id").GetGuid();

        return (offerId, ownerToken, applicantToken);
    }

    // ── GET /api/applications/incoming ────────────────────────────────────

    [Fact]
    public async Task GetIncoming_Authenticated_Returns200()
    {
        var (_, token) = await RegisterAndLoginAsync();
        var resp       = await AuthorizedClient(token).GetAsync("/api/applications/incoming");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetIncoming_Unauthenticated_Returns401()
    {
        var resp = await Client.GetAsync("/api/applications/incoming");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET /api/applications/outgoing ────────────────────────────────────

    [Fact]
    public async Task GetOutgoing_ReturnsApplicationAfterApply()
    {
        var (offerId, _, applicantToken) = await SetupOfferAsync();
        var applicantClient = AuthorizedClient(applicantToken);

        await applicantClient.PostAsJsonAsync("/api/applications",
            new { OfferID = offerId, SkillRequestID = (Guid?)null });

        var resp = await applicantClient.GetAsync("/api/applications/outgoing");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0);
    }

    // ── POST /api/applications ────────────────────────────────────────────

    [Fact]
    public async Task Apply_ToOffer_Returns201()
    {
        var (offerId, _, applicantToken) = await SetupOfferAsync();
        var resp = await AuthorizedClient(applicantToken).PostAsJsonAsync(
            "/api/applications",
            new { OfferID = offerId, SkillRequestID = (Guid?)null, Message = "Please!" });

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Apply_Unauthenticated_Returns401()
    {
        var (offerId, _, _) = await SetupOfferAsync();
        var resp = await Client.PostAsJsonAsync("/api/applications",
            new { OfferID = offerId, SkillRequestID = (Guid?)null });

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Apply_OwnOffer_Returns409()
    {
        var skillId       = await SeedSkillAsync($"AppOwn_{Guid.NewGuid():N}");
        var (userId, token) = await RegisterAndLoginAsync();
        await SeedUserSkillAsync(userId, skillId);
        var client        = AuthorizedClient(token);

        var createResp = await client.PostAsJsonAsync("/api/skilloffers",
            new { SkillID = skillId, Title = "Own offer" });
        var offerId    = (await createResp.Content.ReadFromJsonAsync<JsonElement>())
                         .GetProperty("id").GetGuid();

        var resp = await client.PostAsJsonAsync("/api/applications",
            new { OfferID = offerId, SkillRequestID = (Guid?)null });

        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Apply_Duplicate_Returns409()
    {
        var (offerId, _, applicantToken) = await SetupOfferAsync();
        var client = AuthorizedClient(applicantToken);

        await client.PostAsJsonAsync("/api/applications",
            new { OfferID = offerId, SkillRequestID = (Guid?)null });

        var resp = await client.PostAsJsonAsync("/api/applications",
            new { OfferID = offerId, SkillRequestID = (Guid?)null });

        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ── DELETE /api/applications/{id} ─────────────────────────────────────

    [Fact]
    public async Task Cancel_OwnPendingApplication_Returns204()
    {
        var (offerId, _, applicantToken) = await SetupOfferAsync();
        var applicantClient = AuthorizedClient(applicantToken);

        var applyResp = await applicantClient.PostAsJsonAsync("/api/applications",
            new { OfferID = offerId, SkillRequestID = (Guid?)null });
        var appId = (await applyResp.Content.ReadFromJsonAsync<JsonElement>())
                    .GetProperty("id").GetGuid();

        var resp = await applicantClient.DeleteAsync($"/api/applications/{appId}");

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ── PUT /api/applications/{id}/respond ────────────────────────────────

    [Fact]
    public async Task Respond_Accept_Returns204AndCreatesDeal()
    {
        var (offerId, ownerToken, applicantToken) = await SetupOfferAsync();
        var applicantClient = AuthorizedClient(applicantToken);
        var ownerClient     = AuthorizedClient(ownerToken);

        var applyResp = await applicantClient.PostAsJsonAsync("/api/applications",
            new { OfferID = offerId, SkillRequestID = (Guid?)null });
        var appId = (await applyResp.Content.ReadFromJsonAsync<JsonElement>())
                    .GetProperty("id").GetGuid();

        var resp = await ownerClient.PutAsJsonAsync($"/api/applications/{appId}/respond",
            new { Status = 1 }); // Accepted

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify deal was created
        var dealsResp = await ownerClient.GetAsync("/api/deals");
        var dealsBody = await dealsResp.Content.ReadFromJsonAsync<JsonElement>();
        dealsBody.GetProperty("totalCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Respond_Reject_Returns204()
    {
        var (offerId, ownerToken, applicantToken) = await SetupOfferAsync();
        var applicantClient = AuthorizedClient(applicantToken);
        var ownerClient     = AuthorizedClient(ownerToken);

        var applyResp = await applicantClient.PostAsJsonAsync("/api/applications",
            new { OfferID = offerId, SkillRequestID = (Guid?)null });
        var appId = (await applyResp.Content.ReadFromJsonAsync<JsonElement>())
                    .GetProperty("id").GetGuid();

        var resp = await ownerClient.PutAsJsonAsync($"/api/applications/{appId}/respond",
            new { Status = 2 }); // Rejected

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Respond_NonOwner_Returns403()
    {
        var (offerId, _, applicantToken) = await SetupOfferAsync();
        var (_, randomToken) = await RegisterAndLoginAsync();
        var applicantClient  = AuthorizedClient(applicantToken);
        var randomClient     = AuthorizedClient(randomToken);

        var applyResp = await applicantClient.PostAsJsonAsync("/api/applications",
            new { OfferID = offerId, SkillRequestID = (Guid?)null });
        var appId = (await applyResp.Content.ReadFromJsonAsync<JsonElement>())
                    .GetProperty("id").GetGuid();

        var resp = await randomClient.PutAsJsonAsync($"/api/applications/{appId}/respond",
            new { Status = 1 });

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── GET /api/applications/processed ──────────────────────────────────

    [Fact]
    public async Task GetProcessed_AfterAccept_ReturnsAcceptedApplication()
    {
        var (offerId, ownerToken, applicantToken) = await SetupOfferAsync();
        var applicantClient = AuthorizedClient(applicantToken);
        var ownerClient     = AuthorizedClient(ownerToken);

        var applyResp = await applicantClient.PostAsJsonAsync("/api/applications",
            new { OfferID = offerId, SkillRequestID = (Guid?)null });
        var appId = (await applyResp.Content.ReadFromJsonAsync<JsonElement>())
                    .GetProperty("id").GetGuid();

        await ownerClient.PutAsJsonAsync($"/api/applications/{appId}/respond",
            new { Status = 1 });

        var resp = await ownerClient.GetAsync("/api/applications/processed");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0);
    }
}
