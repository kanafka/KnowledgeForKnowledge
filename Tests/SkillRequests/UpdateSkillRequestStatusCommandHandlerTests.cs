using Application.Common.Exceptions;
using Application.Features.SkillRequests.Commands.UpdateSkillRequestStatus;
using Domain.Enums;
using FluentAssertions;
using Tests.Helpers;

namespace Tests.SkillRequests;

public class UpdateSkillRequestStatusCommandHandlerTests
{
    private static UpdateSkillRequestStatusCommandHandler CreateHandler(Infrastructure.Data.ApplicationDbContext ctx)
        => new(ctx);

    [Fact]
    public async Task Handle_RequestNotFound_ThrowsNotFoundException()
    {
        var ctx = TestDbContext.Create();
        var handler = CreateHandler(ctx);

        var act = () => handler.Handle(
            new UpdateSkillRequestStatusCommand(Guid.NewGuid(), Guid.NewGuid(), RequestStatus.Closed),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WrongOwner_ThrowsUnauthorized()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        var skill = Fakes.Skill();
        var request = Fakes.Request(accountId: account.AccountID, skillId: skill.SkillID);
        ctx.Accounts.Add(account);
        ctx.SkillsCatalog.Add(skill);
        ctx.SkillRequests.Add(request);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var act = () => handler.Handle(
            new UpdateSkillRequestStatusCommand(request.RequestID, Guid.NewGuid(), RequestStatus.Closed),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*доступа*");
    }

    [Fact]
    public async Task Handle_Owner_UpdatesStatus()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        var skill = Fakes.Skill();
        var request = Fakes.Request(accountId: account.AccountID, skillId: skill.SkillID, status: RequestStatus.Open);
        ctx.Accounts.Add(account);
        ctx.SkillsCatalog.Add(skill);
        ctx.SkillRequests.Add(request);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        await handler.Handle(
            new UpdateSkillRequestStatusCommand(request.RequestID, account.AccountID, RequestStatus.Closed),
            CancellationToken.None);

        var updated = await ctx.SkillRequests.FindAsync(request.RequestID);
        updated!.Status.Should().Be(RequestStatus.Closed);
    }

    [Fact]
    public async Task Handle_CanReopenRequest()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        var skill = Fakes.Skill();
        var request = Fakes.Request(accountId: account.AccountID, skillId: skill.SkillID, status: RequestStatus.Closed);
        ctx.Accounts.Add(account);
        ctx.SkillsCatalog.Add(skill);
        ctx.SkillRequests.Add(request);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        await handler.Handle(
            new UpdateSkillRequestStatusCommand(request.RequestID, account.AccountID, RequestStatus.Open),
            CancellationToken.None);

        var updated = await ctx.SkillRequests.FindAsync(request.RequestID);
        updated!.Status.Should().Be(RequestStatus.Open);
    }
}
