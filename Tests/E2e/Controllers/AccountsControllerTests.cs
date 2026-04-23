using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Tests.E2e.Helpers;

namespace Tests.E2e.Controllers;

public class AccountsControllerTests : E2eTestBase
{
    public AccountsControllerTests(WebAppFactory factory) : base(factory) { }

    // ── POST /api/accounts ────────────────────────────────────────────────

    [Fact]
    public async Task CreateAccount_Valid_Returns201()
    {
        var resp = await Client.PostAsJsonAsync("/api/accounts",
            new { Login = UniqueEmail(), Password = "Pass123!" });

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("accountId").GetGuid().Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateAccount_DuplicateLogin_Returns409()
    {
        var login = UniqueEmail();
        await Client.PostAsJsonAsync("/api/accounts", new { Login = login, Password = "Pass123!" });

        var resp = await Client.PostAsJsonAsync("/api/accounts",
            new { Login = login, Password = "Pass123!" });

        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateAccount_WeakPassword_Returns400()
    {
        var resp = await Client.PostAsJsonAsync("/api/accounts",
            new { Login = UniqueEmail(), Password = "x" });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── GET /api/accounts/me ──────────────────────────────────────────────

    [Fact]
    public async Task GetMe_Authenticated_Returns200WithCorrectId()
    {
        var (id, token) = await RegisterAndLoginAsync();
        var client      = AuthorizedClient(token);

        var resp = await client.GetAsync("/api/accounts/me");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("accountID").GetGuid().Should().Be(id);
    }

    [Fact]
    public async Task GetMe_Unauthenticated_Returns401()
    {
        var resp = await Client.GetAsync("/api/accounts/me");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET /api/accounts/{id} ────────────────────────────────────────────

    [Fact]
    public async Task GetAccount_ExistingId_Returns200()
    {
        var (id, token) = await RegisterAndLoginAsync();
        var client      = AuthorizedClient(token);

        var resp = await client.GetAsync($"/api/accounts/{id}");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAccount_UnknownId_Returns404()
    {
        var (_, token) = await RegisterAndLoginAsync();
        var client     = AuthorizedClient(token);

        var resp = await client.GetAsync($"/api/accounts/{Guid.NewGuid()}");

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /api/accounts (Admin only) ───────────────────────────────────

    [Fact]
    public async Task GetAccounts_AsAdmin_Returns200()
    {
        var (_, adminToken) = await RegisterAndLoginAdminAsync();
        var client          = AuthorizedClient(adminToken);

        var resp = await client.GetAsync("/api/accounts");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAccounts_AsRegularUser_Returns403()
    {
        var (_, token) = await RegisterAndLoginAsync();
        var client     = AuthorizedClient(token);

        var resp = await client.GetAsync("/api/accounts");

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── PUT /api/accounts/{id}/password ──────────────────────────────────

    [Fact]
    public async Task ChangePassword_Correct_Returns204()
    {
        var login          = UniqueEmail();
        var (id, token)    = await RegisterAndLoginAsync(login, "OldPass123!");
        var client         = AuthorizedClient(token);

        var resp = await client.PutAsJsonAsync($"/api/accounts/{id}/password",
            new { CurrentPassword = "OldPass123!", NewPassword = "NewPass456!" });

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_Returns403()
    {
        var (id, token) = await RegisterAndLoginAsync();
        var client      = AuthorizedClient(token);

        var resp = await client.PutAsJsonAsync($"/api/accounts/{id}/password",
            new { CurrentPassword = "WrongOld!", NewPassword = "NewPass456!" });

        // Handler throws UnauthorizedAccessException → global exception handler maps it to 403
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── DELETE /api/accounts/{id} ─────────────────────────────────────────

    [Fact]
    public async Task Deactivate_Self_Returns204()
    {
        var (id, token) = await RegisterAndLoginAsync();
        var client      = AuthorizedClient(token);

        var resp = await client.DeleteAsync($"/api/accounts/{id}");

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Deactivate_OtherUserAsNonAdmin_Returns403()
    {
        var (otherId, _)    = await RegisterAndLoginAsync();
        var (_, token)      = await RegisterAndLoginAsync();
        var client          = AuthorizedClient(token);

        var resp = await client.DeleteAsync($"/api/accounts/{otherId}");

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── PUT /api/accounts/{id}/activate (Admin) ───────────────────────────

    [Fact]
    public async Task Activate_AsAdmin_Returns204()
    {
        var (targetId, _)   = await RegisterAndLoginAsync();
        var (_, adminToken) = await RegisterAndLoginAdminAsync();
        var adminClient     = AuthorizedClient(adminToken);

        // First deactivate
        await adminClient.DeleteAsync($"/api/accounts/{targetId}");

        // Then activate
        var resp = await adminClient.PutAsync($"/api/accounts/{targetId}/activate", null);

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
