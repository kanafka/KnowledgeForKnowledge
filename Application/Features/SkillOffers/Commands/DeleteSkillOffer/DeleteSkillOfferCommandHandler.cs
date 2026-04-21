using Application.Common.Exceptions;
using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.SkillOffers.Commands.DeleteSkillOffer;

public class DeleteSkillOfferCommandHandler : IRequestHandler<DeleteSkillOfferCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ITelegramService _telegram;

    public DeleteSkillOfferCommandHandler(IApplicationDbContext context, ITelegramService telegram)
    {
        _context = context;
        _telegram = telegram;
    }

    public async Task Handle(DeleteSkillOfferCommand request, CancellationToken cancellationToken)
    {
        var offer = await _context.SkillOffers
            .Include(o => o.Account)
            .Include(o => o.SkillsCatalog)
            .FirstOrDefaultAsync(o => o.OfferID == request.OfferID, cancellationToken);

        if (offer is null)
            throw new NotFoundException(nameof(Domain.Entities.SkillOffer), request.OfferID);

        if (offer.AccountID != request.AccountID && !request.IsAdmin)
            throw new UnauthorizedAccessException("Нет доступа к удалению этого предложения.");

        var shouldNotifyOwner =
            request.IsAdmin &&
            offer.AccountID != request.AccountID &&
            !string.IsNullOrWhiteSpace(offer.Account.TelegramID);
        var ownerTelegramId = offer.Account.TelegramID;
        var offerTitle = offer.Title;
        var skillName = offer.SkillsCatalog.SkillName;
        var deletionReason = string.IsNullOrWhiteSpace(request.DeletionReason)
            ? "Причина не указана."
            : request.DeletionReason.Trim();

        var removableApplications = await _context.Applications
            .Include(application => application.Deal)
            .Where(application => application.OfferID == offer.OfferID && application.Deal == null)
            .ToListAsync(cancellationToken);

        _context.Applications.RemoveRange(removableApplications);
        _context.SkillOffers.Remove(offer);
        await _context.SaveChangesAsync(cancellationToken);

        if (shouldNotifyOwner && !string.IsNullOrWhiteSpace(ownerTelegramId))
        {
            await _telegram.SendMessageAsync(
                ownerTelegramId,
                $"Ваше предложение «{offerTitle}» по навыку «{skillName}» удалено администратором.\nПричина: {deletionReason}",
                cancellationToken);
        }
    }
}
