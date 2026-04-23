using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Tests.E2e.Helpers;

namespace Tests.E2e.Controllers;

public class UserProfilesControllerTests : E2eTestBase
{
    public UserProfilesControllerTests(WebAppFactory factory) : base(factory) { }

    // ── GET /api/userprofiles/{accountId} ─────────────────────────────────

    [Fact]
    public async Task GetProfile_Anonymous_Returns200()
    {
        var (id, _) = await RegisterAndLoginAsync();
        var resp    = await Client.GetAsync($"/api/userprofiles/{id}");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetProfile_Unknown_Returns404()
    {
        var resp = await Client.GetAsync($"/api/userprofiles/{Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetProfile_AfterUpsert_ReturnsFullName()
    {
        var (id, token) = await RegisterAndLoginAsync();
        var client      = AuthorizedClient(token);

        await client.PutAsJsonAsync("/api/userprofiles",
            new { FullName = "Ivan Petrov" });

        var resp = await Client.GetAsync($"/api/userprofiles/{id}");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("fullName").GetString().Should().Be("Ivan Petrov");
    }

    // ── PUT /api/userprofiles ─────────────────────────────────────────────

    [Fact]
    public async Task UpsertProfile_Valid_Returns204()
    {
        var (_, token) = await RegisterAndLoginAsync();
        var resp       = await AuthorizedClient(token).PutAsJsonAsync("/api/userprofiles",
            new { FullName = "Anna Smirnova", Description = "Developer" });

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpsertProfile_Unauthenticated_Returns401()
    {
        var resp = await Client.PutAsJsonAsync("/api/userprofiles",
            new { FullName = "Ghost" });

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpsertProfile_TwiceSameUser_UpdatesInPlace()
    {
        var (id, token) = await RegisterAndLoginAsync();
        var client      = AuthorizedClient(token);

        await client.PutAsJsonAsync("/api/userprofiles", new { FullName = "First Name" });
        await client.PutAsJsonAsync("/api/userprofiles", new { FullName = "Second Name" });

        var resp = await Client.GetAsync($"/api/userprofiles/{id}");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("fullName").GetString().Should().Be("Second Name");
    }

    [Fact]
    public async Task UpsertProfile_FutureDateOfBirth_Returns400()
    {
        var (_, token) = await RegisterAndLoginAsync();
        var resp       = await AuthorizedClient(token).PutAsJsonAsync("/api/userprofiles",
            new { FullName = "Test", DateOfBirth = DateTime.UtcNow.AddYears(1) });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
