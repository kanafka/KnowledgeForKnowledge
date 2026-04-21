using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Applications.Commands.CreateApplication;

public class CreateApplicationCommandHandler : IRequestHandler<CreateApplicationCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ITelegramService _telegram;

    public CreateApplicationCommandHandler(IApplicationDbContext context, ITelegramService telegram)
    {
        _context = context;
        _telegram = telegram;
    }

    public async Task<Guid> Handle(CreateApplicationCommand request, CancellationToken cancellationToken)
    {
        if (request.OfferID is null && request.SkillRequestID is null)
            throw new InvalidOperationException("Необходимо указать OfferID или SkillRequestID.");

        if (request.OfferID is not null && request.SkillRequestID is not null)
            throw new InvalidOperationException("Нельзя одновременно указать OfferID и SkillRequestID.");

        string notificationTitle;
        string notificationTelegramId = string.Empty;

        if (request.OfferID is not null)
        {
            var offer = await _context.SkillOffers
                .Include(o => o.Account)
                .FirstOrDefaultAsync(o => o.OfferID == request.OfferID, cancellationToken);

            if (offer is null)
                throw new NotFoundException(nameof(Domain.Entities.SkillOffer), request.OfferID);

            if (offer.AccountID == request.ApplicantID)
                throw new InvalidOperationException("Нельзя откликнуться на собственное предложение.");

            var existingOffer = await _context.Applications
                .FirstOrDefaultAsync(
                    a => a.ApplicantID == request.ApplicantID && a.OfferID == request.OfferID,
                    cancellationToken);

            if (existingOffer is not null)
                throw new InvalidOperationException(
                    $"Вы уже откликались на это предложение (статус: {existingOffer.Status}).");

            notificationTitle = offer.Title;
            notificationTelegramId = offer.Account.TelegramID ?? string.Empty;
        }
        else
        {
            var skillRequest = await _context.SkillRequests
                .Include(r => r.Account)
                .FirstOrDefaultAsync(r => r.RequestID == request.SkillRequestID, cancellationToken);

            if (skillRequest is null)
                throw new NotFoundException(nameof(Domain.Entities.SkillRequest), request.SkillRequestID);

            if (skillRequest.AccountID == request.ApplicantID)
                throw new InvalidOperationException("Нельзя откликнуться на собственный запрос.");

            if (skillRequest.Status != RequestStatus.Open)
                throw new InvalidOperationException("Запрос неактивен.");

            var hasRequiredSkill = await _context.UserSkills
                .AnyAsync(
                    userSkill => userSkill.AccountID == request.ApplicantID && userSkill.SkillID == skillRequest.SkillID,
                    cancellationToken);

            if (!hasRequiredSkill)
                throw new InvalidOperationException(
                    "Предложить помощь можно только по навыку, который уже добавлен в ваш профиль.");

            var existingRequest = await _context.Applications
                .FirstOrDefaultAsync(
                    a => a.ApplicantID == request.ApplicantID && a.SkillRequestID == request.SkillRequestID,
                    cancellationToken);

            if (existingRequest is not null)
                throw new InvalidOperationException(
                    $"Вы уже откликались на этот запрос (статус: {existingRequest.Status}).");

            notificationTitle = skillRequest.Title;
            notificationTelegramId = skillRequest.Account.TelegramID ?? string.Empty;
        }

        var application = new Domain.Entities.Application
        {
            ApplicationID = Guid.NewGuid(),
            ApplicantID = request.ApplicantID,
            OfferID = request.OfferID,
            SkillRequestID = request.SkillRequestID,
            Status = ApplicationStatus.Pending,
            Message = request.Message,
            CreatedAt = DateTime.UtcNow
        };

        _context.Applications.Add(application);
        await _context.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrEmpty(notificationTelegramId))
        {
            await _telegram.SendMessageAsync(
                notificationTelegramId,
                $"На ваше объявление «{notificationTitle}» поступил новый отклик.\nID отклика: {application.ApplicationID}",
                cancellationToken);
        }

        return application.ApplicationID;
    }
}
