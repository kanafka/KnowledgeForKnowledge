using Application.Common.Interfaces;
using Application.Features.Auth.Commands.ForgotPassword;
using Application.Features.Auth.Commands.VerifyOtp;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Tests.Helpers;

namespace Tests.Auth;

public class ForgotPasswordCommandHandlerTests
{
    private static ForgotPasswordCommandHandler CreateHandler(
        Infrastructure.Data.ApplicationDbContext ctx,
        ITelegramService? telegram = null,
        IMemoryCache? cache = null)
    {
        return new ForgotPasswordCommandHandler(
            ctx,
            telegram ?? Mock.Of<ITelegramService>(),
            cache ?? new MemoryCache(new MemoryCacheOptions()));
    }

    [Fact]
    public async Task Handle_AccountNotFound_ReturnsEmptySessionSilently()
    {
        var ctx = TestDbContext.Create();
        var handler = CreateHandler(ctx);

        var result = await handler.Handle(new ForgotPasswordCommand("ghost@test.com"), CancellationToken.None);

        result.SessionId.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_AccountHasNoTelegram_ReturnsEmptySession()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account(telegramId: null);
        ctx.Accounts.Add(account);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(new ForgotPasswordCommand(account.Login), CancellationToken.None);

        result.SessionId.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ValidAccount_SendsCodeAndReturnSession()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account(telegramId: "tg_reset_user");
        ctx.Accounts.Add(account);
        await ctx.SaveChangesAsync();

        var telegram = new Mock<ITelegramService>();
        telegram.Setup(t => t.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cache = new MemoryCache(new MemoryCacheOptions());
        var handler = CreateHandler(ctx, telegram.Object, cache);
        var result = await handler.Handle(new ForgotPasswordCommand(account.Login), CancellationToken.None);

        result.SessionId.Should().NotBeNullOrEmpty();
        cache.TryGetValue($"reset:{result.SessionId}", out OtpSession? session).Should().BeTrue();
        session!.AccountId.Should().Be(account.AccountID);
        session.Code.Should().HaveLength(6);
        session.Attempts.Should().Be(0);

        telegram.Verify(t => t.SendMessageAsync("tg_reset_user", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
