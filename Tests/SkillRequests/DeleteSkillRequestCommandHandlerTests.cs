using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.SkillRequests.Commands.DeleteSkillRequest;
using FluentAssertions;
using Moq;
using Tests.Helpers;

namespace Tests.SkillRequests;

public class DeleteSkillRequestCommandHandlerTests
{
    private static DeleteSkillRequestCommandHandler CreateHandler(
        Infrastructure.Data.ApplicationDbContext ctx,
        ITelegramService? telegram = null)
    {
        return new DeleteSkillRequestCommandHandler(ctx, telegram ?? Mock.Of<ITelegramService>());
    }

    [Fact]
    public async Task Handle_RequestNotFound_ThrowsNotFoundException()
    {
        var ctx = TestDbContext.Create();
        var handler = CreateHandler(ctx);

        var act = () => handler.Handle(
            new DeleteSkillRequestCommand(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_NotOwnerNotAdmin_ThrowsUnauthorized()
    {
        var ctx = TestDbContext.Create();
        var owner = Fakes.Account();
        var skill = Fakes.Skill();
        var request = Fakes.Request(accountId: owner.AccountID, skillId: skill.SkillID);
        ctx.Accounts.Add(owner);
        ctx.SkillsCatalog.Add(skill);
        ctx.SkillRequests.Add(request);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var act = () => handler.Handle(
            new DeleteSkillRequestCommand(request.RequestID, Guid.NewGuid(), IsAdmin: false),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*чужой*");
    }

    [Fact]
    public async Task Handle_OwnerDeletes_RemovesRequestAndPendingApplications()
    {
        var ctx = TestDbContext.Create();
        var owner = Fakes.Account();
        var applicant = Fakes.Account(login: "a@t.com");
        var skill = Fakes.Skill();
        var skillRequest = Fakes.Request(accountId: owner.AccountID, skillId: skill.SkillID);
        var pendingApp = Fakes.Application(applicantId: applicant.AccountID, requestId: skillRequest.RequestID);
        ctx.Accounts.AddRange(owner, applicant);
        ctx.SkillsCatalog.Add(skill);
        ctx.SkillRequests.Add(skillRequest);
        ctx.Applications.Add(pendingApp);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        await handler.Handle(
            new DeleteSkillRequestCommand(skillRequest.RequestID, owner.AccountID),
            CancellationToken.None);

        var deletedRequest = await ctx.SkillRequests.FindAsync(skillRequest.RequestID);
        deletedRequest.Should().BeNull();

        var deletedApp = await ctx.Applications.FindAsync(pendingApp.ApplicationID);
        deletedApp.Should().BeNull();
    }

    [Fact]
    public async Task Handle_AdminDeletes_NotifiesOwnerViaTelegram()
    {
        var ctx = TestDbContext.Create();
        var admin = Fakes.Account(isAdmin: true);
        var owner = Fakes.Account(login: "owner@t.com", telegramId: "owner_tg");
        var skill = Fakes.Skill();
        var skillRequest = Fakes.Request(accountId: owner.AccountID, skillId: skill.SkillID);
        ctx.Accounts.AddRange(admin, owner);
        ctx.SkillsCatalog.Add(skill);
        ctx.SkillRequests.Add(skillRequest);
        await ctx.SaveChangesAsync();

        var telegram = new Mock<ITelegramService>();
        telegram.Setup(t => t.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler(ctx, telegram.Object);
        await handler.Handle(
            new DeleteSkillRequestCommand(skillRequest.RequestID, admin.AccountID, IsAdmin: true, DeletionReason: "Spam"),
            CancellationToken.None);

        telegram.Verify(t => t.SendMessageAsync("owner_tg", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ApplicationsWithDeals_AreNotDeleted()
    {
        var ctx = TestDbContext.Create();
        var owner = Fakes.Account();
        var applicant = Fakes.Account(login: "a@t.com");
        var skill = Fakes.Skill();
        var skillRequest = Fakes.Request(accountId: owner.AccountID, skillId: skill.SkillID);
        var app = Fakes.Application(applicantId: applicant.AccountID, requestId: skillRequest.RequestID, status: Domain.Enums.ApplicationStatus.Accepted);
        var deal = Fakes.Deal(applicationId: app.ApplicationID, initiatorId: applicant.AccountID, partnerId: owner.AccountID);
        ctx.Accounts.AddRange(owner, applicant);
        ctx.SkillsCatalog.Add(skill);
        ctx.SkillRequests.Add(skillRequest);
        ctx.Applications.Add(app);
        ctx.Deals.Add(deal);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        await handler.Handle(
            new DeleteSkillRequestCommand(skillRequest.RequestID, owner.AccountID),
            CancellationToken.None);

        var keptApp = await ctx.Applications.FindAsync(app.ApplicationID);
        keptApp.Should().NotBeNull();
    }
}
