using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using SkillLevel = Domain.Enums.SkillLevel;

namespace Tests.E2e.Helpers;

/// <summary>
/// Base class for all E2E test classes.
/// Each test class has its own WebAppFactory (via IClassFixture) → isolated in-memory DB.
/// Within a class, tests share the DB; they must use unique logins / IDs to avoid conflicts.
/// </summary>
public abstract class E2eTestBase : IClassFixture<WebAppFactory>
{
    protected readonly WebAppFactory Factory;
    protected readonly HttpClient Client;

    protected E2eTestBase(WebAppFactory factory)
    {
        Factory = factory;
        Client  = factory.CreateClient();
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>Returns a unique email address safe to use in each test.</summary>
    protected static string UniqueEmail() => $"u{Guid.NewGuid():N}@t.com";

    /// <summary>
    /// Creates an account via POST /api/accounts, then logs in via POST /api/auth/login.
    /// Returns (accountId, JWT token).
    /// </summary>
    protected async Task<(Guid id, string token)> RegisterAndLoginAsync(
        string? login    = null,
        string  password = "Password123!")
    {
        login ??= UniqueEmail();

        var regResp = await Client.PostAsJsonAsync("/api/accounts",
            new { Login = login, Password = password });
        regResp.EnsureSuccessStatusCode();
        var reg = await regResp.Content.ReadFromJsonAsync<JsonElement>();
        var id  = reg.GetProperty("accountId").GetGuid();

        // Login via API requires a Telegram OTP step and never returns a JWT directly.
        // Generate the token straight from the service layer instead.
        using var scope = Factory.Services.CreateScope();
        var db     = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var jwtSvc = scope.ServiceProvider.GetRequiredService<IJwtService>();
        var account = await db.Accounts.FindAsync(id);

        // Seed a minimal profile so commands that require one (SkillOffers, SkillRequests) work.
        var profile = new UserProfile { AccountID = id, FullName = "Test User" };
        db.UserProfiles.Add(profile);
        await db.SaveChangesAsync();

        var token = jwtSvc.GenerateToken(account!);
        return (id, token);
    }

    /// <summary>
    /// Creates a regular account via the API, promotes it to Admin in the DB,
    /// then generates a fresh JWT with the Admin role.
    /// </summary>
    protected async Task<(Guid id, string token)> RegisterAndLoginAdminAsync()
    {
        var (id, _) = await RegisterAndLoginAsync();

        using var scope   = Factory.Services.CreateScope();
        var db            = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var jwtSvc        = scope.ServiceProvider.GetRequiredService<IJwtService>();

        var account = await db.Accounts.FindAsync(id);
        account!.IsAdmin = true;
        await db.SaveChangesAsync();

        var adminToken = jwtSvc.GenerateToken(account);
        return (id, adminToken);
    }

    /// <summary>Creates an HttpClient pre-configured with the given bearer token.</summary>
    protected HttpClient AuthorizedClient(string token)
    {
        var c = Factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    /// <summary>Directly adds a skill to a user's skills in the DB (bypasses profile/validation).</summary>
    protected async Task SeedUserSkillAsync(Guid accountId, Guid skillId)
    {
        using var scope = Factory.Services.CreateScope();
        var db          = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var existing    = await db.UserSkills.FindAsync(accountId, skillId);
        if (existing is not null) return;
        db.UserSkills.Add(new UserSkill
        {
            AccountID  = accountId,
            SkillID    = skillId,
            SkillLevel = SkillLevel.Middle,
            IsVerified = false
        });
        await db.SaveChangesAsync();
    }

    /// <summary>Directly seeds a skill into the catalog (bypasses admin check).</summary>
    protected async Task<Guid> SeedSkillAsync(string name = "Python")
    {
        using var scope = Factory.Services.CreateScope();
        var db          = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var skill       = new SkillsCatalog
        {
            SkillID   = Guid.NewGuid(),
            SkillName = name,
            Epithet   = SkillEpithet.IT
        };
        db.SkillsCatalog.Add(skill);
        await db.SaveChangesAsync();
        return skill.SkillID;
    }

    /// <summary>
    /// Full flow: creates two users (A and B), A posts an offer, B applies,
    /// A accepts the application → returns the resulting DealID.
    /// </summary>
    protected async Task<(Guid dealId, Guid userAId, string tokenA, Guid userBId, string tokenB)>
        SeedActiveDealAsync()
    {
        var skillId         = await SeedSkillAsync($"DealSkill_{Guid.NewGuid():N}");
        var (aId, tokenA)   = await RegisterAndLoginAsync();
        var (bId, tokenB)   = await RegisterAndLoginAsync();
        // A must own the skill to publish an offer
        await SeedUserSkillAsync(aId, skillId);
        var clientA         = AuthorizedClient(tokenA);
        var clientB         = AuthorizedClient(tokenB);

        // A creates an offer
        var offerResp = await clientA.PostAsJsonAsync("/api/skilloffers",
            new { SkillID = skillId, Title = "Test offer" });
        offerResp.EnsureSuccessStatusCode();
        var offerData = await offerResp.Content.ReadFromJsonAsync<JsonElement>();
        var offerId   = offerData.GetProperty("id").GetGuid();

        // B applies
        var appResp = await clientB.PostAsJsonAsync("/api/applications",
            new { OfferID = offerId, SkillRequestID = (Guid?)null, Message = "Hi" });
        appResp.EnsureSuccessStatusCode();
        var appData = await appResp.Content.ReadFromJsonAsync<JsonElement>();
        var appId   = appData.GetProperty("id").GetGuid();

        // A accepts → deal is created
        var respondResp = await clientA.PutAsJsonAsync($"/api/applications/{appId}/respond",
            new { Status = 1 }); // 1 = Accepted
        respondResp.EnsureSuccessStatusCode();

        // Find the deal ID via API
        var dealsResp = await clientA.GetAsync("/api/deals");
        dealsResp.EnsureSuccessStatusCode();
        var dealsData = await dealsResp.Content.ReadFromJsonAsync<JsonElement>();
        var dealId    = dealsData.GetProperty("items")[0].GetProperty("dealID").GetGuid();

        return (dealId, aId, tokenA, bId, tokenB);
    }
}
