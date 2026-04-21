using Application.Common.Exceptions;
using Application.Features.SkillRequests.Commands.CreateSkillRequest;
using Domain.Enums;
using FluentAssertions;
using FluentValidation;
using Tests.Helpers;

namespace Tests.SkillRequests;

public class CreateSkillRequestCommandHandlerTests
{
    private static CreateSkillRequestCommandHandler CreateHandler(Infrastructure.Data.ApplicationDbContext ctx)
        => new(ctx);

    [Fact]
    public async Task Handle_SkillNotFound_ThrowsNotFoundException()
    {
        var ctx = TestDbContext.Create();
        var handler = CreateHandler(ctx);

        var act = () => handler.Handle(
            new CreateSkillRequestCommand(Guid.NewGuid(), Guid.NewGuid(), "Title", null),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_NoProfile_ThrowsValidationException()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        var skill = Fakes.Skill();
        ctx.Accounts.Add(account);
        ctx.SkillsCatalog.Add(skill);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var act = () => handler.Handle(
            new CreateSkillRequestCommand(account.AccountID, skill.SkillID, "Need help with Python", null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .Where(e => e.Message.Contains("профиль"));
    }

    [Fact]
    public async Task Handle_Valid_CreatesRequestWithOpenStatus()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        var skill = Fakes.Skill();
        var profile = Fakes.Profile(account.AccountID);
        ctx.Accounts.Add(account);
        ctx.SkillsCatalog.Add(skill);
        ctx.UserProfiles.Add(profile);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var requestId = await handler.Handle(
            new CreateSkillRequestCommand(account.AccountID, skill.SkillID, "Need Python help", "More details"),
            CancellationToken.None);

        requestId.Should().NotBeEmpty();
        var request = await ctx.SkillRequests.FindAsync(requestId);
        request.Should().NotBeNull();
        request!.Status.Should().Be(RequestStatus.Open);
        request.Title.Should().Be("Need Python help");
        request.Details.Should().Be("More details");
        request.AccountID.Should().Be(account.AccountID);
        request.SkillID.Should().Be(skill.SkillID);
    }
}
