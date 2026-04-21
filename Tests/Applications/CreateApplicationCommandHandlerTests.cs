using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Applications.Commands.CreateApplication;
using Domain.Enums;
using FluentAssertions;
using Moq;
using Tests.Helpers;

namespace Tests.Applications;

public class CreateApplicationCommandHandlerTests
{
    private static CreateApplicationCommandHandler CreateHandler(
        Infrastructure.Data.ApplicationDbContext ctx,
        ITelegramService? telegram = null)
    {
        return new CreateApplicationCommandHandler(ctx, telegram ?? Mock.Of<ITelegramService>());
    }

    [Fact]
    public async Task Handle_BothNullIds_ThrowsInvalidOperation()
    {
        var ctx = TestDbContext.Create();
        var handler = CreateHandler(ctx);

        var act = () => handler.Handle(
            new CreateApplicationCommand(Guid.NewGuid(), null, null, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*OfferID или SkillRequestID*");
    }

    [Fact]
    public async Task Handle_BothIdsProvided_ThrowsInvalidOperation()
    {
        var ctx = TestDbContext.Create();
        var handler = CreateHandler(ctx);

        var act = () => handler.Handle(
            new CreateApplicationCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*одновременно*");
    }

    // --- Offer-based ---

    [Fact]
    public async Task Handle_OfferNotFound_ThrowsNotFoundException()
    {
        var ctx = TestDbContext.Create();
        var handler = CreateHandler(ctx);

        var act = () => handler.Handle(
            new CreateApplicationCommand(Guid.NewGuid(), Guid.NewGuid(), null, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ApplyToOwnOffer_ThrowsInvalidOperation()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        var skill = Fakes.Skill();
        var offer = Fakes.Offer(accountId: account.AccountID, skillId: skill.SkillID);
        ctx.Accounts.Add(account);
        ctx.SkillsCatalog.Add(skill);
        ctx.SkillOffers.Add(offer);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var act = () => handler.Handle(
            new CreateApplicationCommand(account.AccountID, offer.OfferID, null, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*собственное предложение*");
    }

    [Fact]
    public async Task Handle_DuplicateOfferApplication_ThrowsInvalidOperation()
    {
        var ctx = TestDbContext.Create();
        var owner = Fakes.Account();
        var applicant = Fakes.Account(login: "applicant@t.com");
        var skill = Fakes.Skill();
        var offer = Fakes.Offer(accountId: owner.AccountID, skillId: skill.SkillID);
        var existing = Fakes.Application(applicantId: applicant.AccountID, offerId: offer.OfferID);
        ctx.Accounts.AddRange(owner, applicant);
        ctx.SkillsCatalog.Add(skill);
        ctx.SkillOffers.Add(offer);
        ctx.Applications.Add(existing);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var act = () => handler.Handle(
            new CreateApplicationCommand(applicant.AccountID, offer.OfferID, null, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*уже откликались*");
    }

    [Fact]
    public async Task Handle_ValidOfferApplication_CreatesApplicationAndNotifies()
    {
        var ctx = TestDbContext.Create();
        var owner = Fakes.Account(telegramId: "owner_tg");
        var applicant = Fakes.Account(login: "applicant@t.com");
        var skill = Fakes.Skill();
        var offer = Fakes.Offer(accountId: owner.AccountID, skillId: skill.SkillID);
        ctx.Accounts.AddRange(owner, applicant);
        ctx.SkillsCatalog.Add(skill);
        ctx.SkillOffers.Add(offer);
        await ctx.SaveChangesAsync();

        var telegram = new Mock<ITelegramService>();
        telegram.Setup(t => t.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler(ctx, telegram.Object);
        var appId = await handler.Handle(
            new CreateApplicationCommand(applicant.AccountID, offer.OfferID, null, "Hello"),
            CancellationToken.None);

        appId.Should().NotBeEmpty();
        var app = await ctx.Applications.FindAsync(appId);
        app.Should().NotBeNull();
        app!.Status.Should().Be(ApplicationStatus.Pending);
        app.OfferID.Should().Be(offer.OfferID);
        app.Message.Should().Be("Hello");

        telegram.Verify(t => t.SendMessageAsync("owner_tg", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // --- Request-based ---

    [Fact]
    public async Task Handle_RequestNotFound_ThrowsNotFoundException()
    {
        var ctx = TestDbContext.Create();
        var handler = CreateHandler(ctx);

        var act = () => handler.Handle(
            new CreateApplicationCommand(Guid.NewGuid(), null, Guid.NewGuid(), null),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ApplyToOwnRequest_ThrowsInvalidOperation()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        var skill = Fakes.Skill();
        var request = Fakes.Request(accountId: account.AccountID, skillId: skill.SkillID);
        ctx.Accounts.Add(account);
        ctx.SkillsCatalog.Add(skill);
        ctx.SkillRequests.Add(request);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var act = () => handler.Handle(
            new CreateApplicationCommand(account.AccountID, null, request.RequestID, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*собственный запрос*");
    }

    [Fact]
    public async Task Handle_RequestNotOpen_ThrowsInvalidOperation()
    {
        var ctx = TestDbContext.Create();
        var owner = Fakes.Account();
        var applicant = Fakes.Account(login: "a@t.com");
        var skill = Fakes.Skill();
        var request = Fakes.Request(accountId: owner.AccountID, skillId: skill.SkillID, status: RequestStatus.Closed);
        ctx.Accounts.AddRange(owner, applicant);
        ctx.SkillsCatalog.Add(skill);
        ctx.SkillRequests.Add(request);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var act = () => handler.Handle(
            new CreateApplicationCommand(applicant.AccountID, null, request.RequestID, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*неактивен*");
    }

    [Fact]
    public async Task Handle_ApplicantLacksRequiredSkill_ThrowsInvalidOperation()
    {
        var ctx = TestDbContext.Create();
        var owner = Fakes.Account();
        var applicant = Fakes.Account(login: "a@t.com");
        var skill = Fakes.Skill();
        var request = Fakes.Request(accountId: owner.AccountID, skillId: skill.SkillID);
        ctx.Accounts.AddRange(owner, applicant);
        ctx.SkillsCatalog.Add(skill);
        ctx.SkillRequests.Add(request);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var act = () => handler.Handle(
            new CreateApplicationCommand(applicant.AccountID, null, request.RequestID, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*навыку*");
    }

    [Fact]
    public async Task Handle_ValidRequestApplication_CreatesApplication()
    {
        var ctx = TestDbContext.Create();
        var owner = Fakes.Account(telegramId: "owner_tg");
        var applicant = Fakes.Account(login: "a@t.com");
        var skill = Fakes.Skill();
        var request = Fakes.Request(accountId: owner.AccountID, skillId: skill.SkillID);
        var userSkill = Fakes.UserSkill(applicant.AccountID, skill.SkillID);
        ctx.Accounts.AddRange(owner, applicant);
        ctx.SkillsCatalog.Add(skill);
        ctx.SkillRequests.Add(request);
        ctx.UserSkills.Add(userSkill);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var appId = await handler.Handle(
            new CreateApplicationCommand(applicant.AccountID, null, request.RequestID, null),
            CancellationToken.None);

        appId.Should().NotBeEmpty();
        var app = await ctx.Applications.FindAsync(appId);
        app!.SkillRequestID.Should().Be(request.RequestID);
        app.Status.Should().Be(ApplicationStatus.Pending);
    }

    [Fact]
    public async Task Handle_DuplicateRequestApplication_ThrowsInvalidOperation()
    {
        var ctx = TestDbContext.Create();
        var owner = Fakes.Account();
        var applicant = Fakes.Account(login: "a@t.com");
        var skill = Fakes.Skill();
        var request = Fakes.Request(accountId: owner.AccountID, skillId: skill.SkillID);
        var userSkill = Fakes.UserSkill(applicant.AccountID, skill.SkillID);
        var existing = Fakes.Application(applicantId: applicant.AccountID, requestId: request.RequestID);
        ctx.Accounts.AddRange(owner, applicant);
        ctx.SkillsCatalog.Add(skill);
        ctx.SkillRequests.Add(request);
        ctx.UserSkills.Add(userSkill);
        ctx.Applications.Add(existing);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var act = () => handler.Handle(
            new CreateApplicationCommand(applicant.AccountID, null, request.RequestID, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*уже откликались*");
    }
}
