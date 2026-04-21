using Application.Features.Notifications.Commands.MarkNotificationsRead;
using FluentAssertions;
using Tests.Helpers;

namespace Tests.Notifications;

public class MarkNotificationsReadCommandHandlerTests
{
    private static MarkNotificationsReadCommandHandler CreateHandler(Infrastructure.Data.ApplicationDbContext ctx)
        => new(ctx);

    [Fact]
    public async Task Handle_SingleNotification_MarksAsRead()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        var notification = Fakes.Notification(accountId: account.AccountID);
        notification.IsRead = false;
        ctx.Accounts.Add(account);
        ctx.Notifications.Add(notification);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        await handler.Handle(
            new MarkNotificationsReadCommand(account.AccountID, notification.NotificationID),
            CancellationToken.None);

        var updated = await ctx.Notifications.FindAsync(notification.NotificationID);
        updated!.IsRead.Should().BeTrue();
    }

    // NOTE: The following two tests call the "mark all" (NotificationID = null) path which uses
    // ExecuteUpdateAsync — a bulk-update API not supported by the EF InMemory provider.
    // They are skipped here; the single-notification path above provides coverage for the handler.
    // These cases should be covered by integration tests that use a real database.

    [Fact(Skip = "ExecuteUpdateAsync is not supported by the EF InMemory provider")]
    public async Task Handle_AllNotifications_MarksAllAsRead()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        var n1 = Fakes.Notification(accountId: account.AccountID);
        var n2 = Fakes.Notification(accountId: account.AccountID);
        var n3 = Fakes.Notification(accountId: account.AccountID);
        n1.IsRead = false;
        n2.IsRead = false;
        n3.IsRead = true; // already read
        ctx.Accounts.Add(account);
        ctx.Notifications.AddRange(n1, n2, n3);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        await handler.Handle(
            new MarkNotificationsReadCommand(account.AccountID, null),
            CancellationToken.None);

        var all = ctx.Notifications
            .Where(n => n.AccountID == account.AccountID)
            .ToList();
        all.Should().AllSatisfy(n => n.IsRead.Should().BeTrue());
    }

    [Fact(Skip = "ExecuteUpdateAsync is not supported by the EF InMemory provider")]
    public async Task Handle_WrongAccountId_DoesNotMarkOtherUsersNotifications()
    {
        var ctx = TestDbContext.Create();
        var account1 = Fakes.Account();
        var account2 = Fakes.Account(login: "other@t.com");
        var notification = Fakes.Notification(accountId: account1.AccountID);
        notification.IsRead = false;
        ctx.Accounts.AddRange(account1, account2);
        ctx.Notifications.Add(notification);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        // Mark all for account2 (has none)
        await handler.Handle(
            new MarkNotificationsReadCommand(account2.AccountID, null),
            CancellationToken.None);

        var unchanged = await ctx.Notifications.FindAsync(notification.NotificationID);
        unchanged!.IsRead.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NotificationIdFromOtherUser_NotMarked()
    {
        var ctx = TestDbContext.Create();
        var account1 = Fakes.Account();
        var account2 = Fakes.Account(login: "other@t.com");
        var notification = Fakes.Notification(accountId: account1.AccountID);
        notification.IsRead = false;
        ctx.Accounts.AddRange(account1, account2);
        ctx.Notifications.Add(notification);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        // Try to mark notification owned by account1, but using account2's ID
        await handler.Handle(
            new MarkNotificationsReadCommand(account2.AccountID, notification.NotificationID),
            CancellationToken.None);

        var unchanged = await ctx.Notifications.FindAsync(notification.NotificationID);
        unchanged!.IsRead.Should().BeFalse();
    }
}
