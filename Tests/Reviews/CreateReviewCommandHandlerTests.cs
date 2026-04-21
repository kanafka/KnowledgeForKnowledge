using Application.Common.Exceptions;
using Application.Features.Reviews.Commands.CreateReview;
using Domain.Enums;
using FluentAssertions;
using Tests.Helpers;

namespace Tests.Reviews;

public class CreateReviewCommandHandlerTests
{
    private static CreateReviewCommandHandler CreateHandler(Infrastructure.Data.ApplicationDbContext ctx)
        => new(ctx);

    [Fact]
    public async Task Handle_RatingTooLow_ThrowsInvalidOperation()
    {
        var ctx = TestDbContext.Create();
        var handler = CreateHandler(ctx);

        var act = () => handler.Handle(
            new CreateReviewCommand(Guid.NewGuid(), Guid.NewGuid(), 0, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*1 до 5*");
    }

    [Fact]
    public async Task Handle_RatingTooHigh_ThrowsInvalidOperation()
    {
        var ctx = TestDbContext.Create();
        var handler = CreateHandler(ctx);

        var act = () => handler.Handle(
            new CreateReviewCommand(Guid.NewGuid(), Guid.NewGuid(), 6, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*1 до 5*");
    }

    [Fact]
    public async Task Handle_DealNotFound_ThrowsNotFoundException()
    {
        var ctx = TestDbContext.Create();
        var handler = CreateHandler(ctx);

        var act = () => handler.Handle(
            new CreateReviewCommand(Guid.NewGuid(), Guid.NewGuid(), 4, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_CancelledDeal_ThrowsInvalidOperation()
    {
        var ctx = TestDbContext.Create();
        var initiator = Fakes.Account();
        var partner = Fakes.Account(login: "p@t.com");
        ctx.Accounts.AddRange(initiator, partner);
        var deal = Fakes.Deal(initiatorId: initiator.AccountID, partnerId: partner.AccountID, status: DealStatus.Cancelled);
        ctx.Deals.Add(deal);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var act = () => handler.Handle(
            new CreateReviewCommand(deal.DealID, initiator.AccountID, 5, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*отменённой*");
    }

    [Fact]
    public async Task Handle_NotParticipant_ThrowsUnauthorized()
    {
        var ctx = TestDbContext.Create();
        var initiator = Fakes.Account();
        var partner = Fakes.Account(login: "p@t.com");
        ctx.Accounts.AddRange(initiator, partner);
        var deal = Fakes.Deal(initiatorId: initiator.AccountID, partnerId: partner.AccountID, status: DealStatus.Completed);
        ctx.Deals.Add(deal);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var act = () => handler.Handle(
            new CreateReviewCommand(deal.DealID, Guid.NewGuid(), 5, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*участник*");
    }

    [Fact]
    public async Task Handle_DuplicateReview_ThrowsInvalidOperation()
    {
        var ctx = TestDbContext.Create();
        var initiator = Fakes.Account();
        var partner = Fakes.Account(login: "p@t.com");
        ctx.Accounts.AddRange(initiator, partner);
        var deal = Fakes.Deal(initiatorId: initiator.AccountID, partnerId: partner.AccountID, status: DealStatus.Completed);
        var existingReview = Fakes.Review(dealId: deal.DealID, authorId: initiator.AccountID, targetId: partner.AccountID);
        ctx.Deals.Add(deal);
        ctx.Reviews.Add(existingReview);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var act = () => handler.Handle(
            new CreateReviewCommand(deal.DealID, initiator.AccountID, 4, "Great"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*уже оставили*");
    }

    [Fact]
    public async Task Handle_InitiatorReviewsPartner_TargetIsPartner()
    {
        var ctx = TestDbContext.Create();
        var initiator = Fakes.Account();
        var partner = Fakes.Account(login: "p@t.com");
        ctx.Accounts.AddRange(initiator, partner);
        var deal = Fakes.Deal(initiatorId: initiator.AccountID, partnerId: partner.AccountID, status: DealStatus.Completed);
        ctx.Deals.Add(deal);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var reviewId = await handler.Handle(
            new CreateReviewCommand(deal.DealID, initiator.AccountID, 5, "Excellent"),
            CancellationToken.None);

        reviewId.Should().NotBeEmpty();
        var review = await ctx.Reviews.FindAsync(reviewId);
        review.Should().NotBeNull();
        review!.AuthorID.Should().Be(initiator.AccountID);
        review.TargetID.Should().Be(partner.AccountID);
        review.Rating.Should().Be(5);
        review.Comment.Should().Be("Excellent");
    }

    [Fact]
    public async Task Handle_PartnerReviewsInitiator_TargetIsInitiator()
    {
        var ctx = TestDbContext.Create();
        var initiator = Fakes.Account();
        var partner = Fakes.Account(login: "p@t.com");
        ctx.Accounts.AddRange(initiator, partner);
        var deal = Fakes.Deal(initiatorId: initiator.AccountID, partnerId: partner.AccountID, status: DealStatus.Completed);
        ctx.Deals.Add(deal);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var reviewId = await handler.Handle(
            new CreateReviewCommand(deal.DealID, partner.AccountID, 3, null),
            CancellationToken.None);

        var review = await ctx.Reviews.FindAsync(reviewId);
        review!.AuthorID.Should().Be(partner.AccountID);
        review.TargetID.Should().Be(initiator.AccountID);
        review.Rating.Should().Be(3);
    }

    [Fact]
    public async Task Handle_BothParticipantsReview_BothSucceed()
    {
        var ctx = TestDbContext.Create();
        var initiator = Fakes.Account();
        var partner = Fakes.Account(login: "p@t.com");
        ctx.Accounts.AddRange(initiator, partner);
        var deal = Fakes.Deal(initiatorId: initiator.AccountID, partnerId: partner.AccountID, status: DealStatus.Completed);
        ctx.Deals.Add(deal);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var r1 = await handler.Handle(new CreateReviewCommand(deal.DealID, initiator.AccountID, 5, null), CancellationToken.None);
        var r2 = await handler.Handle(new CreateReviewCommand(deal.DealID, partner.AccountID, 4, null), CancellationToken.None);

        r1.Should().NotBeEmpty();
        r2.Should().NotBeEmpty();
        ctx.Reviews.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_ActiveDeal_AllowsReview()
    {
        var ctx = TestDbContext.Create();
        var initiator = Fakes.Account();
        var partner = Fakes.Account(login: "p@t.com");
        ctx.Accounts.AddRange(initiator, partner);
        var deal = Fakes.Deal(initiatorId: initiator.AccountID, partnerId: partner.AccountID, status: DealStatus.Active);
        ctx.Deals.Add(deal);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var reviewId = await handler.Handle(
            new CreateReviewCommand(deal.DealID, initiator.AccountID, 4, null),
            CancellationToken.None);

        reviewId.Should().NotBeEmpty();
    }
}
