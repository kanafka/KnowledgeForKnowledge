using Application.Features.Matches.Queries.GetMatches;
using Domain.Enums;
using FluentAssertions;
using Tests.Helpers;

namespace Tests.Matches;

public class GetMatchesQueryHandlerTests
{
    private static GetMatchesQueryHandler CreateHandler(Infrastructure.Data.ApplicationDbContext ctx) => new(ctx);

    [Fact]
    public async Task Handle_NoSkillsAndNoRequests_ReturnsEmpty()
    {
        var ctx = TestDbContext.Create();
        var handler = CreateHandler(ctx);
        var result = await handler.Handle(new GetMatchesQuery(Guid.NewGuid()), CancellationToken.None);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MySkill_OtherHasRequest_ReturnsMatch()
    {
        var ctx = TestDbContext.Create();
        var me = Fakes.Account();
        var other = Fakes.Account(login: "o@t.com");
        var skill = Fakes.Skill(name: "Python");

        ctx.Accounts.AddRange(me, other);
        ctx.SkillsCatalog.Add(skill);
        // I have Python
        ctx.UserSkills.Add(Fakes.UserSkill(me.AccountID, skill.SkillID));
        // Other wants Python
        ctx.SkillRequests.Add(Fakes.Request(accountId: other.AccountID, skillId: skill.SkillID));
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(new GetMatchesQuery(me.AccountID), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].AccountID.Should().Be(other.AccountID);
        result[0].SkillsIHaveThatTheyWant.Should().Contain("Python");
    }

    [Fact]
    public async Task Handle_MyRequest_OtherHasSkill_ReturnsMatch()
    {
        var ctx = TestDbContext.Create();
        var me = Fakes.Account();
        var other = Fakes.Account(login: "o@t.com");
        var skill = Fakes.Skill(name: "Java");

        ctx.Accounts.AddRange(me, other);
        ctx.SkillsCatalog.Add(skill);
        // I want Java
        ctx.SkillRequests.Add(Fakes.Request(accountId: me.AccountID, skillId: skill.SkillID));
        // Other has Java
        ctx.UserSkills.Add(Fakes.UserSkill(other.AccountID, skill.SkillID));
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(new GetMatchesQuery(me.AccountID), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].AccountID.Should().Be(other.AccountID);
        result[0].SkillsTheyHaveThatIWant.Should().Contain("Java");
    }

    [Fact]
    public async Task Handle_MutualMatch_BothSidesPopulated()
    {
        var ctx = TestDbContext.Create();
        var me = Fakes.Account();
        var other = Fakes.Account(login: "o@t.com");
        var python = Fakes.Skill(name: "Python");
        var java = Fakes.Skill(name: "Java");

        ctx.Accounts.AddRange(me, other);
        ctx.SkillsCatalog.AddRange(python, java);
        // I have Python, want Java
        ctx.UserSkills.Add(Fakes.UserSkill(me.AccountID, python.SkillID));
        ctx.SkillRequests.Add(Fakes.Request(accountId: me.AccountID, skillId: java.SkillID));
        // Other has Java, wants Python
        ctx.UserSkills.Add(Fakes.UserSkill(other.AccountID, java.SkillID));
        ctx.SkillRequests.Add(Fakes.Request(accountId: other.AccountID, skillId: python.SkillID));
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(new GetMatchesQuery(me.AccountID), CancellationToken.None);

        result.Should().HaveCount(1);
        var match = result[0];
        match.SkillsIHaveThatTheyWant.Should().Contain("Python");
        match.SkillsTheyHaveThatIWant.Should().Contain("Java");
    }

    [Fact]
    public async Task Handle_ClosedRequest_NotIncluded()
    {
        var ctx = TestDbContext.Create();
        var me = Fakes.Account();
        var other = Fakes.Account(login: "o@t.com");
        var skill = Fakes.Skill(name: "Python");

        ctx.Accounts.AddRange(me, other);
        ctx.SkillsCatalog.Add(skill);
        ctx.UserSkills.Add(Fakes.UserSkill(me.AccountID, skill.SkillID));
        // Other's request is Closed — should not match
        ctx.SkillRequests.Add(Fakes.Request(accountId: other.AccountID, skillId: skill.SkillID, status: RequestStatus.Closed));
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(new GetMatchesQuery(me.AccountID), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_OwnRequestNotMatchedWithSelf()
    {
        var ctx = TestDbContext.Create();
        var me = Fakes.Account();
        var skill = Fakes.Skill(name: "Python");

        ctx.Accounts.Add(me);
        ctx.SkillsCatalog.Add(skill);
        ctx.UserSkills.Add(Fakes.UserSkill(me.AccountID, skill.SkillID));
        ctx.SkillRequests.Add(Fakes.Request(accountId: me.AccountID, skillId: skill.SkillID));
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(new GetMatchesQuery(me.AccountID), CancellationToken.None);

        // Should not match with yourself
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MatchWithProfile_ReturnsFullName()
    {
        var ctx = TestDbContext.Create();
        var me = Fakes.Account();
        var other = Fakes.Account(login: "o@t.com");
        var skill = Fakes.Skill(name: "Python");

        ctx.Accounts.AddRange(me, other);
        ctx.SkillsCatalog.Add(skill);
        ctx.UserProfiles.Add(Fakes.Profile(other.AccountID, "Oleg Ivanov"));
        ctx.UserSkills.Add(Fakes.UserSkill(me.AccountID, skill.SkillID));
        ctx.SkillRequests.Add(Fakes.Request(accountId: other.AccountID, skillId: skill.SkillID));
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(new GetMatchesQuery(me.AccountID), CancellationToken.None);

        result[0].FullName.Should().Be("Oleg Ivanov");
    }

    [Fact]
    public async Task Handle_MatchWithoutProfile_ReturnsLogin()
    {
        var ctx = TestDbContext.Create();
        var me = Fakes.Account();
        var other = Fakes.Account(login: "other@t.com");
        var skill = Fakes.Skill(name: "Python");

        ctx.Accounts.AddRange(me, other);
        ctx.SkillsCatalog.Add(skill);
        ctx.UserSkills.Add(Fakes.UserSkill(me.AccountID, skill.SkillID));
        ctx.SkillRequests.Add(Fakes.Request(accountId: other.AccountID, skillId: skill.SkillID));
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(new GetMatchesQuery(me.AccountID), CancellationToken.None);

        result[0].FullName.Should().Be("other@t.com");
    }

    [Fact]
    public async Task Handle_TheirRequests_Populated()
    {
        var ctx = TestDbContext.Create();
        var me = Fakes.Account();
        var other = Fakes.Account(login: "o@t.com");
        var skill = Fakes.Skill(name: "Python");

        ctx.Accounts.AddRange(me, other);
        ctx.SkillsCatalog.Add(skill);
        ctx.UserSkills.Add(Fakes.UserSkill(me.AccountID, skill.SkillID));

        var request = Fakes.Request(accountId: other.AccountID, skillId: skill.SkillID);
        request.Title = "Need Python mentor";
        ctx.SkillRequests.Add(request);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(new GetMatchesQuery(me.AccountID), CancellationToken.None);

        result[0].TheirRequests.Should().HaveCount(1);
        result[0].TheirRequests[0].Title.Should().Be("Need Python mentor");
        result[0].TheirRequests[0].SkillName.Should().Be("Python");
    }

    [Fact]
    public async Task Handle_MultipleMatches_AllReturned()
    {
        var ctx = TestDbContext.Create();
        var me = Fakes.Account();
        var other1 = Fakes.Account(login: "a@t.com");
        var other2 = Fakes.Account(login: "b@t.com");
        var skill = Fakes.Skill(name: "Python");

        ctx.Accounts.AddRange(me, other1, other2);
        ctx.SkillsCatalog.Add(skill);
        ctx.UserSkills.Add(Fakes.UserSkill(me.AccountID, skill.SkillID));
        ctx.SkillRequests.Add(Fakes.Request(accountId: other1.AccountID, skillId: skill.SkillID));
        ctx.SkillRequests.Add(Fakes.Request(accountId: other2.AccountID, skillId: skill.SkillID));
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(new GetMatchesQuery(me.AccountID), CancellationToken.None);

        result.Should().HaveCount(2);
    }
}
