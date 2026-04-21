using Application.Common.Interfaces;
using Application.Features.Auth.Commands.Login;
using Application.Features.Auth.Commands.VerifyOtp;
using Domain.Entities;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Tests.Helpers;

namespace Tests.Auth;

public class LoginCommandHandlerTests
{
    private static LoginCommandHandler CreateHandler(
        Infrastructure.Data.ApplicationDbContext ctx,
        IPasswordHasher? passwordHasher = null,
        ITelegramService? telegram = null,
        IJwtService? jwt = null,
        IMemoryCache? cache = null)
    {
        return new LoginCommandHandler(
            ctx,
            jwt ?? Mock.Of<IJwtService>(),
            passwordHasher ?? Mock.Of<IPasswordHasher>(),
            telegram ?? Mock.Of<ITelegramService>(),
            cache ?? new MemoryCache(new MemoryCacheOptions()));
    }

    [Fact]
    public async Task Handle_AccountNotFound_ThrowsUnauthorized()
    {
        var ctx = TestDbContext.Create();
        var handler = CreateHandler(ctx);

        var act = () => handler.Handle(new LoginCommand("nobody@test.com", "pass"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Неверный логин или пароль*");
    }

    [Fact]
    public async Task Handle_InactiveAccount_ThrowsUnauthorized()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account(isActive: false);
        ctx.Accounts.Add(account);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var act = () => handler.Handle(new LoginCommand(account.Login, "pass"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*деактивирован*");
    }

    [Fact]
    public async Task Handle_AccountLockedOut_ThrowsUnauthorized()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        account.LockoutUntil = DateTime.UtcNow.AddMinutes(10);
        ctx.Accounts.Add(account);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var act = () => handler.Handle(new LoginCommand(account.Login, "pass"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*заблокирован*");
    }

    [Fact]
    public async Task Handle_WrongPassword_IncrementsFailedAttempts()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        ctx.Accounts.Add(account);
        await ctx.SaveChangesAsync();

        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var handler = CreateHandler(ctx, hasher.Object);
        var act = () => handler.Handle(new LoginCommand(account.Login, "wrong"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Неверный логин или пароль*");

        var updated = await ctx.Accounts.FindAsync(account.AccountID);
        updated!.FailedLoginAttempts.Should().Be(1);
    }

    [Fact]
    public async Task Handle_FiveWrongPasswords_LocksAccount()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        account.FailedLoginAttempts = 4;
        ctx.Accounts.Add(account);
        await ctx.SaveChangesAsync();

        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var handler = CreateHandler(ctx, hasher.Object);
        var act = () => handler.Handle(new LoginCommand(account.Login, "wrong"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*заблокирован на*");

        var updated = await ctx.Accounts.FindAsync(account.AccountID);
        updated!.LockoutUntil.Should().NotBeNull();
        updated.FailedLoginAttempts.Should().Be(0);
    }

    [Fact]
    public async Task Handle_LockoutExpired_ResetsCounter()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account(telegramId: "tg123");
        account.LockoutUntil = DateTime.UtcNow.AddMinutes(-1); // expired
        account.FailedLoginAttempts = 5;
        ctx.Accounts.Add(account);
        await ctx.SaveChangesAsync();

        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        var telegram = new Mock<ITelegramService>();

        var handler = CreateHandler(ctx, hasher.Object, telegram.Object);
        var result = await handler.Handle(new LoginCommand(account.Login, "pass"), CancellationToken.None);

        result.RequiresOtp.Should().BeTrue();
        result.SessionId.Should().NotBeNullOrEmpty();

        var updated = await ctx.Accounts.FindAsync(account.AccountID);
        updated!.LockoutUntil.Should().BeNull();
        updated.FailedLoginAttempts.Should().Be(0);
    }

    [Fact]
    public async Task Handle_NoTelegramLinked_ReturnsTelegramLinkToken()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account(telegramId: null);
        ctx.Accounts.Add(account);
        await ctx.SaveChangesAsync();

        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var handler = CreateHandler(ctx, hasher.Object);
        var result = await handler.Handle(new LoginCommand(account.Login, "pass"), CancellationToken.None);

        result.RequiresTelegramLink.Should().BeTrue();
        result.TelegramLinkToken.Should().NotBeNullOrEmpty();
        result.Token.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_TelegramLinked_ReturnsOtpSession()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account(telegramId: "tg_user_123");
        ctx.Accounts.Add(account);
        await ctx.SaveChangesAsync();

        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        var telegram = new Mock<ITelegramService>();
        telegram.Setup(t => t.SendOtpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var cache = new MemoryCache(new MemoryCacheOptions());
        var handler = CreateHandler(ctx, hasher.Object, telegram.Object, cache: cache);
        var result = await handler.Handle(new LoginCommand(account.Login, "pass"), CancellationToken.None);

        result.RequiresOtp.Should().BeTrue();
        result.SessionId.Should().NotBeNullOrEmpty();
        result.Token.Should().BeEmpty();

        cache.TryGetValue($"otp:{result.SessionId}", out OtpSession? session).Should().BeTrue();
        session!.AccountId.Should().Be(account.AccountID);
        session.Code.Should().HaveLength(6);
        telegram.Verify(t => t.SendOtpAsync("tg_user_123", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_TelegramLinked_ExistingTelegramLinkTokenPreserved()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account(telegramId: null);
        account.TelegramLinkToken = "EXISTING1";
        ctx.Accounts.Add(account);
        await ctx.SaveChangesAsync();

        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var handler = CreateHandler(ctx, hasher.Object);
        var result = await handler.Handle(new LoginCommand(account.Login, "pass"), CancellationToken.None);

        result.TelegramLinkToken.Should().Be("EXISTING1");
    }
}
