using Application.Common.Interfaces;
using Application.Features.Auth.Commands.ResetPassword;
using Application.Features.Auth.Commands.VerifyOtp;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Tests.Helpers;

namespace Tests.Auth;

public class ResetPasswordCommandHandlerTests
{
    private static ResetPasswordCommandHandler CreateHandler(
        Infrastructure.Data.ApplicationDbContext ctx,
        IPasswordHasher? hasher = null,
        IMemoryCache? cache = null)
    {
        return new ResetPasswordCommandHandler(
            ctx,
            hasher ?? Mock.Of<IPasswordHasher>(),
            cache ?? new MemoryCache(new MemoryCacheOptions()));
    }

    [Fact]
    public async Task Handle_ShortPassword_ThrowsInvalidOperation()
    {
        var ctx = TestDbContext.Create();
        var handler = CreateHandler(ctx);

        var act = () => handler.Handle(new ResetPasswordCommand("sess", "123456", "short"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*не менее 8*");
    }

    [Fact]
    public async Task Handle_SessionNotFound_ThrowsUnauthorized()
    {
        var ctx = TestDbContext.Create();
        var handler = CreateHandler(ctx);

        var act = () => handler.Handle(new ResetPasswordCommand("nosession", "123456", "newpassword123"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*не найдена*");
    }

    [Fact]
    public async Task Handle_MaxAttemptsExceeded_ThrowsUnauthorized()
    {
        var ctx = TestDbContext.Create();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var sessionId = "rs1";
        cache.Set($"reset:{sessionId}", new OtpSession(Guid.NewGuid(), "111111", 5));

        var handler = CreateHandler(ctx, cache: cache);
        var act = () => handler.Handle(new ResetPasswordCommand(sessionId, "111111", "newpassword123"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Превышено*");

        cache.TryGetValue($"reset:{sessionId}", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WrongCode_IncrementsAttempts()
    {
        var ctx = TestDbContext.Create();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var sessionId = "rs2";
        cache.Set($"reset:{sessionId}", new OtpSession(Guid.NewGuid(), "111111", 0));

        var handler = CreateHandler(ctx, cache: cache);
        var act = () => handler.Handle(new ResetPasswordCommand(sessionId, "999999", "newpassword123"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Неверный код*");

        cache.TryGetValue($"reset:{sessionId}", out OtpSession? updated).Should().BeTrue();
        updated!.Attempts.Should().Be(1);
    }

    [Fact]
    public async Task Handle_CorrectCode_UpdatesPasswordAndClearsLockout()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        account.FailedLoginAttempts = 3;
        account.LockoutUntil = DateTime.UtcNow.AddMinutes(5);
        ctx.Accounts.Add(account);
        await ctx.SaveChangesAsync();

        var cache = new MemoryCache(new MemoryCacheOptions());
        var sessionId = "rs3";
        cache.Set($"reset:{sessionId}", new OtpSession(account.AccountID, "111111", 0));

        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Hash("newpassword123")).Returns("new_hash");

        var handler = CreateHandler(ctx, hasher.Object, cache);
        await handler.Handle(new ResetPasswordCommand(sessionId, "111111", "newpassword123"), CancellationToken.None);

        var updated = await ctx.Accounts.FindAsync(account.AccountID);
        updated!.PasswordHash.Should().Be("new_hash");
        updated.FailedLoginAttempts.Should().Be(0);
        updated.LockoutUntil.Should().BeNull();

        cache.TryGetValue($"reset:{sessionId}", out _).Should().BeFalse();
    }
}
