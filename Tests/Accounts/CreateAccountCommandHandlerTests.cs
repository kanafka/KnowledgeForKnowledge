using Application.Common.Interfaces;
using Application.Features.Accounts.Commands.CreateAccount;
using FluentAssertions;
using Moq;
using Tests.Helpers;

namespace Tests.Accounts;

public class CreateAccountCommandHandlerTests
{
    private static CreateAccountCommandHandler CreateHandler(
        Infrastructure.Data.ApplicationDbContext ctx,
        IPasswordHasher? hasher = null)
    {
        var h = hasher ?? Mock.Of<IPasswordHasher>(m => m.Hash(It.IsAny<string>()) == "hashed");
        return new CreateAccountCommandHandler(ctx, h);
    }

    [Fact]
    public async Task Handle_BasicCreation_SavesAccount()
    {
        var ctx = TestDbContext.Create();
        var handler = CreateHandler(ctx);

        var result = await handler.Handle(
            new CreateAccountCommand { Login = "new@test.com", Password = "pass123" },
            CancellationToken.None);

        result.AccountId.Should().NotBeEmpty();
        result.TelegramLinkToken.Should().BeNull();

        var account = await ctx.Accounts.FindAsync(result.AccountId);
        account.Should().NotBeNull();
        account!.Login.Should().Be("new@test.com");
        account.PasswordHash.Should().Be("hashed");
        account.IsAdmin.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithTelegramId_NoTokenGenerated()
    {
        var ctx = TestDbContext.Create();
        var handler = CreateHandler(ctx);

        var result = await handler.Handle(
            new CreateAccountCommand
            {
                Login = "tg@test.com",
                Password = "pass123",
                TelegramID = "tg123",
                CreateTelegramLinkToken = true
            },
            CancellationToken.None);

        result.TelegramLinkToken.Should().BeNull();

        var account = await ctx.Accounts.FindAsync(result.AccountId);
        account!.TelegramID.Should().Be("tg123");
        account.TelegramLinkToken.Should().BeNull();
    }

    [Fact]
    public async Task Handle_CreateTelegramLinkTokenTrue_NoTelegramId_GeneratesToken()
    {
        var ctx = TestDbContext.Create();
        var handler = CreateHandler(ctx);

        var result = await handler.Handle(
            new CreateAccountCommand
            {
                Login = "link@test.com",
                Password = "pass123",
                CreateTelegramLinkToken = true
            },
            CancellationToken.None);

        result.TelegramLinkToken.Should().NotBeNullOrEmpty();
        result.TelegramLinkToken!.Length.Should().Be(8);

        var account = await ctx.Accounts.FindAsync(result.AccountId);
        account!.TelegramLinkToken.Should().Be(result.TelegramLinkToken);
    }

    [Fact]
    public async Task Handle_GeneratedToken_IsUnique()
    {
        var ctx = TestDbContext.Create();
        var handler = CreateHandler(ctx);

        var r1 = await handler.Handle(
            new CreateAccountCommand { Login = "u1@t.com", Password = "p", CreateTelegramLinkToken = true },
            CancellationToken.None);

        var r2 = await handler.Handle(
            new CreateAccountCommand { Login = "u2@t.com", Password = "p", CreateTelegramLinkToken = true },
            CancellationToken.None);

        r1.TelegramLinkToken.Should().NotBe(r2.TelegramLinkToken);
    }

    [Fact]
    public async Task Handle_PasswordIsHashed()
    {
        var ctx = TestDbContext.Create();
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Hash("rawpass")).Returns("bcrypt_hash");
        var handler = CreateHandler(ctx, hasher.Object);

        var result = await handler.Handle(
            new CreateAccountCommand { Login = "h@t.com", Password = "rawpass" },
            CancellationToken.None);

        var account = await ctx.Accounts.FindAsync(result.AccountId);
        account!.PasswordHash.Should().Be("bcrypt_hash");
        hasher.Verify(h => h.Hash("rawpass"), Times.Once);
    }
}
