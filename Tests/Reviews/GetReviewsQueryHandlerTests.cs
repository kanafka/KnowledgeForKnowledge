using Application.Features.Reviews.Queries.GetReviews;
using FluentAssertions;
using Tests.Helpers;

namespace Tests.Reviews;

public class GetReviewsQueryHandlerTests
{
    private static GetReviewsQueryHandler CreateHandler(Infrastructure.Data.ApplicationDbContext ctx) => new(ctx);

    [Fact]
    public async Task Handle_NoReviews_ReturnsEmpty()
    {
        var ctx = TestDbContext.Create();
        var handler = CreateHandler(ctx);
        var result = await handler.Handle(new GetReviewsQuery(Guid.NewGuid()), CancellationToken.None);
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.AverageRating.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithReviews_ReturnsThem()
    {
        var ctx = TestDbContext.Create();
        var target = Fakes.Account();
        var author = Fakes.Account(login: "a@t.com");
        ctx.Accounts.AddRange(target, author);
        var deal = Fakes.Deal(initiatorId: author.AccountID, partnerId: target.AccountID);
        ctx.Deals.Add(deal);
        ctx.Reviews.Add(Fakes.Review(dealId: deal.DealID, authorId: author.AccountID, targetId: target.AccountID, rating: 5));
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(new GetReviewsQuery(target.AccountID), CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items[0].Rating.Should().Be(5);
    }

    [Fact]
    public async Task Handle_AverageRating_CalculatedCorrectly()
    {
        var ctx = TestDbContext.Create();
        var target = Fakes.Account();
        var a1 = Fakes.Account(login: "a1@t.com");
        var a2 = Fakes.Account(login: "a2@t.com");
        ctx.Accounts.AddRange(target, a1, a2);
        var deal1 = Fakes.Deal(initiatorId: a1.AccountID, partnerId: target.AccountID);
        var deal2 = Fakes.Deal(initiatorId: a2.AccountID, partnerId: target.AccountID);
        ctx.Deals.AddRange(deal1, deal2);
        ctx.Reviews.Add(Fakes.Review(dealId: deal1.DealID, authorId: a1.AccountID, targetId: target.AccountID, rating: 4));
        ctx.Reviews.Add(Fakes.Review(dealId: deal2.DealID, authorId: a2.AccountID, targetId: target.AccountID, rating: 2));
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(new GetReviewsQuery(target.AccountID), CancellationToken.None);

        result.AverageRating.Should().Be(3.0);
    }

    [Fact]
    public async Task Handle_OnlyTargetAccountReviews_Returned()
    {
        var ctx = TestDbContext.Create();
        var target = Fakes.Account();
        var other = Fakes.Account(login: "o@t.com");
        var author = Fakes.Account(login: "a@t.com");
        ctx.Accounts.AddRange(target, other, author);
        var deal1 = Fakes.Deal(initiatorId: author.AccountID, partnerId: target.AccountID);
        var deal2 = Fakes.Deal(initiatorId: author.AccountID, partnerId: other.AccountID);
        ctx.Deals.AddRange(deal1, deal2);
        ctx.Reviews.Add(Fakes.Review(dealId: deal1.DealID, authorId: author.AccountID, targetId: target.AccountID, rating: 5));
        ctx.Reviews.Add(Fakes.Review(dealId: deal2.DealID, authorId: author.AccountID, targetId: other.AccountID, rating: 3));
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(new GetReviewsQuery(target.AccountID), CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items[0].AuthorID.Should().Be(author.AccountID);
    }

    [Fact]
    public async Task Handle_AuthorWithProfile_ReturnsProfileName()
    {
        var ctx = TestDbContext.Create();
        var target = Fakes.Account();
        var author = Fakes.Account(login: "a@t.com");
        ctx.Accounts.AddRange(target, author);
        ctx.UserProfiles.Add(Fakes.Profile(author.AccountID, "Ivan Petrov"));
        var deal = Fakes.Deal(initiatorId: author.AccountID, partnerId: target.AccountID);
        ctx.Deals.Add(deal);
        ctx.Reviews.Add(Fakes.Review(dealId: deal.DealID, authorId: author.AccountID, targetId: target.AccountID));
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(new GetReviewsQuery(target.AccountID), CancellationToken.None);

        result.Items[0].AuthorName.Should().Be("Ivan Petrov");
    }

    [Fact]
    public async Task Handle_AuthorWithoutProfile_ReturnsLogin()
    {
        var ctx = TestDbContext.Create();
        var target = Fakes.Account();
        var author = Fakes.Account(login: "author@t.com");
        ctx.Accounts.AddRange(target, author);
        var deal = Fakes.Deal(initiatorId: author.AccountID, partnerId: target.AccountID);
        ctx.Deals.Add(deal);
        ctx.Reviews.Add(Fakes.Review(dealId: deal.DealID, authorId: author.AccountID, targetId: target.AccountID));
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(new GetReviewsQuery(target.AccountID), CancellationToken.None);

        result.Items[0].AuthorName.Should().Be("author@t.com");
    }

    [Fact]
    public async Task Handle_Pagination_Works()
    {
        var ctx = TestDbContext.Create();
        var target = Fakes.Account();
        ctx.Accounts.Add(target);

        for (var i = 0; i < 5; i++)
        {
            var author = Fakes.Account(login: $"a{i}@t.com");
            var deal = Fakes.Deal(initiatorId: author.AccountID, partnerId: target.AccountID);
            ctx.Accounts.Add(author);
            ctx.Deals.Add(deal);
            ctx.Reviews.Add(Fakes.Review(dealId: deal.DealID, authorId: author.AccountID, targetId: target.AccountID));
        }
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var page1 = await handler.Handle(new GetReviewsQuery(target.AccountID, Page: 1, PageSize: 3), CancellationToken.None);
        var page2 = await handler.Handle(new GetReviewsQuery(target.AccountID, Page: 2, PageSize: 3), CancellationToken.None);

        page1.Items.Should().HaveCount(3);
        page2.Items.Should().HaveCount(2);
        page1.TotalCount.Should().Be(5);
    }
}
