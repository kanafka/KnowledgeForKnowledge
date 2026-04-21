using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.VerificationRequests.Commands.ReviewVerificationRequest;

public class ReviewVerificationRequestCommandHandler
    : IRequestHandler<ReviewVerificationRequestCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ITelegramService _telegram;

    public ReviewVerificationRequestCommandHandler(
        IApplicationDbContext context,
        ITelegramService telegram)
    {
        _context = context;
        _telegram = telegram;
    }

    public async Task Handle(
        ReviewVerificationRequestCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await _context.VerificationRequests
            .Include(r => r.Account)
            .FirstOrDefaultAsync(r => r.RequestID == request.RequestID, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.VerificationRequest), request.RequestID);

        if (entity.Status != VerificationStatus.Pending)
            throw new InvalidOperationException("Заявка уже рассмотрена.");

        entity.Status = request.NewStatus;

        // При одобрении верификации навыка — помечаем UserSkill.IsVerified = true
        if (request.NewStatus == VerificationStatus.Approved
            && entity.RequestType == VerificationRequestType.SkillVerify
            && entity.ProofID.HasValue)
        {
            var proof = await _context.Proofs
                .FirstOrDefaultAsync(p => p.ProofID == entity.ProofID, cancellationToken);

            if (proof?.SkillID != null)
            {
                var userSkill = await _context.UserSkills
                    .FirstOrDefaultAsync(
                        us => us.AccountID == entity.AccountID && us.SkillID == proof.SkillID,
                        cancellationToken);

                if (userSkill != null)
                    userSkill.IsVerified = true;

                if (proof != null)
                    proof.IsVerified = true;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Уведомление пользователю о результате
        if (!string.IsNullOrEmpty(entity.Account.TelegramID))
        {
            var typeText = entity.RequestType == VerificationRequestType.SkillVerify
                ? "верификации навыка"
                : "верификации аккаунта";

            var resultText = request.NewStatus == VerificationStatus.Approved
                ? $"✅ Ваша заявка на {typeText} одобрена!"
                : $"❌ Ваша заявка на {typeText} отклонена.";

            if (request.NewStatus == VerificationStatus.Rejected && !string.IsNullOrWhiteSpace(request.RejectionReason))
                resultText = $"{resultText}\nПричина: {request.RejectionReason.Trim()}";

            await _telegram.SendMessageAsync(entity.Account.TelegramID, resultText, cancellationToken);
        }
    }
}
