using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Tests.E2e.Helpers;

namespace Tests.E2e.Controllers;

public class ReviewsControllerTests : E2eTestBase
{
    public ReviewsControllerTests(WebAppFactory factory) : base(factory) { }

    // ── GET /api/reviews/{accountId} ──────────────────────────────────────

    [Fact]
    public async Task GetReviews_Anonymous_Returns200()
    {
        var resp = await Client.GetAsync($"/api/reviews/{Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetReviews_AfterReview_ReturnsIt()
    {
        var (dealId, userAId, tokenA, userBId, tokenB) = await SeedActiveDealAsync();

        // Complete on both sides
        var clientA = AuthorizedClient(tokenA);
        var clientB = AuthorizedClient(tokenB);
        await clientA.PutAsync($"/api/deals/{dealId}/complete", null);
        await clientB.PutAsync($"/api/deals/{dealId}/complete", null);

        // A reviews B
        await clientA.PostAsJsonAsync("/api/reviews",
            new { DealID = dealId, TargetID = userBId, Rating = 5, Comment = "Great!" });

        var resp = await Client.GetAsync($"/api/reviews/{userBId}");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("totalCount").GetInt32().Should().Be(1);
        body.GetProperty("averageRating").GetDouble().Should().Be(5.0);
    }

    // ── POST /api/reviews ─────────────────────────────────────────────────

    [Fact]
    public async Task CreateReview_Valid_Returns201()
    {
        var (dealId, _, tokenA, userBId, tokenB) = await SeedActiveDealAsync();
        var clientA = AuthorizedClient(tokenA);
        var clientB = AuthorizedClient(tokenB);

        await clientA.PutAsync($"/api/deals/{dealId}/complete", null);
        await clientB.PutAsync($"/api/deals/{dealId}/complete", null);

        var resp = await clientA.PostAsJsonAsync("/api/reviews",
            new { DealID = dealId, TargetID = userBId, Rating = 4 });

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateReview_Unauthenticated_Returns401()
    {
        var resp = await Client.PostAsJsonAsync("/api/reviews",
            new { DealID = Guid.NewGuid(), TargetID = Guid.NewGuid(), Rating = 5 });

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateReview_InvalidRating_Returns400()
    {
        var (_, token) = await RegisterAndLoginAsync();
        var resp       = await AuthorizedClient(token).PostAsJsonAsync("/api/reviews",
            new { DealID = Guid.NewGuid(), TargetID = Guid.NewGuid(), Rating = 10 });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateReview_DealNotFound_Returns404()
    {
        var (_, token) = await RegisterAndLoginAsync();
        var resp       = await AuthorizedClient(token).PostAsJsonAsync("/api/reviews",
            new { DealID = Guid.NewGuid(), TargetID = Guid.NewGuid(), Rating = 4 });

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
