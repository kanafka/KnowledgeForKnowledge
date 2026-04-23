using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Tests.E2e.Helpers;

namespace Tests.E2e.Controllers;

public class AuthControllerTests : E2eTestBase
{
    public AuthControllerTests(WebAppFactory factory) : base(factory) { }

    // ── POST /api/auth/login ───────────────────────────────────────────────

    [Fact]
    public async Task Login_ValidCredentials_Returns200WithToken()
    {
        var (_, token) = await RegisterAndLoginAsync();
        token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_WrongPassword_Returns403()
    {
        var login = UniqueEmail();
        await Client.PostAsJsonAsync("/api/accounts", new { Login = login, Password = "Correct123!" });

        var resp = await Client.PostAsJsonAsync("/api/auth/login",
            new { Login = login, Password = "WrongPass!" });

        // LoginCommandHandler throws UnauthorizedAccessException → global handler maps it to 403
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Login_UnknownLogin_Returns403()
    {
        var resp = await Client.PostAsJsonAsync("/api/auth/login",
            new { Login = "nobody@nowhere.com", Password = "Pass123!" });

        // LoginCommandHandler throws UnauthorizedAccessException → global handler maps it to 403
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Login_NoTelegram_RequiresTelegramLink()
    {
        var login = UniqueEmail();
        await Client.PostAsJsonAsync("/api/accounts", new { Login = login, Password = "Pass123!" });

        var resp = await Client.PostAsJsonAsync("/api/auth/login",
            new { Login = login, Password = "Pass123!" });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        // Account has no TelegramID → no OTP can be sent; user must link Telegram first
        body.GetProperty("requiresOtp").GetBoolean().Should().BeFalse();
        body.GetProperty("requiresTelegramLink").GetBoolean().Should().BeTrue();
        body.GetProperty("token").GetString().Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task Login_AccountIdReturnedCorrectly()
    {
        var login = UniqueEmail();
        var regResp = await Client.PostAsJsonAsync("/api/accounts", new { Login = login, Password = "Pass123!" });
        var reg     = await regResp.Content.ReadFromJsonAsync<JsonElement>();
        var regId   = reg.GetProperty("accountId").GetGuid();

        var loginResp = await Client.PostAsJsonAsync("/api/auth/login",
            new { Login = login, Password = "Pass123!" });
        var loginData = await loginResp.Content.ReadFromJsonAsync<JsonElement>();

        loginData.GetProperty("accountId").GetGuid().Should().Be(regId);
    }

    // ── POST /api/auth/forgot-password ────────────────────────────────────

    [Fact]
    public async Task ForgotPassword_AnyLogin_Returns200WithSessionId()
    {
        // Returns a session (possibly empty) regardless of whether account exists
        var resp = await Client.PostAsJsonAsync("/api/auth/forgot-password",
            new { Login = "nobody@test.com" });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("sessionId", out _).Should().BeTrue();
    }

    // ── POST /api/auth/reset-password ─────────────────────────────────────

    [Fact]
    public async Task ResetPassword_InvalidSession_Returns403()
    {
        var resp = await Client.PostAsJsonAsync("/api/auth/reset-password", new
        {
            SessionId   = Guid.NewGuid().ToString(),
            Code        = "123456",
            NewPassword = "NewPass123!"
        });

        // ResetPasswordCommandHandler throws UnauthorizedAccessException → global handler maps it to 403
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
