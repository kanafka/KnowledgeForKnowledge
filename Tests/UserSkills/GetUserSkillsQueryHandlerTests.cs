using Application.Features.UserSkills.Queries.GetUserSkills;
using Domain.Enums;
using FluentAssertions;
using Tests.Helpers;

namespace Tests.UserSkills;

public class GetUserSkillsQueryHandlerTests
{
    private static GetUserSkillsQueryHandler CreateHandler(Infrastructure.Data.ApplicationDbContext ctx) => new(ctx);

    [Fact]
    public async Task Handle_NoSkills_ReturnsEmpty()
    {
        var ctx = TestDbContext.Create();
        var handler = CreateHandler(ctx);
        var result = await handler.Handle(new GetUserSkillsQuery(Guid.NewGuid()), CancellationToken.None);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsOnlyAccountsSkills()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        var other = Fakes.Account(login: "o@t.com");
        var skill1 = Fakes.Skill(name: "Python");
        var skill2 = Fakes.Skill(name: "Java");
        ctx.Accounts.AddRange(account, other);
        ctx.SkillsCatalog.AddRange(skill1, skill2);
        ctx.UserSkills.Add(Fakes.UserSkill(account.AccountID, skill1.SkillID));
        ctx.UserSkills.Add(Fakes.UserSkill(other.AccountID, skill2.SkillID));
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(new GetUserSkillsQuery(account.AccountID), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].SkillName.Should().Be("Python");
    }

    [Fact]
    public async Task Handle_MultipleSkills_AllReturned()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        var skill1 = Fakes.Skill(name: "Python");
        var skill2 = Fakes.Skill(name: "Java");
        var skill3 = Fakes.Skill(name: "C#");
        ctx.Accounts.Add(account);
        ctx.SkillsCatalog.AddRange(skill1, skill2, skill3);
        ctx.UserSkills.Add(Fakes.UserSkill(account.AccountID, skill1.SkillID, SkillLevel.Junior));
        ctx.UserSkills.Add(Fakes.UserSkill(account.AccountID, skill2.SkillID, SkillLevel.Middle));
        ctx.UserSkills.Add(Fakes.UserSkill(account.AccountID, skill3.SkillID, SkillLevel.Senior));
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(new GetUserSkillsQuery(account.AccountID), CancellationToken.None);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_SkillLevel_MappedCorrectly()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        var skill = Fakes.Skill(name: "Python");
        ctx.Accounts.Add(account);
        ctx.SkillsCatalog.Add(skill);
        ctx.UserSkills.Add(Fakes.UserSkill(account.AccountID, skill.SkillID, SkillLevel.Senior));
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(new GetUserSkillsQuery(account.AccountID), CancellationToken.None);

        result[0].Level.Should().Be(SkillLevel.Senior);
    }

    [Fact]
    public async Task Handle_IsVerified_MappedCorrectly()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        var skill = Fakes.Skill(name: "Python");
        ctx.Accounts.Add(account);
        ctx.SkillsCatalog.Add(skill);
        var us = Fakes.UserSkill(account.AccountID, skill.SkillID);
        us.IsVerified = true;
        ctx.UserSkills.Add(us);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(new GetUserSkillsQuery(account.AccountID), CancellationToken.None);

        result[0].IsVerified.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_DescriptionAndLearnedAt_MappedCorrectly()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        var skill = Fakes.Skill(name: "Python");
        ctx.Accounts.Add(account);
        ctx.SkillsCatalog.Add(skill);
        var us = Fakes.UserSkill(account.AccountID, skill.SkillID);
        us.Description = "I use it at work";
        us.LearnedAt = "2020";
        ctx.UserSkills.Add(us);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(new GetUserSkillsQuery(account.AccountID), CancellationToken.None);

        result[0].Description.Should().Be("I use it at work");
        result[0].LearnedAt.Should().Be("2020");
    }

    [Fact]
    public async Task Handle_Epithet_MappedFromCatalog()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        var skill = new Domain.Entities.SkillsCatalog
        {
            SkillID = Guid.NewGuid(),
            SkillName = "Drawing",
            Epithet = Domain.Enums.SkillEpithet.Music
        };
        ctx.Accounts.Add(account);
        ctx.SkillsCatalog.Add(skill);
        ctx.UserSkills.Add(Fakes.UserSkill(account.AccountID, skill.SkillID));
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(new GetUserSkillsQuery(account.AccountID), CancellationToken.None);

        result[0].Epithet.Should().Be(Domain.Enums.SkillEpithet.Music);
    }
}
