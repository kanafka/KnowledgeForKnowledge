using Application.Common.Interfaces;
using Application.Features.Auth.Commands.VerifyOtp;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Tests.Helpers;

namespace Tests.Auth;

public class VerifyOtpCommandHandlerTests
{
    private static VerifyOtpCommandHandler CreateHandler(
        Infrastructure.Data.ApplicationDbContext ctx,
        IMemoryCache cache,
        IJwtService? jwt = null)
    {
        return new VerifyOtpCommandHandler(ctx, jwt ?? Mock.Of<IJwtService>(), cache);
    }

    [Fact]
    public async Task Handle_SessionNotFound_ThrowsUnauthorized()
    {
        var ctx = TestDbContext.Create();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var handler = CreateHandler(ctx, cache);

        var act = () => handler.Handle(new VerifyOtpCommand("nosession", "123456"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*не найдена*");
    }

    [Fact]
    public async Task Handle_MaxAttemptsExceeded_RemovesSessionAndThrows()
    {
        var ctx = TestDbContext.Create();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var sessionId = "sess1";
        var account = Fakes.Account();
        ctx.Accounts.Add(account);
        await ctx.SaveChangesAsync();

        cache.Set($"otp:{sessionId}", new OtpSession(account.AccountID, "111111", 5));
        var handler = CreateHandler(ctx, cache);

        var act = () => handler.Handle(new VerifyOtpCommand(sessionId, "111111"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Превышено число попыток*");

        cache.TryGetValue($"otp:{sessionId}", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WrongCode_IncrementsAttempts()
    {
        var ctx = TestDbContext.Create();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var sessionId = "sess2";
        var account = Fakes.Account();
        ctx.Accounts.Add(account);
        await ctx.SaveChangesAsync();

        cache.Set($"otp:{sessionId}", new OtpSession(account.AccountID, "111111", 0));
        var handler = CreateHandler(ctx, cache);

        var act = () => handler.Handle(new VerifyOtpCommand(sessionId, "999999"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Неверный код*");

        cache.TryGetValue($"otp:{sessionId}", out OtpSession? updated).Should().BeTrue();
        updated!.Attempts.Should().Be(1);
    }

    [Fact]
    public async Task Handle_CorrectCode_ReturnsTokenAndRemovesSession()
    {
        var ctx = TestDbContext.Create();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var sessionId = "sess3";
        var account = Fakes.Account();
        ctx.Accounts.Add(account);
        await ctx.SaveChangesAsync();

        cache.Set($"otp:{sessionId}", new OtpSession(account.AccountID, "123456", 2));

        var jwt = new Mock<IJwtService>();
        jwt.Setup(j => j.GenerateToken(It.IsAny<Domain.Entities.Account>())).Returns("jwt_token");

        var handler = CreateHandler(ctx, cache, jwt.Object);
        var result = await handler.Handle(new VerifyOtpCommand(sessionId, "123456"), CancellationToken.None);

        result.Token.Should().Be("jwt_token");
        result.AccountId.Should().Be(account.AccountID);
        result.IsAdmin.Should().BeFalse();

        cache.TryGetValue($"otp:{sessionId}", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_CorrectCode_AccountNotInDb_ThrowsUnauthorized()
    {
        var ctx = TestDbContext.Create();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var sessionId = "sess4";
        var missingId = Guid.NewGuid();

        cache.Set($"otp:{sessionId}", new OtpSession(missingId, "123456", 0));
        var handler = CreateHandler(ctx, cache);

        var act = () => handler.Handle(new VerifyOtpCommand(sessionId, "123456"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Аккаунт не найден*");
    }
}
