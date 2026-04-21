using Application.Common.Exceptions;
using Application.Features.Accounts.Commands.DeactivateAccount;
using FluentAssertions;
using Tests.Helpers;

namespace Tests.Accounts;

public class DeactivateAccountCommandHandlerTests
{
    private static DeactivateAccountCommandHandler CreateHandler(Infrastructure.Data.ApplicationDbContext ctx)
        => new(ctx);

    [Fact]
    public async Task Handle_NonAdminDeactivatingOtherAccount_ThrowsUnauthorized()
    {
        var ctx = TestDbContext.Create();
        var handler = CreateHandler(ctx);

        var act = () => handler.Handle(
            new DeactivateAccountCommand(Guid.NewGuid(), Guid.NewGuid(), IsAdmin: false),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*прав*");
    }

    [Fact]
    public async Task Handle_AccountNotFound_ThrowsNotFoundException()
    {
        var ctx = TestDbContext.Create();
        var id = Guid.NewGuid();
        var handler = CreateHandler(ctx);

        var act = () => handler.Handle(
            new DeactivateAccountCommand(id, id, IsAdmin: false),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_SelfDeactivation_SetsIsActiveFalse()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        ctx.Accounts.Add(account);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        await handler.Handle(
            new DeactivateAccountCommand(account.AccountID, account.AccountID, IsAdmin: false),
            CancellationToken.None);

        var updated = await ctx.Accounts.FindAsync(account.AccountID);
        updated!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_AdminDeactivatingOtherAccount_Succeeds()
    {
        var ctx = TestDbContext.Create();
        var admin = Fakes.Account(isAdmin: true);
        var target = Fakes.Account(login: "target@t.com");
        ctx.Accounts.AddRange(admin, target);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        await handler.Handle(
            new DeactivateAccountCommand(target.AccountID, admin.AccountID, IsAdmin: true),
            CancellationToken.None);

        var updated = await ctx.Accounts.FindAsync(target.AccountID);
        updated!.IsActive.Should().BeFalse();
    }
}
