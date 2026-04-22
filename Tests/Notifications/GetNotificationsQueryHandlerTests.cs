using Application.Features.Notifications.Queries.GetNotifications;
using FluentAssertions;
using Tests.Helpers;

namespace Tests.Notifications;

public class GetNotificationsQueryHandlerTests
{
    private static GetNotificationsQueryHandler CreateHandler(Infrastructure.Data.ApplicationDbContext ctx) => new(ctx);

    [Fact]
    public async Task Handle_NoNotifications_ReturnsEmpty()
    {
        var ctx = TestDbContext.Create();
        var handler = CreateHandler(ctx);
        var result = await handler.Handle(
            new GetNotificationsQuery(Guid.NewGuid(), UnreadOnly: false),
            CancellationToken.None);
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.UnreadCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ReturnsOnlyAccountsNotifications()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        var other = Fakes.Account(login: "o@t.com");
        ctx.Accounts.AddRange(account, other);
        ctx.Notifications.Add(Fakes.Notification(accountId: account.AccountID));
        ctx.Notifications.Add(Fakes.Notification(accountId: other.AccountID));
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(
            new GetNotificationsQuery(account.AccountID, UnreadOnly: false),
            CancellationToken.None);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_UnreadOnly_FiltersRead()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        ctx.Accounts.Add(account);
        var unread = Fakes.Notification(accountId: account.AccountID);
        unread.IsRead = false;
        var read = Fakes.Notification(accountId: account.AccountID);
        read.IsRead = true;
        ctx.Notifications.AddRange(unread, read);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(
            new GetNotificationsQuery(account.AccountID, UnreadOnly: true),
            CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items[0].IsRead.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_AllNotifications_IncludesRead()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        ctx.Accounts.Add(account);
        var unread = Fakes.Notification(accountId: account.AccountID);
        unread.IsRead = false;
        var read = Fakes.Notification(accountId: account.AccountID);
        read.IsRead = true;
        ctx.Notifications.AddRange(unread, read);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(
            new GetNotificationsQuery(account.AccountID, UnreadOnly: false),
            CancellationToken.None);

        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_UnreadCount_ReflectsUnreadNotifications()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        ctx.Accounts.Add(account);
        var n1 = Fakes.Notification(accountId: account.AccountID); n1.IsRead = false;
        var n2 = Fakes.Notification(accountId: account.AccountID); n2.IsRead = false;
        var n3 = Fakes.Notification(accountId: account.AccountID); n3.IsRead = true;
        ctx.Notifications.AddRange(n1, n2, n3);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(
            new GetNotificationsQuery(account.AccountID, UnreadOnly: false),
            CancellationToken.None);

        result.UnreadCount.Should().Be(2);
        result.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task Handle_Pagination_Works()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        ctx.Accounts.Add(account);
        for (var i = 0; i < 7; i++)
            ctx.Notifications.Add(Fakes.Notification(accountId: account.AccountID));
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var page1 = await handler.Handle(
            new GetNotificationsQuery(account.AccountID, UnreadOnly: false, Page: 1, PageSize: 5),
            CancellationToken.None);
        var page2 = await handler.Handle(
            new GetNotificationsQuery(account.AccountID, UnreadOnly: false, Page: 2, PageSize: 5),
            CancellationToken.None);

        page1.Items.Should().HaveCount(5);
        page2.Items.Should().HaveCount(2);
        page1.TotalCount.Should().Be(7);
    }

    [Fact]
    public async Task Handle_NotificationContent_MappedCorrectly()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        ctx.Accounts.Add(account);
        var notif = Fakes.Notification(accountId: account.AccountID);
        notif.Message = "Hello world";
        ctx.Notifications.Add(notif);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(
            new GetNotificationsQuery(account.AccountID, UnreadOnly: false),
            CancellationToken.None);

        result.Items[0].Message.Should().Be("Hello world");
        result.Items[0].NotificationID.Should().Be(notif.NotificationID);
    }
}
