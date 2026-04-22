using Application.Features.Deals.Queries.GetDealById;
using Application.Features.Deals.Queries.GetDeals;
using Application.Features.Deals.Queries.GetPublicDeals;
using Application.Common.Exceptions;
using Domain.Enums;
using FluentAssertions;
using Tests.Helpers;

namespace Tests.Deals;

public class GetDealsQueryHandlerTests
{
    private static GetDealsQueryHandler CreateHandler(Infrastructure.Data.ApplicationDbContext ctx) => new(ctx);

    private async Task<Infrastructure.Data.ApplicationDbContext> SeedDeal(
        Domain.Entities.Account initiator,
        Domain.Entities.Account partner,
        DealStatus status = DealStatus.Active)
    {
        var ctx = TestDbContext.Create();
        var skill = Fakes.Skill();
        var offer = Fakes.Offer(accountId: initiator.AccountID, skillId: skill.SkillID);
        var app = Fakes.Application(applicantId: partner.AccountID, offerId: offer.OfferID);
        var deal = Fakes.Deal(
            applicationId: app.ApplicationID,
            initiatorId: initiator.AccountID,
            partnerId: partner.AccountID,
            status: status);

        ctx.Accounts.AddRange(initiator, partner);
        ctx.SkillsCatalog.Add(skill);
        ctx.SkillOffers.Add(offer);
        ctx.Applications.Add(app);
        ctx.Deals.Add(deal);
        await ctx.SaveChangesAsync();
        return ctx;
    }

    [Fact]
    public async Task Handle_NoDeals_ReturnsEmpty()
    {
        var ctx = TestDbContext.Create();
        var handler = CreateHandler(ctx);
        var result = await handler.Handle(new GetDealsQuery(Guid.NewGuid()), CancellationToken.None);
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_InitiatorSeesTheirDeal()
    {
        var initiator = Fakes.Account();
        var partner = Fakes.Account(login: "p@t.com");
        var ctx = await SeedDeal(initiator, partner);
        var handler = CreateHandler(ctx);

        var result = await handler.Handle(new GetDealsQuery(initiator.AccountID), CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items[0].InitiatorID.Should().Be(initiator.AccountID);
    }

    [Fact]
    public async Task Handle_PartnerSeesTheirDeal()
    {
        var initiator = Fakes.Account();
        var partner = Fakes.Account(login: "p@t.com");
        var ctx = await SeedDeal(initiator, partner);
        var handler = CreateHandler(ctx);

        var result = await handler.Handle(new GetDealsQuery(partner.AccountID), CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items[0].PartnerID.Should().Be(partner.AccountID);
    }

    [Fact]
    public async Task Handle_OtherUser_SeesNoDeals()
    {
        var initiator = Fakes.Account();
        var partner = Fakes.Account(login: "p@t.com");
        var ctx = await SeedDeal(initiator, partner);
        var handler = CreateHandler(ctx);

        var result = await handler.Handle(new GetDealsQuery(Guid.NewGuid()), CancellationToken.None);

        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MyReviewExists_FlagIsTrue()
    {
        var initiator = Fakes.Account();
        var partner = Fakes.Account(login: "p@t.com");
        var ctx = await SeedDeal(initiator, partner, DealStatus.Completed);
        var deal = ctx.Deals.First();
        var review = Fakes.Review(dealId: deal.DealID, authorId: initiator.AccountID, targetId: partner.AccountID);
        ctx.Reviews.Add(review);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(new GetDealsQuery(initiator.AccountID), CancellationToken.None);

        result.Items[0].MyReviewExists.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NoReviewByMe_FlagIsFalse()
    {
        var initiator = Fakes.Account();
        var partner = Fakes.Account(login: "p@t.com");
        var ctx = await SeedDeal(initiator, partner, DealStatus.Completed);

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(new GetDealsQuery(initiator.AccountID), CancellationToken.None);

        result.Items[0].MyReviewExists.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ProfileName_ReturnsFullName()
    {
        var initiator = Fakes.Account();
        var partner = Fakes.Account(login: "p@t.com");
        var ctx = await SeedDeal(initiator, partner);
        ctx.UserProfiles.Add(Fakes.Profile(partner.AccountID, "Vasya Pupkin"));
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(new GetDealsQuery(initiator.AccountID), CancellationToken.None);

        result.Items[0].PartnerName.Should().Be("Vasya Pupkin");
    }

    [Fact]
    public async Task Handle_Pagination_Works()
    {
        var ctx = TestDbContext.Create();
        var initiator = Fakes.Account();
        var skill = Fakes.Skill();
        ctx.Accounts.Add(initiator);
        ctx.SkillsCatalog.Add(skill);

        for (var i = 0; i < 5; i++)
        {
            var partner = Fakes.Account(login: $"p{i}@t.com");
            var offer = Fakes.Offer(accountId: initiator.AccountID, skillId: skill.SkillID);
            var app = Fakes.Application(applicantId: partner.AccountID, offerId: offer.OfferID);
            var deal = Fakes.Deal(applicationId: app.ApplicationID, initiatorId: initiator.AccountID, partnerId: partner.AccountID);
            ctx.Accounts.Add(partner);
            ctx.SkillOffers.Add(offer);
            ctx.Applications.Add(app);
            ctx.Deals.Add(deal);
        }
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(new GetDealsQuery(initiator.AccountID, Page: 2, PageSize: 3), CancellationToken.None);

        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(5);
    }
}

public class GetDealByIdQueryHandlerTests
{
    private static GetDealByIdQueryHandler CreateHandler(Infrastructure.Data.ApplicationDbContext ctx) => new(ctx);

    private async Task<(Infrastructure.Data.ApplicationDbContext ctx, Domain.Entities.Deal deal,
        Domain.Entities.Account initiator, Domain.Entities.Account partner)> SeedOne()
    {
        var ctx = TestDbContext.Create();
        var initiator = Fakes.Account();
        var partner = Fakes.Account(login: "p@t.com");
        var skill = Fakes.Skill();
        var offer = Fakes.Offer(accountId: initiator.AccountID, skillId: skill.SkillID);
        var app = Fakes.Application(applicantId: partner.AccountID, offerId: offer.OfferID);
        var deal = Fakes.Deal(applicationId: app.ApplicationID, initiatorId: initiator.AccountID, partnerId: partner.AccountID);
        ctx.Accounts.AddRange(initiator, partner);
        ctx.SkillsCatalog.Add(skill);
        ctx.SkillOffers.Add(offer);
        ctx.Applications.Add(app);
        ctx.Deals.Add(deal);
        await ctx.SaveChangesAsync();
        return (ctx, deal, initiator, partner);
    }

    [Fact]
    public async Task Handle_NotFound_Throws()
    {
        var ctx = TestDbContext.Create();
        var handler = CreateHandler(ctx);
        var act = () => handler.Handle(new GetDealByIdQuery(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_NotParticipant_ThrowsUnauthorized()
    {
        var (ctx, deal, _, _) = await SeedOne();
        var handler = CreateHandler(ctx);
        var act = () => handler.Handle(new GetDealByIdQuery(deal.DealID, Guid.NewGuid()), CancellationToken.None);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_Initiator_ReturnsDetail()
    {
        var (ctx, deal, initiator, _) = await SeedOne();
        var handler = CreateHandler(ctx);
        var result = await handler.Handle(new GetDealByIdQuery(deal.DealID, initiator.AccountID), CancellationToken.None);
        result.DealID.Should().Be(deal.DealID);
        result.InitiatorID.Should().Be(initiator.AccountID);
    }

    [Fact]
    public async Task Handle_Partner_CanViewDeal()
    {
        var (ctx, deal, _, partner) = await SeedOne();
        var handler = CreateHandler(ctx);
        var result = await handler.Handle(new GetDealByIdQuery(deal.DealID, partner.AccountID), CancellationToken.None);
        result.PartnerID.Should().Be(partner.AccountID);
    }

    [Fact]
    public async Task Handle_WithReview_IncludesReviews()
    {
        var (ctx, deal, initiator, partner) = await SeedOne();
        var review = Fakes.Review(dealId: deal.DealID, authorId: initiator.AccountID, targetId: partner.AccountID, rating: 4);
        ctx.Reviews.Add(review);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(new GetDealByIdQuery(deal.DealID, initiator.AccountID), CancellationToken.None);

        result.Reviews.Should().HaveCount(1);
        result.Reviews[0].Rating.Should().Be(4);
    }
}

public class GetPublicDealsQueryHandlerTests
{
    private static GetPublicDealsQueryHandler CreateHandler(Infrastructure.Data.ApplicationDbContext ctx) => new(ctx);

    [Fact]
    public async Task Handle_NoDeals_ReturnsEmpty()
    {
        var ctx = TestDbContext.Create();
        var handler = CreateHandler(ctx);
        var result = await handler.Handle(new GetPublicDealsQuery(Guid.NewGuid()), CancellationToken.None);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ActiveDeal_NotReturned()
    {
        var ctx = TestDbContext.Create();
        var user = Fakes.Account();
        var other = Fakes.Account(login: "o@t.com");
        var skill = Fakes.Skill();
        var offer = Fakes.Offer(accountId: user.AccountID, skillId: skill.SkillID);
        var app = Fakes.Application(applicantId: other.AccountID, offerId: offer.OfferID);
        var deal = Fakes.Deal(applicationId: app.ApplicationID, initiatorId: user.AccountID, partnerId: other.AccountID, status: DealStatus.Active);
        ctx.Accounts.AddRange(user, other);
        ctx.SkillsCatalog.Add(skill);
        ctx.SkillOffers.Add(offer);
        ctx.Applications.Add(app);
        ctx.Deals.Add(deal);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(new GetPublicDealsQuery(user.AccountID), CancellationToken.None);

        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_CompletedDeal_IsReturned()
    {
        var ctx = TestDbContext.Create();
        var user = Fakes.Account();
        var other = Fakes.Account(login: "o@t.com");
        var skill = Fakes.Skill();
        var offer = Fakes.Offer(accountId: user.AccountID, skillId: skill.SkillID);
        var app = Fakes.Application(applicantId: other.AccountID, offerId: offer.OfferID);
        var deal = Fakes.Deal(applicationId: app.ApplicationID, initiatorId: user.AccountID, partnerId: other.AccountID, status: DealStatus.Completed);
        ctx.Accounts.AddRange(user, other);
        ctx.SkillsCatalog.Add(skill);
        ctx.SkillOffers.Add(offer);
        ctx.Applications.Add(app);
        ctx.Deals.Add(deal);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(new GetPublicDealsQuery(user.AccountID), CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items[0].Status.Should().Be("Completed");
    }

    [Fact]
    public async Task Handle_InitiatorView_ShowsPartnerName()
    {
        var ctx = TestDbContext.Create();
        var user = Fakes.Account();
        var other = Fakes.Account(login: "o@t.com");
        ctx.UserProfiles.Add(Fakes.Profile(other.AccountID, "Other Person"));
        var skill = Fakes.Skill();
        var offer = Fakes.Offer(accountId: user.AccountID, skillId: skill.SkillID);
        var app = Fakes.Application(applicantId: other.AccountID, offerId: offer.OfferID);
        var deal = Fakes.Deal(applicationId: app.ApplicationID, initiatorId: user.AccountID, partnerId: other.AccountID, status: DealStatus.Completed);
        ctx.Accounts.AddRange(user, other);
        ctx.SkillsCatalog.Add(skill);
        ctx.SkillOffers.Add(offer);
        ctx.Applications.Add(app);
        ctx.Deals.Add(deal);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(new GetPublicDealsQuery(user.AccountID), CancellationToken.None);

        result.Items[0].PartnerName.Should().Be("Other Person");
    }
}
