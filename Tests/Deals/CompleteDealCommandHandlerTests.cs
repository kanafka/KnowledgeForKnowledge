using Application.Common.Exceptions;
using Application.Features.Deals.Commands.CompleteDeal;
using Domain.Enums;
using FluentAssertions;
using Tests.Helpers;

namespace Tests.Deals;

public class CompleteDealCommandHandlerTests
{
    private static CompleteDealCommandHandler CreateHandler(Infrastructure.Data.ApplicationDbContext ctx)
        => new(ctx);

    [Fact]
    public async Task Handle_DealNotFound_ThrowsNotFoundException()
    {
        var ctx = TestDbContext.Create();
        var handler = CreateHandler(ctx);

        var act = () => handler.Handle(new CompleteDealCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

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
        var act = () => handler.Handle(new CompleteDealCommand(deal.DealID, Guid.NewGuid()), CancellationToken.None);

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
        var act = () => handler.Handle(new CompleteDealCommand(deal.DealID, initiator.AccountID), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*уже завершена*");
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
        var act = () => handler.Handle(new CompleteDealCommand(deal.DealID, initiator.AccountID), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*отменённую*");
    }

    [Fact]
    public async Task Handle_InitiatorCompletesActive_SetsCompletedByInitiator()
    {
        var ctx = TestDbContext.Create();
        var initiator = Fakes.Account();
        var partner = Fakes.Account(login: "p@t.com");
        ctx.Accounts.AddRange(initiator, partner);
        var deal = Fakes.Deal(initiatorId: initiator.AccountID, partnerId: partner.AccountID, status: DealStatus.Active);
        ctx.Deals.Add(deal);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        await handler.Handle(new CompleteDealCommand(deal.DealID, initiator.AccountID), CancellationToken.None);

        var updated = await ctx.Deals.FindAsync(deal.DealID);
        updated!.Status.Should().Be(DealStatus.CompletedByInitiator);
        updated.CompletedAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_PartnerCompletesActive_SetsCompletedByPartner()
    {
        var ctx = TestDbContext.Create();
        var initiator = Fakes.Account();
        var partner = Fakes.Account(login: "p@t.com");
        ctx.Accounts.AddRange(initiator, partner);
        var deal = Fakes.Deal(initiatorId: initiator.AccountID, partnerId: partner.AccountID, status: DealStatus.Active);
        ctx.Deals.Add(deal);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        await handler.Handle(new CompleteDealCommand(deal.DealID, partner.AccountID), CancellationToken.None);

        var updated = await ctx.Deals.FindAsync(deal.DealID);
        updated!.Status.Should().Be(DealStatus.CompletedByPartner);
    }

    [Fact]
    public async Task Handle_PartnerCompletesAfterInitiator_SetsCompletedAndTimestamp()
    {
        var ctx = TestDbContext.Create();
        var initiator = Fakes.Account();
        var partner = Fakes.Account(login: "p@t.com");
        ctx.Accounts.AddRange(initiator, partner);
        var deal = Fakes.Deal(initiatorId: initiator.AccountID, partnerId: partner.AccountID, status: DealStatus.CompletedByInitiator);
        ctx.Deals.Add(deal);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        await handler.Handle(new CompleteDealCommand(deal.DealID, partner.AccountID), CancellationToken.None);

        var updated = await ctx.Deals.FindAsync(deal.DealID);
        updated!.Status.Should().Be(DealStatus.Completed);
        updated.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_InitiatorCompletesAfterPartner_SetsCompleted()
    {
        var ctx = TestDbContext.Create();
        var initiator = Fakes.Account();
        var partner = Fakes.Account(login: "p@t.com");
        ctx.Accounts.AddRange(initiator, partner);
        var deal = Fakes.Deal(initiatorId: initiator.AccountID, partnerId: partner.AccountID, status: DealStatus.CompletedByPartner);
        ctx.Deals.Add(deal);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        await handler.Handle(new CompleteDealCommand(deal.DealID, initiator.AccountID), CancellationToken.None);

        var updated = await ctx.Deals.FindAsync(deal.DealID);
        updated!.Status.Should().Be(DealStatus.Completed);
        updated.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_InitiatorAlreadyMarkedComplete_ThrowsInvalidOperation()
    {
        var ctx = TestDbContext.Create();
        var initiator = Fakes.Account();
        var partner = Fakes.Account(login: "p@t.com");
        ctx.Accounts.AddRange(initiator, partner);
        var deal = Fakes.Deal(initiatorId: initiator.AccountID, partnerId: partner.AccountID, status: DealStatus.CompletedByInitiator);
        ctx.Deals.Add(deal);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var act = () => handler.Handle(new CompleteDealCommand(deal.DealID, initiator.AccountID), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*уже отметили*");
    }
}
