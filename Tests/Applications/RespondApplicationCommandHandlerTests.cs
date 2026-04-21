using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Applications.Commands.RespondApplication;
using Domain.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Tests.Helpers;

namespace Tests.Applications;

public class RespondApplicationCommandHandlerTests
{
    private static RespondApplicationCommandHandler CreateHandler(
        Infrastructure.Data.ApplicationDbContext ctx,
        ITelegramService? telegram = null)
    {
        return new RespondApplicationCommandHandler(ctx, telegram ?? Mock.Of<ITelegramService>());
    }

    [Fact]
    public async Task Handle_SetPendingManually_ThrowsInvalidOperation()
    {
        var ctx = TestDbContext.Create();
        var handler = CreateHandler(ctx);

        var act = () => handler.Handle(
            new RespondApplicationCommand(Guid.NewGuid(), Guid.NewGuid(), ApplicationStatus.Pending),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Pending*");
    }

    [Fact]
    public async Task Handle_ApplicationNotFound_ThrowsNotFoundException()
    {
        var ctx = TestDbContext.Create();
        var handler = CreateHandler(ctx);

        var act = () => handler.Handle(
            new RespondApplicationCommand(Guid.NewGuid(), Guid.NewGuid(), ApplicationStatus.Accepted),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WrongOwner_ThrowsUnauthorized()
    {
        var ctx = TestDbContext.Create();
        var owner = Fakes.Account();
        var applicant = Fakes.Account(login: "a@t.com");
        var skill = Fakes.Skill();
        var offer = Fakes.Offer(accountId: owner.AccountID, skillId: skill.SkillID);
        var app = Fakes.Application(applicantId: applicant.AccountID, offerId: offer.OfferID);
        ctx.Accounts.AddRange(owner, applicant);
        ctx.SkillsCatalog.Add(skill);
        ctx.SkillOffers.Add(offer);
        ctx.Applications.Add(app);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var act = () => handler.Handle(
            new RespondApplicationCommand(app.ApplicationID, Guid.NewGuid(), ApplicationStatus.Accepted),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*доступа*");
    }

    [Fact]
    public async Task Handle_AcceptOffer_CreatesDealAndDeactivatesOffer()
    {
        var ctx = TestDbContext.Create();
        var owner = Fakes.Account(telegramId: "owner_tg");
        var applicant = Fakes.Account(login: "a@t.com", telegramId: "applicant_tg");
        var skill = Fakes.Skill();
        var offer = Fakes.Offer(accountId: owner.AccountID, skillId: skill.SkillID);
        var app = Fakes.Application(applicantId: applicant.AccountID, offerId: offer.OfferID);
        ctx.Accounts.AddRange(owner, applicant);
        ctx.SkillsCatalog.Add(skill);
        ctx.SkillOffers.Add(offer);
        ctx.Applications.Add(app);
        await ctx.SaveChangesAsync();

        var telegram = new Mock<ITelegramService>();
        telegram.Setup(t => t.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler(ctx, telegram.Object);
        await handler.Handle(
            new RespondApplicationCommand(app.ApplicationID, owner.AccountID, ApplicationStatus.Accepted),
            CancellationToken.None);

        var updatedApp = await ctx.Applications.FindAsync(app.ApplicationID);
        updatedApp!.Status.Should().Be(ApplicationStatus.Accepted);

        var deal = await ctx.Deals.FirstOrDefaultAsync(d => d.ApplicationID == app.ApplicationID);
        deal.Should().NotBeNull();
        deal!.InitiatorID.Should().Be(applicant.AccountID);
        deal.PartnerID.Should().Be(owner.AccountID);
        deal.Status.Should().Be(DealStatus.Active);

        var updatedOffer = await ctx.SkillOffers.FindAsync(offer.OfferID);
        updatedOffer!.IsActive.Should().BeFalse();

        var notifications = ctx.Notifications.Where(n =>
            n.AccountID == applicant.AccountID || n.AccountID == owner.AccountID).ToList();
        notifications.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_RejectOffer_NoDealsCreated()
    {
        var ctx = TestDbContext.Create();
        var owner = Fakes.Account();
        var applicant = Fakes.Account(login: "a@t.com");
        var skill = Fakes.Skill();
        var offer = Fakes.Offer(accountId: owner.AccountID, skillId: skill.SkillID);
        var app = Fakes.Application(applicantId: applicant.AccountID, offerId: offer.OfferID);
        ctx.Accounts.AddRange(owner, applicant);
        ctx.SkillsCatalog.Add(skill);
        ctx.SkillOffers.Add(offer);
        ctx.Applications.Add(app);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        await handler.Handle(
            new RespondApplicationCommand(app.ApplicationID, owner.AccountID, ApplicationStatus.Rejected),
            CancellationToken.None);

        var updatedApp = await ctx.Applications.FindAsync(app.ApplicationID);
        updatedApp!.Status.Should().Be(ApplicationStatus.Rejected);

        ctx.Deals.Should().BeEmpty();

        var notification = ctx.Notifications.FirstOrDefault(n => n.AccountID == applicant.AccountID);
        notification.Should().NotBeNull();
        notification!.Type.Should().Be(Domain.Enums.NotificationType.ApplicationRejected);
    }

    [Fact]
    public async Task Handle_AlreadyProcessed_ThrowsInvalidOperation()
    {
        var ctx = TestDbContext.Create();
        var owner = Fakes.Account();
        var applicant = Fakes.Account(login: "a@t.com");
        var skill = Fakes.Skill();
        var offer = Fakes.Offer(accountId: owner.AccountID, skillId: skill.SkillID);
        var app = Fakes.Application(applicantId: applicant.AccountID, offerId: offer.OfferID, status: ApplicationStatus.Accepted);
        ctx.Accounts.AddRange(owner, applicant);
        ctx.SkillsCatalog.Add(skill);
        ctx.SkillOffers.Add(offer);
        ctx.Applications.Add(app);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var act = () => handler.Handle(
            new RespondApplicationCommand(app.ApplicationID, owner.AccountID, ApplicationStatus.Rejected),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*обработана*");
    }

    [Fact]
    public async Task Handle_AcceptRequest_CreatesDealAndKeepsRequestOpen()
    {
        var ctx = TestDbContext.Create();
        var requestOwner = Fakes.Account();
        var applicant = Fakes.Account(login: "a@t.com");
        var skill = Fakes.Skill();
        var skillRequest = Fakes.Request(accountId: requestOwner.AccountID, skillId: skill.SkillID);
        var app = Fakes.Application(applicantId: applicant.AccountID, requestId: skillRequest.RequestID);
        ctx.Accounts.AddRange(requestOwner, applicant);
        ctx.SkillsCatalog.Add(skill);
        ctx.SkillRequests.Add(skillRequest);
        ctx.Applications.Add(app);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        await handler.Handle(
            new RespondApplicationCommand(app.ApplicationID, requestOwner.AccountID, ApplicationStatus.Accepted),
            CancellationToken.None);

        var updatedRequest = await ctx.SkillRequests.FindAsync(skillRequest.RequestID);
        updatedRequest!.Status.Should().Be(RequestStatus.Open); // request stays open

        var deal = await ctx.Deals.FirstOrDefaultAsync(d => d.ApplicationID == app.ApplicationID);
        deal.Should().NotBeNull();
    }
}
