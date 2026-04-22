using Application.Common.Exceptions;
using Application.Features.Accounts.Queries.GetAccount;
using Application.Features.Accounts.Queries.GetAccounts;
using FluentAssertions;
using Tests.Helpers;

namespace Tests.Accounts;

public class GetAccountQueryHandlerTests
{
    [Fact]
    public async Task Handle_AccountNotFound_ThrowsNotFoundException()
    {
        var ctx = TestDbContext.Create();
        var handler = new GetAccountQueryHandler(ctx);

        var act = () => handler.Handle(new GetAccountQuery { AccountID = Guid.NewGuid() }, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ExistingAccount_ReturnsCorrectDto()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account(login: "user@test.com", isAdmin: false);
        ctx.Accounts.Add(account);
        await ctx.SaveChangesAsync();

        var handler = new GetAccountQueryHandler(ctx);
        var result = await handler.Handle(new GetAccountQuery { AccountID = account.AccountID }, CancellationToken.None);

        result.AccountID.Should().Be(account.AccountID);
        result.Login.Should().Be("user@test.com");
        result.IsAdmin.Should().BeFalse();
        result.IsActive.Should().BeTrue();
        result.TelegramID.Should().BeNull();
    }

    [Fact]
    public async Task Handle_AdminAccount_ReturnsIsAdminTrue()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account(isAdmin: true);
        ctx.Accounts.Add(account);
        await ctx.SaveChangesAsync();

        var handler = new GetAccountQueryHandler(ctx);
        var result = await handler.Handle(new GetAccountQuery { AccountID = account.AccountID }, CancellationToken.None);

        result.IsAdmin.Should().BeTrue();
    }
}

public class GetAccountsQueryHandlerTests
{
    [Fact]
    public async Task Handle_NoAccounts_ReturnsEmptyPage()
    {
        var ctx = TestDbContext.Create();
        var handler = new GetAccountsQueryHandler(ctx);

        var result = await handler.Handle(new GetAccountsQuery(null), CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_MultipleAccounts_ReturnsPaged()
    {
        var ctx = TestDbContext.Create();
        for (var i = 0; i < 5; i++)
            ctx.Accounts.Add(Fakes.Account(login: $"user{i}@t.com"));
        await ctx.SaveChangesAsync();

        var handler = new GetAccountsQueryHandler(ctx);
        var result = await handler.Handle(new GetAccountsQuery(null, Page: 1, PageSize: 3), CancellationToken.None);

        result.Items.Should().HaveCount(3);
        result.TotalCount.Should().Be(5);
        result.TotalPages.Should().Be(2);
    }

    [Fact]
    public async Task Handle_SearchByLogin_FiltersResults()
    {
        var ctx = TestDbContext.Create();
        ctx.Accounts.Add(Fakes.Account(login: "alice@test.com"));
        ctx.Accounts.Add(Fakes.Account(login: "bob@test.com"));
        ctx.Accounts.Add(Fakes.Account(login: "charlie@test.com"));
        await ctx.SaveChangesAsync();

        var handler = new GetAccountsQueryHandler(ctx);
        var result = await handler.Handle(new GetAccountsQuery("alice"), CancellationToken.None);

        result.Items.Should().HaveCount(1);
        result.Items[0].Login.Should().Be("alice@test.com");
    }

    [Fact]
    public async Task Handle_SearchByFullName_FiltersResults()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account(login: "a@t.com");
        var profile = Fakes.Profile(account.AccountID, "Ivan Petrov");
        ctx.Accounts.Add(account);
        ctx.UserProfiles.Add(profile);
        ctx.Accounts.Add(Fakes.Account(login: "b@t.com"));
        await ctx.SaveChangesAsync();

        var handler = new GetAccountsQueryHandler(ctx);
        var result = await handler.Handle(new GetAccountsQuery("petrov"), CancellationToken.None);

        result.Items.Should().HaveCount(1);
        result.Items[0].FullName.Should().Be("Ivan Petrov");
        result.Items[0].HasProfile.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_AccountWithoutProfile_HasProfileFalse()
    {
        var ctx = TestDbContext.Create();
        ctx.Accounts.Add(Fakes.Account());
        await ctx.SaveChangesAsync();

        var handler = new GetAccountsQueryHandler(ctx);
        var result = await handler.Handle(new GetAccountsQuery(null), CancellationToken.None);

        result.Items[0].HasProfile.Should().BeFalse();
        result.Items[0].FullName.Should().BeNull();
    }

    [Fact]
    public async Task Handle_SecondPage_ReturnsCorrectItems()
    {
        var ctx = TestDbContext.Create();
        for (var i = 0; i < 5; i++)
            ctx.Accounts.Add(Fakes.Account(login: $"u{i}@t.com"));
        await ctx.SaveChangesAsync();

        var handler = new GetAccountsQueryHandler(ctx);
        var result = await handler.Handle(new GetAccountsQuery(null, Page: 2, PageSize: 3), CancellationToken.None);

        result.Items.Should().HaveCount(2);
        result.Page.Should().Be(2);
    }
}
