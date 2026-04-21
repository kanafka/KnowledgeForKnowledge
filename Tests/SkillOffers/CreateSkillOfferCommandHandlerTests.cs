using Application.Common.Exceptions;
using Application.Features.SkillOffers.Commands.CreateSkillOffer;
using FluentAssertions;
using FluentValidation;
using Tests.Helpers;

namespace Tests.SkillOffers;

public class CreateSkillOfferCommandHandlerTests
{
    private static CreateSkillOfferCommandHandler CreateHandler(Infrastructure.Data.ApplicationDbContext ctx)
        => new(ctx);

    [Fact]
    public async Task Handle_SkillNotFound_ThrowsNotFoundException()
    {
        var ctx = TestDbContext.Create();
        var handler = CreateHandler(ctx);

        var act = () => handler.Handle(
            new CreateSkillOfferCommand(Guid.NewGuid(), Guid.NewGuid(), "Title", null),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_NoProfile_ThrowsValidationException()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        var skill = Fakes.Skill();
        ctx.Accounts.Add(account);
        ctx.SkillsCatalog.Add(skill);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var act = () => handler.Handle(
            new CreateSkillOfferCommand(account.AccountID, skill.SkillID, "Title", null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .Where(e => e.Message.Contains("профиль"));
    }

    [Fact]
    public async Task Handle_NoUserSkill_ThrowsValidationException()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        var skill = Fakes.Skill();
        var profile = Fakes.Profile(account.AccountID);
        ctx.Accounts.Add(account);
        ctx.SkillsCatalog.Add(skill);
        ctx.UserProfiles.Add(profile);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var act = () => handler.Handle(
            new CreateSkillOfferCommand(account.AccountID, skill.SkillID, "Title", null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .Where(e => e.Message.Contains("навыки") || e.Message.Contains("навыкам") || e.Message.Contains("личный кабинет"));
    }

    [Fact]
    public async Task Handle_Valid_CreatesOffer()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        var skill = Fakes.Skill();
        var profile = Fakes.Profile(account.AccountID);
        var userSkill = Fakes.UserSkill(account.AccountID, skill.SkillID);
        ctx.Accounts.Add(account);
        ctx.SkillsCatalog.Add(skill);
        ctx.UserProfiles.Add(profile);
        ctx.UserSkills.Add(userSkill);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var offerId = await handler.Handle(
            new CreateSkillOfferCommand(account.AccountID, skill.SkillID, "Python tutoring", "Details here"),
            CancellationToken.None);

        offerId.Should().NotBeEmpty();
        var offer = await ctx.SkillOffers.FindAsync(offerId);
        offer.Should().NotBeNull();
        offer!.Title.Should().Be("Python tutoring");
        offer.Details.Should().Be("Details here");
        offer.IsActive.Should().BeTrue();
        offer.AccountID.Should().Be(account.AccountID);
        offer.SkillID.Should().Be(skill.SkillID);
    }
}
