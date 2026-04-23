using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Tests.E2e.Helpers;

namespace Tests.E2e.Controllers;

public class DealsControllerTests : E2eTestBase
{
    public DealsControllerTests(WebAppFactory factory) : base(factory) { }

    // ── GET /api/deals ────────────────────────────────────────────────────

    [Fact]
    public async Task GetDeals_Unauthenticated_Returns401()
    {
        var resp = await Client.GetAsync("/api/deals");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetDeals_NewUser_ReturnsEmptyList()
    {
        var (_, token) = await RegisterAndLoginAsync();
        var resp       = await AuthorizedClient(token).GetAsync("/api/deals");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("totalCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task GetDeals_AfterDealCreated_ReturnsDeal()
    {
        var (_, _, tokenA, _, _) = await SeedActiveDealAsync();
        var resp                 = await AuthorizedClient(tokenA).GetAsync("/api/deals");
        var body                 = await resp.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("totalCount").GetInt32().Should().Be(1);
    }

    // ── GET /api/deals/user/{accountId} ───────────────────────────────────

    [Fact]
    public async Task GetUserDeals_Anonymous_Returns200()
    {
        var (_, token) = await RegisterAndLoginAsync();
        var me         = await AuthorizedClient(token).GetAsync("/api/accounts/me");
        var userId     = (await me.Content.ReadFromJsonAsync<JsonElement>())
                         .GetProperty("accountID").GetGuid();

        var resp = await Client.GetAsync($"/api/deals/user/{userId}");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── GET /api/deals/{id} ───────────────────────────────────────────────

    [Fact]
    public async Task GetById_Participant_Returns200()
    {
        var (dealId, _, tokenA, _, _) = await SeedActiveDealAsync();
        var resp                      = await AuthorizedClient(tokenA)
                                            .GetAsync($"/api/deals/{dealId}");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("dealID").GetGuid().Should().Be(dealId);
    }

    [Fact]
    public async Task GetById_NonParticipant_Returns403()
    {
        var (dealId, _, _, _, _) = await SeedActiveDealAsync();
        var (_, randomToken)     = await RegisterAndLoginAsync();

        var resp = await AuthorizedClient(randomToken).GetAsync($"/api/deals/{dealId}");

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetById_Unknown_Returns404()
    {
        var (_, token) = await RegisterAndLoginAsync();
        var resp       = await AuthorizedClient(token).GetAsync($"/api/deals/{Guid.NewGuid()}");

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── PUT /api/deals/{id}/complete ──────────────────────────────────────

    [Fact]
    public async Task Complete_Initiator_Returns204()
    {
        var (dealId, _, tokenA, _, _) = await SeedActiveDealAsync();

        var resp = await AuthorizedClient(tokenA)
                       .PutAsync($"/api/deals/{dealId}/complete", null);

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Complete_BothSides_StatusBecomesCompleted()
    {
        var (dealId, _, tokenA, _, tokenB) = await SeedActiveDealAsync();
        var clientA = AuthorizedClient(tokenA);
        var clientB = AuthorizedClient(tokenB);

        await clientA.PutAsync($"/api/deals/{dealId}/complete", null);
        await clientB.PutAsync($"/api/deals/{dealId}/complete", null);

        var resp = await clientA.GetAsync($"/api/deals/{dealId}");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("status").GetString().Should().Be("Completed");
    }

    [Fact]
    public async Task Complete_NonParticipant_Returns403()
    {
        var (dealId, _, _, _, _) = await SeedActiveDealAsync();
        var (_, randomToken)     = await RegisterAndLoginAsync();

        var resp = await AuthorizedClient(randomToken)
                       .PutAsync($"/api/deals/{dealId}/complete", null);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── PUT /api/deals/{id}/cancel ────────────────────────────────────────

    [Fact]
    public async Task Cancel_Participant_Returns204()
    {
        var (dealId, _, tokenA, _, _) = await SeedActiveDealAsync();

        var resp = await AuthorizedClient(tokenA)
                       .PutAsync($"/api/deals/{dealId}/cancel", null);

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Cancel_NonParticipant_Returns403()
    {
        var (dealId, _, _, _, _) = await SeedActiveDealAsync();
        var (_, randomToken)     = await RegisterAndLoginAsync();

        var resp = await AuthorizedClient(randomToken)
                       .PutAsync($"/api/deals/{dealId}/cancel", null);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
