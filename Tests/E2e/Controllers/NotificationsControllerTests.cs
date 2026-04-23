using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Tests.E2e.Helpers;

namespace Tests.E2e.Controllers;

public class NotificationsControllerTests : E2eTestBase
{
    public NotificationsControllerTests(WebAppFactory factory) : base(factory) { }

    // ── GET /api/notifications ────────────────────────────────────────────

    [Fact]
    public async Task GetNotifications_Authenticated_Returns200()
    {
        var (_, token) = await RegisterAndLoginAsync();
        var resp       = await AuthorizedClient(token).GetAsync("/api/notifications");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetNotifications_Unauthenticated_Returns401()
    {
        var resp = await Client.GetAsync("/api/notifications");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetNotifications_ReturnsSeededNotification()
    {
        var (id, token) = await RegisterAndLoginAsync();
        await SeedNotificationAsync(id);

        var resp = await AuthorizedClient(token).GetAsync("/api/notifications");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetNotifications_UnreadOnly_FiltersCorrectly()
    {
        var (id, token) = await RegisterAndLoginAsync();
        await SeedNotificationAsync(id, isRead: false);
        await SeedNotificationAsync(id, isRead: true);

        var resp = await AuthorizedClient(token).GetAsync("/api/notifications?unreadOnly=true");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("totalCount").GetInt32().Should().Be(1);
    }

    // ── PUT /api/notifications/{id}/read ──────────────────────────────────

    [Fact]
    public async Task MarkRead_ValidId_Returns204()
    {
        var (accountId, token) = await RegisterAndLoginAsync();
        var notifId            = await SeedNotificationAsync(accountId);

        var resp = await AuthorizedClient(token)
                       .PutAsync($"/api/notifications/{notifId}/read", null);

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task MarkRead_NotFound_Returns404()
    {
        var (_, token) = await RegisterAndLoginAsync();

        var resp = await AuthorizedClient(token)
                       .PutAsync($"/api/notifications/{Guid.NewGuid()}/read", null);

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── PUT /api/notifications/read-all ───────────────────────────────────

    [Fact]
    public async Task MarkAllRead_Returns204()
    {
        var (accountId, token) = await RegisterAndLoginAsync();
        await SeedNotificationAsync(accountId);
        await SeedNotificationAsync(accountId);

        var resp = await AuthorizedClient(token)
                       .PutAsync("/api/notifications/read-all", null);

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ── Helper ────────────────────────────────────────────────────────────

    private async Task<Guid> SeedNotificationAsync(Guid accountId, bool isRead = false)
    {
        using var scope = Factory.Services.CreateScope();
        var db          = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var notif = new Notification
        {
            NotificationID  = Guid.NewGuid(),
            AccountID       = accountId,
            Type            = NotificationType.NewApplication,
            Message         = "Test notification",
            IsRead          = isRead,
            CreatedAt       = DateTime.UtcNow
        };
        db.Notifications.Add(notif);
        await db.SaveChangesAsync();
        return notif.NotificationID;
    }
}
