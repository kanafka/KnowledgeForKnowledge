using Application.Features.SkillOffers.Queries.GetSkillOffers;
using FluentAssertions;
using Tests.Helpers;

namespace Tests.SkillOffers;

public class GetSkillOffersQueryHandlerTests
{
    private static GetSkillOffersQueryHandler CreateHandler(Infrastructure.Data.ApplicationDbContext ctx)
        => new(ctx);

    private async Task<(Infrastructure.Data.ApplicationDbContext ctx, Domain.Entities.Account owner,
        Domain.Entities.SkillsCatalog skill, Domain.Entities.SkillOffer offer)> SeedOne()
    {
        var ctx = TestDbContext.Create();
        var owner = Fakes.Account();
        var skill = Fakes.Skill(name: "Python");
        var offer = Fakes.Offer(accountId: owner.AccountID, skillId: skill.SkillID);
        ctx.Accounts.Add(owner);
        ctx.SkillsCatalog.Add(skill);
        ctx.SkillOffers.Add(offer);
        await ctx.SaveChangesAsync();
        return (ctx, owner, skill, offer);
    }

    [Fact]
    public async Task Handle_NoOffers_ReturnsEmpty()
    {
        var ctx = TestDbContext.Create();
        var handler = CreateHandler(ctx);
        var result = await handler.Handle(
            new GetSkillOffersQuery(null, null, null, null, null, null, null),
            CancellationToken.None);
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ReturnsAllOffers()
    {
        var (ctx, _, skill, _) = await SeedOne();
        var owner2 = Fakes.Account(login: "b@t.com");
        ctx.Accounts.Add(owner2);
        ctx.SkillOffers.Add(Fakes.Offer(accountId: owner2.AccountID, skillId: skill.SkillID));
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(
            new GetSkillOffersQuery(null, null, null, null, null, null, null),
            CancellationToken.None);

        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_FilterBySkillId_ReturnsMatchingOffers()
    {
        var ctx = TestDbContext.Create();
        var owner = Fakes.Account();
        var skill1 = Fakes.Skill(name: "Python");
        var skill2 = Fakes.Skill(name: "Java");
        ctx.Accounts.Add(owner);
        ctx.SkillsCatalog.AddRange(skill1, skill2);
        ctx.SkillOffers.Add(Fakes.Offer(accountId: owner.AccountID, skillId: skill1.SkillID));
        ctx.SkillOffers.Add(Fakes.Offer(accountId: owner.AccountID, skillId: skill2.SkillID));
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(
            new GetSkillOffersQuery(skill1.SkillID, null, null, null, null, null, null),
            CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items[0].SkillName.Should().Be("Python");
    }

    [Fact]
    public async Task Handle_FilterByAccountId_ReturnsOnlyThatAccount()
    {
        var ctx = TestDbContext.Create();
        var owner1 = Fakes.Account();
        var owner2 = Fakes.Account(login: "b@t.com");
        var skill = Fakes.Skill();
        ctx.Accounts.AddRange(owner1, owner2);
        ctx.SkillsCatalog.Add(skill);
        ctx.SkillOffers.Add(Fakes.Offer(accountId: owner1.AccountID, skillId: skill.SkillID));
        ctx.SkillOffers.Add(Fakes.Offer(accountId: owner2.AccountID, skillId: skill.SkillID));
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(
            new GetSkillOffersQuery(null, owner1.AccountID, null, null, null, null, null),
            CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items[0].AccountID.Should().Be(owner1.AccountID);
    }

    [Fact]
    public async Task Handle_FilterByIsActive_ReturnsOnlyActiveOffers()
    {
        var ctx = TestDbContext.Create();
        var owner = Fakes.Account();
        var skill = Fakes.Skill();
        ctx.Accounts.Add(owner);
        ctx.SkillsCatalog.Add(skill);
        ctx.SkillOffers.Add(Fakes.Offer(accountId: owner.AccountID, skillId: skill.SkillID, isActive: true));
        ctx.SkillOffers.Add(Fakes.Offer(accountId: owner.AccountID, skillId: skill.SkillID, isActive: false));
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(
            new GetSkillOffersQuery(null, null, IsActive: true, null, null, null, null),
            CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items[0].IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_SearchByTitle_FiltersCorrectly()
    {
        var ctx = TestDbContext.Create();
        var owner = Fakes.Account();
        var skill = Fakes.Skill(name: "CSharp");  // neutral name that won't match "python"
        ctx.Accounts.Add(owner);
        ctx.SkillsCatalog.Add(skill);
        var offer1 = Fakes.Offer(accountId: owner.AccountID, skillId: skill.SkillID);
        offer1.Title = "Python tutoring sessions";
        var offer2 = Fakes.Offer(accountId: owner.AccountID, skillId: skill.SkillID);
        offer2.Title = "Java course";
        ctx.SkillOffers.AddRange(offer1, offer2);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(
            new GetSkillOffersQuery(null, null, null, "python", null, null, null),
            CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items[0].Title.Should().Be("Python tutoring sessions");
    }

    [Fact]
    public async Task Handle_ViewerHasSkill_ReturnsOnlyOffersViewerCanHelp()
    {
        var ctx = TestDbContext.Create();
        var owner = Fakes.Account();
        var viewer = Fakes.Account(login: "v@t.com");
        var skill1 = Fakes.Skill(name: "Python");
        var skill2 = Fakes.Skill(name: "Java");
        var userSkill = Fakes.UserSkill(viewer.AccountID, skill1.SkillID);
        ctx.Accounts.AddRange(owner, viewer);
        ctx.SkillsCatalog.AddRange(skill1, skill2);
        ctx.UserSkills.Add(userSkill);
        ctx.SkillOffers.Add(Fakes.Offer(accountId: owner.AccountID, skillId: skill1.SkillID));
        ctx.SkillOffers.Add(Fakes.Offer(accountId: owner.AccountID, skillId: skill2.SkillID));
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(
            new GetSkillOffersQuery(null, null, null, null, viewer.AccountID, ViewerHasSkill: true, null),
            CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items[0].SkillName.Should().Be("Python");
    }

    [Fact]
    public async Task Handle_AuthorWithProfile_ReturnsProfileName()
    {
        var (ctx, owner, _, offer) = await SeedOne();
        var profile = Fakes.Profile(owner.AccountID, "Vasya Pupkin");
        ctx.UserProfiles.Add(profile);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(
            new GetSkillOffersQuery(null, null, null, null, null, null, null),
            CancellationToken.None);

        result.Items[0].AuthorName.Should().Be("Vasya Pupkin");
    }

    [Fact]
    public async Task Handle_AuthorWithoutProfile_ReturnsLogin()
    {
        var (ctx, owner, _, _) = await SeedOne();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(
            new GetSkillOffersQuery(null, null, null, null, null, null, null),
            CancellationToken.None);

        result.Items[0].AuthorName.Should().Be(owner.Login);
    }

    [Fact]
    public async Task Handle_Pagination_WorksCorrectly()
    {
        var ctx = TestDbContext.Create();
        var owner = Fakes.Account();
        var skill = Fakes.Skill();
        ctx.Accounts.Add(owner);
        ctx.SkillsCatalog.Add(skill);
        for (var i = 0; i < 7; i++)
            ctx.SkillOffers.Add(Fakes.Offer(accountId: owner.AccountID, skillId: skill.SkillID));
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var page1 = await handler.Handle(
            new GetSkillOffersQuery(null, null, null, null, null, null, null, Page: 1, PageSize: 5),
            CancellationToken.None);
        var page2 = await handler.Handle(
            new GetSkillOffersQuery(null, null, null, null, null, null, null, Page: 2, PageSize: 5),
            CancellationToken.None);

        page1.Items.Should().HaveCount(5);
        page2.Items.Should().HaveCount(2);
        page1.TotalCount.Should().Be(7);
    }
}
