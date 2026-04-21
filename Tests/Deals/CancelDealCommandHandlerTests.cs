using Application.Common.Exceptions;
using Application.Features.Deals.Commands.CancelDeal;
using Domain.Enums;
using FluentAssertions;
using Tests.Helpers;

namespace Tests.Deals;

public class CancelDealCommandHandlerTests
{
    private static CancelDealCommandHandler CreateHandler(Infrastructure.Data.ApplicationDbContext ctx)
        => new(ctx);

    [Fact]
    public async Task Handle_DealNotFound_ThrowsNotFoundException()
    {
        var ctx = TestDbContext.Create();
        var handler = CreateHandler(ctx);

        var act = () => handler.Handle(new CancelDealCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_NotParticipant_ThrowsUnauthorized()
    {
        var ctx = TestDbContext.Create();
        var deal = Fakes.Deal();
        ctx.Deals.Add(deal);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var act = () => handler.Handle(new CancelDealCommand(deal.DealID, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*доступа*");
    }

    [Fact]
    public async Task Handle_AlreadyCompleted_ThrowsInvalidOperation()
    {
        var ctx = TestDbContext.Create();
        var initiator = Fakes.Account();
        ctx.Accounts.Add(initiator);
        var deal = Fakes.Deal(initiatorId: initiator.AccountID, status: DealStatus.Completed);
        ctx.Deals.Add(deal);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var act = () => handler.Handle(new CancelDealCommand(deal.DealID, initiator.AccountID), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*завершённую*");
    }

    [Fact]
    public async Task Handle_AlreadyCancelled_ThrowsInvalidOperation()
    {
        var ctx = TestDbContext.Create();
        var initiator = Fakes.Account();
        ctx.Accounts.Add(initiator);
        var deal = Fakes.Deal(initiatorId: initiator.AccountID, status: DealStatus.Cancelled);
        ctx.Deals.Add(deal);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var act = () => handler.Handle(new CancelDealCommand(deal.DealID, initiator.AccountID), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*уже отменена*");
    }

    [Fact]
    public async Task Handle_InitiatorCancels_SetsCancelled()
    {
        var ctx = TestDbContext.Create();
        var initiator = Fakes.Account();
        var partner = Fakes.Account(login: "p@t.com");
        ctx.Accounts.AddRange(initiator, partner);
        var deal = Fakes.Deal(initiatorId: initiator.AccountID, partnerId: partner.AccountID, status: DealStatus.Active);
        ctx.Deals.Add(deal);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        await handler.Handle(new CancelDealCommand(deal.DealID, initiator.AccountID), CancellationToken.None);

        var updated = await ctx.Deals.FindAsync(deal.DealID);
        updated!.Status.Should().Be(DealStatus.Cancelled);
    }

    [Fact]
    public async Task Handle_PartnerCancels_SetsCancelled()
    {
        var ctx = TestDbContext.Create();
        var initiator = Fakes.Account();
        var partner = Fakes.Account(login: "p@t.com");
        ctx.Accounts.AddRange(initiator, partner);
        var deal = Fakes.Deal(initiatorId: initiator.AccountID, partnerId: partner.AccountID, status: DealStatus.Active);
        ctx.Deals.Add(deal);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        await handler.Handle(new CancelDealCommand(deal.DealID, partner.AccountID), CancellationToken.None);

        var updated = await ctx.Deals.FindAsync(deal.DealID);
        updated!.Status.Should().Be(DealStatus.Cancelled);
    }

    [Fact]
    public async Task Handle_PartialCompletionState_CanBeCancelled()
    {
        var ctx = TestDbContext.Create();
        var initiator = Fakes.Account();
        var partner = Fakes.Account(login: "p@t.com");
        ctx.Accounts.AddRange(initiator, partner);
        var deal = Fakes.Deal(initiatorId: initiator.AccountID, partnerId: partner.AccountID, status: DealStatus.CompletedByInitiator);
        ctx.Deals.Add(deal);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        await handler.Handle(new CancelDealCommand(deal.DealID, partner.AccountID), CancellationToken.None);

        var updated = await ctx.Deals.FindAsync(deal.DealID);
        updated!.Status.Should().Be(DealStatus.Cancelled);
    }
}
