using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.SkillOffers.Commands.DeleteSkillOffer;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Tests.Helpers;

namespace Tests.SkillOffers;

public class DeleteSkillOfferCommandHandlerTests
{
    private static DeleteSkillOfferCommandHandler CreateHandler(
        Infrastructure.Data.ApplicationDbContext ctx,
        ITelegramService? telegram = null)
    {
        return new DeleteSkillOfferCommandHandler(ctx, telegram ?? Mock.Of<ITelegramService>());
    }

    [Fact]
    public async Task Handle_OfferNotFound_ThrowsNotFoundException()
    {
        var ctx = TestDbContext.Create();
        var handler = CreateHandler(ctx);

        var act = () => handler.Handle(
            new DeleteSkillOfferCommand(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_NotOwnerNotAdmin_ThrowsUnauthorized()
    {
        var ctx = TestDbContext.Create();
        var owner = Fakes.Account();
        var skill = Fakes.Skill();
        var offer = Fakes.Offer(accountId: owner.AccountID, skillId: skill.SkillID);
        ctx.Accounts.Add(owner);
        ctx.SkillsCatalog.Add(skill);
        ctx.SkillOffers.Add(offer);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var act = () => handler.Handle(
            new DeleteSkillOfferCommand(offer.OfferID, Guid.NewGuid(), IsAdmin: false),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*доступа*");
    }

    [Fact]
    public async Task Handle_OwnerDeletes_RemovesOfferAndPendingApplications()
    {
        var ctx = TestDbContext.Create();
        var owner = Fakes.Account();
        var skill = Fakes.Skill();
        var offer = Fakes.Offer(accountId: owner.AccountID, skillId: skill.SkillID);
        var applicant = Fakes.Account(login: "a@t.com");
        var pendingApp = Fakes.Application(applicantId: applicant.AccountID, offerId: offer.OfferID);
        ctx.Accounts.AddRange(owner, applicant);
        ctx.SkillsCatalog.Add(skill);
        ctx.SkillOffers.Add(offer);
        ctx.Applications.Add(pendingApp);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        await handler.Handle(
            new DeleteSkillOfferCommand(offer.OfferID, owner.AccountID, IsAdmin: false),
            CancellationToken.None);

        var deletedOffer = await ctx.SkillOffers.FindAsync(offer.OfferID);
        deletedOffer.Should().BeNull();

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
        var offer = Fakes.Offer(accountId: owner.AccountID, skillId: skill.SkillID);
        ctx.Accounts.AddRange(admin, owner);
        ctx.SkillsCatalog.Add(skill);
        ctx.SkillOffers.Add(offer);
        await ctx.SaveChangesAsync();

        var telegram = new Mock<ITelegramService>();
        telegram.Setup(t => t.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler(ctx, telegram.Object);
        await handler.Handle(
            new DeleteSkillOfferCommand(offer.OfferID, admin.AccountID, IsAdmin: true, DeletionReason: "Violations"),
            CancellationToken.None);

        telegram.Verify(t => t.SendMessageAsync("owner_tg", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DeletedOffer_ApplicationsWithDealsAreKept()
    {
        var ctx = TestDbContext.Create();
        var owner = Fakes.Account();
        var applicant = Fakes.Account(login: "a@t.com");
        var skill = Fakes.Skill();
        var offer = Fakes.Offer(accountId: owner.AccountID, skillId: skill.SkillID);
        var app = Fakes.Application(applicantId: applicant.AccountID, offerId: offer.OfferID, status: Domain.Enums.ApplicationStatus.Accepted);
        var deal = Fakes.Deal(applicationId: app.ApplicationID, initiatorId: applicant.AccountID, partnerId: owner.AccountID);
        ctx.Accounts.AddRange(owner, applicant);
        ctx.SkillsCatalog.Add(skill);
        ctx.SkillOffers.Add(offer);
        ctx.Applications.Add(app);
        ctx.Deals.Add(deal);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        await handler.Handle(
            new DeleteSkillOfferCommand(offer.OfferID, owner.AccountID, IsAdmin: false),
            CancellationToken.None);

        // Application with deal should still exist
        var keptApp = await ctx.Applications.FindAsync(app.ApplicationID);
        keptApp.Should().NotBeNull();

        var keptDeal = await ctx.Deals.FindAsync(deal.DealID);
        keptDeal.Should().NotBeNull();
    }
}
