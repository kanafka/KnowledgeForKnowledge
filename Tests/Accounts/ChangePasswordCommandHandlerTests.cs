using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Accounts.Commands.ChangePassword;
using FluentAssertions;
using Moq;
using Tests.Helpers;

namespace Tests.Accounts;

public class ChangePasswordCommandHandlerTests
{
    private static ChangePasswordCommandHandler CreateHandler(
        Infrastructure.Data.ApplicationDbContext ctx,
        IPasswordHasher? hasher = null)
    {
        return new ChangePasswordCommandHandler(ctx, hasher ?? Mock.Of<IPasswordHasher>());
    }

    [Fact]
    public async Task Handle_AccountNotFound_ThrowsNotFoundException()
    {
        var ctx = TestDbContext.Create();
        var handler = CreateHandler(ctx);

        var act = () => handler.Handle(
            new ChangePasswordCommand(Guid.NewGuid(), "old", "newpass123"),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WrongCurrentPassword_ThrowsUnauthorized()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        ctx.Accounts.Add(account);
        await ctx.SaveChangesAsync();

        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Verify("wrongpass", account.PasswordHash)).Returns(false);

        var handler = CreateHandler(ctx, hasher.Object);
        var act = () => handler.Handle(
            new ChangePasswordCommand(account.AccountID, "wrongpass", "newpass123"),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*текущий пароль*");
    }

    [Fact]
    public async Task Handle_ShortNewPassword_ThrowsInvalidOperation()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        ctx.Accounts.Add(account);
        await ctx.SaveChangesAsync();

        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Verify("correct", account.PasswordHash)).Returns(true);

        var handler = CreateHandler(ctx, hasher.Object);
        var act = () => handler.Handle(
            new ChangePasswordCommand(account.AccountID, "correct", "abc"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*6 символов*");
    }

    [Fact]
    public async Task Handle_ValidRequest_UpdatesPasswordHash()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        ctx.Accounts.Add(account);
        await ctx.SaveChangesAsync();

        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Verify("correct", account.PasswordHash)).Returns(true);
        hasher.Setup(h => h.Hash("newsecure")).Returns("new_bcrypt_hash");

        var handler = CreateHandler(ctx, hasher.Object);
        await handler.Handle(
            new ChangePasswordCommand(account.AccountID, "correct", "newsecure"),
            CancellationToken.None);

        var updated = await ctx.Accounts.FindAsync(account.AccountID);
        updated!.PasswordHash.Should().Be("new_bcrypt_hash");
    }
}
