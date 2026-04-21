using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.VerificationRequests.Commands.ReviewVerificationRequest;
using Application.Features.VerificationRequests.Commands.SubmitVerificationRequest;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Moq;
using Tests.Helpers;

namespace Tests.Verification;

public class VerificationRequestCommandHandlerTests
{
    private static SubmitVerificationRequestCommandHandler CreateSubmitHandler(Infrastructure.Data.ApplicationDbContext ctx)
        => new(ctx);

    private static ReviewVerificationRequestCommandHandler CreateReviewHandler(
        Infrastructure.Data.ApplicationDbContext ctx,
        ITelegramService? telegram = null)
    {
        return new ReviewVerificationRequestCommandHandler(ctx, telegram ?? Mock.Of<ITelegramService>());
    }

    // --- SubmitVerificationRequest ---

    [Fact]
    public async Task Submit_SkillVerify_WithoutProofId_ThrowsValidation()
    {
        var ctx = TestDbContext.Create();
        var handler = CreateSubmitHandler(ctx);

        var act = () => handler.Handle(
            new SubmitVerificationRequestCommand(Guid.NewGuid(), VerificationRequestType.SkillVerify, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .Where(e => e.Message.Contains("пруф"));
    }

    [Fact]
    public async Task Submit_ProofNotFound_ThrowsValidation()
    {
        var ctx = TestDbContext.Create();
        var handler = CreateSubmitHandler(ctx);

        var act = () => handler.Handle(
            new SubmitVerificationRequestCommand(Guid.NewGuid(), VerificationRequestType.SkillVerify, Guid.NewGuid()),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .Where(e => e.Message.Contains("не найден"));
    }

    [Fact]
    public async Task Submit_ProofOwnedByOther_ThrowsUnauthorized()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        var otherAccount = Fakes.Account(login: "other@t.com");
        var skill = Fakes.Skill();
        var proof = new Proof
        {
            ProofID = Guid.NewGuid(),
            AccountID = otherAccount.AccountID,
            SkillID = skill.SkillID,
            FileURL = "https://proof.url",
            IsVerified = false
        };
        ctx.Accounts.AddRange(account, otherAccount);
        ctx.SkillsCatalog.Add(skill);
        ctx.Proofs.Add(proof);
        await ctx.SaveChangesAsync();

        var handler = CreateSubmitHandler(ctx);
        var act = () => handler.Handle(
            new SubmitVerificationRequestCommand(account.AccountID, VerificationRequestType.SkillVerify, proof.ProofID),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*чужой*");
    }

    [Fact]
    public async Task Submit_ProofAlreadyVerified_ThrowsInvalidOperation()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        var skill = Fakes.Skill();
        var proof = new Proof
        {
            ProofID = Guid.NewGuid(),
            AccountID = account.AccountID,
            SkillID = skill.SkillID,
            FileURL = "https://proof.url",
            IsVerified = true
        };
        ctx.Accounts.Add(account);
        ctx.SkillsCatalog.Add(skill);
        ctx.Proofs.Add(proof);
        await ctx.SaveChangesAsync();

        var handler = CreateSubmitHandler(ctx);
        var act = () => handler.Handle(
            new SubmitVerificationRequestCommand(account.AccountID, VerificationRequestType.SkillVerify, proof.ProofID),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*уже подтвержден*");
    }

    [Fact]
    public async Task Submit_DuplicatePendingRequest_ThrowsInvalidOperation()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        var skill = Fakes.Skill();
        var proof = new Proof
        {
            ProofID = Guid.NewGuid(),
            AccountID = account.AccountID,
            SkillID = skill.SkillID,
            FileURL = "https://proof.url",
            IsVerified = false
        };
        var existingRequest = new VerificationRequest
        {
            RequestID = Guid.NewGuid(),
            AccountID = account.AccountID,
            RequestType = VerificationRequestType.SkillVerify,
            ProofID = proof.ProofID,
            Status = VerificationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Accounts.Add(account);
        ctx.SkillsCatalog.Add(skill);
        ctx.Proofs.Add(proof);
        ctx.VerificationRequests.Add(existingRequest);
        await ctx.SaveChangesAsync();

        var handler = CreateSubmitHandler(ctx);
        var act = () => handler.Handle(
            new SubmitVerificationRequestCommand(account.AccountID, VerificationRequestType.SkillVerify, proof.ProofID),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*уже есть заявка*");
    }

    [Fact]
    public async Task Submit_ValidAccountVerification_CreatesRequest()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        ctx.Accounts.Add(account);
        await ctx.SaveChangesAsync();

        var handler = CreateSubmitHandler(ctx);
        var requestId = await handler.Handle(
            new SubmitVerificationRequestCommand(account.AccountID, VerificationRequestType.AccountVerify, null),
            CancellationToken.None);

        requestId.Should().NotBeEmpty();
        var vr = await ctx.VerificationRequests.FindAsync(requestId);
        vr.Should().NotBeNull();
        vr!.Status.Should().Be(VerificationStatus.Pending);
        vr.RequestType.Should().Be(VerificationRequestType.AccountVerify);
    }

    [Fact]
    public async Task Submit_ValidSkillVerification_CreatesRequest()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        var skill = Fakes.Skill();
        var proof = new Proof
        {
            ProofID = Guid.NewGuid(),
            AccountID = account.AccountID,
            SkillID = skill.SkillID,
            FileURL = "https://proof.url",
            IsVerified = false
        };
        ctx.Accounts.Add(account);
        ctx.SkillsCatalog.Add(skill);
        ctx.Proofs.Add(proof);
        await ctx.SaveChangesAsync();

        var handler = CreateSubmitHandler(ctx);
        var requestId = await handler.Handle(
            new SubmitVerificationRequestCommand(account.AccountID, VerificationRequestType.SkillVerify, proof.ProofID),
            CancellationToken.None);

        requestId.Should().NotBeEmpty();
    }

    // --- ReviewVerificationRequest ---

    [Fact]
    public async Task Review_RequestNotFound_ThrowsNotFoundException()
    {
        var ctx = TestDbContext.Create();
        var handler = CreateReviewHandler(ctx);

        var act = () => handler.Handle(
            new ReviewVerificationRequestCommand(Guid.NewGuid(), VerificationStatus.Approved),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Review_AlreadyReviewed_ThrowsInvalidOperation()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        var vr = new VerificationRequest
        {
            RequestID = Guid.NewGuid(),
            AccountID = account.AccountID,
            RequestType = VerificationRequestType.AccountVerify,
            Status = VerificationStatus.Approved,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Accounts.Add(account);
        ctx.VerificationRequests.Add(vr);
        await ctx.SaveChangesAsync();

        var handler = CreateReviewHandler(ctx);
        var act = () => handler.Handle(
            new ReviewVerificationRequestCommand(vr.RequestID, VerificationStatus.Rejected),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*рассмотрена*");
    }

    [Fact]
    public async Task Review_Approve_UpdatesStatus()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account(telegramId: "tg_user");
        var vr = new VerificationRequest
        {
            RequestID = Guid.NewGuid(),
            AccountID = account.AccountID,
            RequestType = VerificationRequestType.AccountVerify,
            Status = VerificationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Accounts.Add(account);
        ctx.VerificationRequests.Add(vr);
        await ctx.SaveChangesAsync();

        var telegram = new Mock<ITelegramService>();
        telegram.Setup(t => t.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = CreateReviewHandler(ctx, telegram.Object);
        await handler.Handle(
            new ReviewVerificationRequestCommand(vr.RequestID, VerificationStatus.Approved),
            CancellationToken.None);

        var updated = await ctx.VerificationRequests.FindAsync(vr.RequestID);
        updated!.Status.Should().Be(VerificationStatus.Approved);
        telegram.Verify(t => t.SendMessageAsync("tg_user", It.Is<string>(s => s.Contains("одобрена")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Review_ApproveSkillVerify_SetsUserSkillIsVerified()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        var skill = Fakes.Skill();
        var proof = new Proof
        {
            ProofID = Guid.NewGuid(),
            AccountID = account.AccountID,
            SkillID = skill.SkillID,
            FileURL = "https://proof.url",
            IsVerified = false
        };
        var userSkill = Fakes.UserSkill(account.AccountID, skill.SkillID);
        userSkill.IsVerified = false;
        var vr = new VerificationRequest
        {
            RequestID = Guid.NewGuid(),
            AccountID = account.AccountID,
            RequestType = VerificationRequestType.SkillVerify,
            ProofID = proof.ProofID,
            Status = VerificationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Accounts.Add(account);
        ctx.SkillsCatalog.Add(skill);
        ctx.Proofs.Add(proof);
        ctx.UserSkills.Add(userSkill);
        ctx.VerificationRequests.Add(vr);
        await ctx.SaveChangesAsync();

        var handler = CreateReviewHandler(ctx);
        await handler.Handle(
            new ReviewVerificationRequestCommand(vr.RequestID, VerificationStatus.Approved),
            CancellationToken.None);

        var updatedSkill = await ctx.UserSkills
            .FirstOrDefaultAsync(us => us.AccountID == account.AccountID && us.SkillID == skill.SkillID);
        updatedSkill!.IsVerified.Should().BeTrue();

        var updatedProof = await ctx.Proofs.FindAsync(proof.ProofID);
        updatedProof!.IsVerified.Should().BeTrue();
    }

    [Fact]
    public async Task Review_Reject_WithReason_SendsReasonViaTelegram()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account(telegramId: "tg_user");
        var vr = new VerificationRequest
        {
            RequestID = Guid.NewGuid(),
            AccountID = account.AccountID,
            RequestType = VerificationRequestType.AccountVerify,
            Status = VerificationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Accounts.Add(account);
        ctx.VerificationRequests.Add(vr);
        await ctx.SaveChangesAsync();

        var telegram = new Mock<ITelegramService>();
        string? sentMessage = null;
        telegram.Setup(t => t.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, msg, _) => sentMessage = msg)
            .Returns(Task.CompletedTask);

        var handler = CreateReviewHandler(ctx, telegram.Object);
        await handler.Handle(
            new ReviewVerificationRequestCommand(vr.RequestID, VerificationStatus.Rejected, "Insufficient documents"),
            CancellationToken.None);

        sentMessage.Should().Contain("отклонена");
        sentMessage.Should().Contain("Insufficient documents");
    }
}
