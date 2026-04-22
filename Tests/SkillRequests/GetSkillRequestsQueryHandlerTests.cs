using Application.Features.SkillRequests.Queries.GetSkillRequests;
using Domain.Enums;
using FluentAssertions;
using Tests.Helpers;

namespace Tests.SkillRequests;

public class GetSkillRequestsQueryHandlerTests
{
    private static GetSkillRequestsQueryHandler CreateHandler(Infrastructure.Data.ApplicationDbContext ctx)
        => new(ctx);

    [Fact]
    public async Task Handle_NoRequests_ReturnsEmpty()
    {
        var ctx = TestDbContext.Create();
        var handler = CreateHandler(ctx);
        var result = await handler.Handle(
            new GetSkillRequestsQuery(null, null, null, null, null, null, null),
            CancellationToken.None);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_FilterByStatus_ReturnsOnly()
    {
        var ctx = TestDbContext.Create();
        var owner = Fakes.Account();
        var skill = Fakes.Skill();
        ctx.Accounts.Add(owner);
        ctx.SkillsCatalog.Add(skill);
        ctx.SkillRequests.Add(Fakes.Request(accountId: owner.AccountID, skillId: skill.SkillID, status: RequestStatus.Open));
        ctx.SkillRequests.Add(Fakes.Request(accountId: owner.AccountID, skillId: skill.SkillID, status: RequestStatus.Closed));
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(
            new GetSkillRequestsQuery(null, null, RequestStatus.Open, null, null, null, null),
            CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items[0].Status.Should().Be(RequestStatus.Open);
    }

    [Fact]
    public async Task Handle_FilterBySkillId_FiltersCorrectly()
    {
        var ctx = TestDbContext.Create();
        var owner = Fakes.Account();
        var skill1 = Fakes.Skill(name: "Python");
        var skill2 = Fakes.Skill(name: "Java");
        ctx.Accounts.Add(owner);
        ctx.SkillsCatalog.AddRange(skill1, skill2);
        ctx.SkillRequests.Add(Fakes.Request(accountId: owner.AccountID, skillId: skill1.SkillID));
        ctx.SkillRequests.Add(Fakes.Request(accountId: owner.AccountID, skillId: skill2.SkillID));
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(
            new GetSkillRequestsQuery(skill1.SkillID, null, null, null, null, null, null),
            CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items[0].SkillName.Should().Be("Python");
    }

    [Fact]
    public async Task Handle_SearchInTitle_FiltersCorrectly()
    {
        var ctx = TestDbContext.Create();
        var owner = Fakes.Account();
        var skill = Fakes.Skill(name: "CSharp");  // neutral name that won't match "python"
        ctx.Accounts.Add(owner);
        ctx.SkillsCatalog.Add(skill);
        var r1 = Fakes.Request(accountId: owner.AccountID, skillId: skill.SkillID);
        r1.Title = "Need Python help urgently";
        var r2 = Fakes.Request(accountId: owner.AccountID, skillId: skill.SkillID);
        r2.Title = "Java mentor wanted";
        ctx.SkillRequests.AddRange(r1, r2);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(
            new GetSkillRequestsQuery(null, null, null, "python", null, null, null),
            CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items[0].Title.Should().Be("Need Python help urgently");
    }

    [Fact]
    public async Task Handle_HelperCanHelp_ReturnsRequestsHelperHasSkillFor()
    {
        var ctx = TestDbContext.Create();
        var requester = Fakes.Account();
        var helper = Fakes.Account(login: "h@t.com");
        var skill1 = Fakes.Skill(name: "Python");
        var skill2 = Fakes.Skill(name: "Java");
        var helperSkill = Fakes.UserSkill(helper.AccountID, skill1.SkillID);
        ctx.Accounts.AddRange(requester, helper);
        ctx.SkillsCatalog.AddRange(skill1, skill2);
        ctx.UserSkills.Add(helperSkill);
        ctx.SkillRequests.Add(Fakes.Request(accountId: requester.AccountID, skillId: skill1.SkillID));
        ctx.SkillRequests.Add(Fakes.Request(accountId: requester.AccountID, skillId: skill2.SkillID));
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(
            new GetSkillRequestsQuery(null, null, null, null, helper.AccountID, CanHelp: true, null),
            CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items[0].SkillName.Should().Be("Python");
    }

    [Fact]
    public async Task Handle_AuthorWithProfile_ReturnsProfileName()
    {
        var ctx = TestDbContext.Create();
        var owner = Fakes.Account();
        var skill = Fakes.Skill();
        var profile = Fakes.Profile(owner.AccountID, "Иван Иванов");
        ctx.Accounts.Add(owner);
        ctx.SkillsCatalog.Add(skill);
        ctx.UserProfiles.Add(profile);
        ctx.SkillRequests.Add(Fakes.Request(accountId: owner.AccountID, skillId: skill.SkillID));
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(
            new GetSkillRequestsQuery(null, null, null, null, null, null, null),
            CancellationToken.None);

        result.Items[0].AuthorName.Should().Be("Иван Иванов");
    }

    [Fact]
    public async Task Handle_Pagination_Works()
    {
        var ctx = TestDbContext.Create();
        var owner = Fakes.Account();
        var skill = Fakes.Skill();
        ctx.Accounts.Add(owner);
        ctx.SkillsCatalog.Add(skill);
        for (var i = 0; i < 6; i++)
            ctx.SkillRequests.Add(Fakes.Request(accountId: owner.AccountID, skillId: skill.SkillID));
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(
            new GetSkillRequestsQuery(null, null, null, null, null, null, null, Page: 2, PageSize: 4),
            CancellationToken.None);

        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(6);
    }
}
