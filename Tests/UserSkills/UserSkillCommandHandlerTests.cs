using Application.Common.Exceptions;
using Application.Features.UserSkills.Commands.AddUserSkill;
using Application.Features.UserSkills.Commands.RemoveUserSkill;
using Domain.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Tests.Helpers;

namespace Tests.UserSkills;

public class UserSkillCommandHandlerTests
{
    private static AddUserSkillCommandHandler CreateAddHandler(Infrastructure.Data.ApplicationDbContext ctx)
        => new(ctx);

    private static RemoveUserSkillCommandHandler CreateRemoveHandler(Infrastructure.Data.ApplicationDbContext ctx)
        => new(ctx);

    // --- AddUserSkill ---

    [Fact]
    public async Task AddUserSkill_SkillNotFound_ThrowsNotFoundException()
    {
        var ctx = TestDbContext.Create();
        var handler = CreateAddHandler(ctx);

        var act = () => handler.Handle(
            new AddUserSkillCommand(Guid.NewGuid(), Guid.NewGuid(), SkillLevel.Junior, null, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task AddUserSkill_NewSkill_CreatesUserSkill()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        var skill = Fakes.Skill();
        ctx.Accounts.Add(account);
        ctx.SkillsCatalog.Add(skill);
        await ctx.SaveChangesAsync();

        var handler = CreateAddHandler(ctx);
        await handler.Handle(
            new AddUserSkillCommand(account.AccountID, skill.SkillID, SkillLevel.Middle, "Good at Python", "2020"),
            CancellationToken.None);

        var userSkill = await ctx.UserSkills
            .FirstOrDefaultAsync(us => us.AccountID == account.AccountID && us.SkillID == skill.SkillID);
        userSkill.Should().NotBeNull();
        userSkill!.SkillLevel.Should().Be(SkillLevel.Middle);
        userSkill.Description.Should().Be("Good at Python");
        userSkill.LearnedAt.Should().Be("2020");
        userSkill.IsVerified.Should().BeFalse();
    }

    [Fact]
    public async Task AddUserSkill_ExistingSkill_UpdatesIt()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        var skill = Fakes.Skill();
        var existing = Fakes.UserSkill(account.AccountID, skill.SkillID, SkillLevel.Junior);
        ctx.Accounts.Add(account);
        ctx.SkillsCatalog.Add(skill);
        ctx.UserSkills.Add(existing);
        await ctx.SaveChangesAsync();

        var handler = CreateAddHandler(ctx);
        await handler.Handle(
            new AddUserSkillCommand(account.AccountID, skill.SkillID, SkillLevel.Senior, "Now expert", "2023"),
            CancellationToken.None);

        var userSkills = ctx.UserSkills
            .Where(us => us.AccountID == account.AccountID && us.SkillID == skill.SkillID)
            .ToList();
        userSkills.Should().HaveCount(1);
        userSkills[0].SkillLevel.Should().Be(SkillLevel.Senior);
        userSkills[0].Description.Should().Be("Now expert");
    }

    [Fact]
    public async Task AddUserSkill_WhitespaceDescription_NormalizesToNull()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        var skill = Fakes.Skill();
        ctx.Accounts.Add(account);
        ctx.SkillsCatalog.Add(skill);
        await ctx.SaveChangesAsync();

        var handler = CreateAddHandler(ctx);
        await handler.Handle(
            new AddUserSkillCommand(account.AccountID, skill.SkillID, SkillLevel.Junior, "   ", "  "),
            CancellationToken.None);

        var userSkill = await ctx.UserSkills
            .FirstOrDefaultAsync(us => us.AccountID == account.AccountID && us.SkillID == skill.SkillID);
        userSkill!.Description.Should().BeNull();
        userSkill.LearnedAt.Should().BeNull();
    }

    // --- RemoveUserSkill ---

    [Fact]
    public async Task RemoveUserSkill_NotFound_ThrowsNotFoundException()
    {
        var ctx = TestDbContext.Create();
        var handler = CreateRemoveHandler(ctx);

        var act = () => handler.Handle(
            new RemoveUserSkillCommand(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task RemoveUserSkill_Exists_Removes()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        var skill = Fakes.Skill();
        var userSkill = Fakes.UserSkill(account.AccountID, skill.SkillID);
        ctx.Accounts.Add(account);
        ctx.SkillsCatalog.Add(skill);
        ctx.UserSkills.Add(userSkill);
        await ctx.SaveChangesAsync();

        var handler = CreateRemoveHandler(ctx);
        await handler.Handle(
            new RemoveUserSkillCommand(account.AccountID, skill.SkillID),
            CancellationToken.None);

        var deleted = await ctx.UserSkills
            .FirstOrDefaultAsync(us => us.AccountID == account.AccountID && us.SkillID == skill.SkillID);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task RemoveUserSkill_WrongAccount_ThrowsNotFoundException()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        var skill = Fakes.Skill();
        var userSkill = Fakes.UserSkill(account.AccountID, skill.SkillID);
        ctx.Accounts.Add(account);
        ctx.SkillsCatalog.Add(skill);
        ctx.UserSkills.Add(userSkill);
        await ctx.SaveChangesAsync();

        var handler = CreateRemoveHandler(ctx);
        var act = () => handler.Handle(
            new RemoveUserSkillCommand(Guid.NewGuid(), skill.SkillID),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
