using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Applications.Commands.RespondApplication;

public class RespondApplicationCommandHandler : IRequestHandler<RespondApplicationCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ITelegramService _telegram;

    public RespondApplicationCommandHandler(IApplicationDbContext context, ITelegramService telegram)
    {
        _context = context;
        _telegram = telegram;
    }

    public async Task Handle(RespondApplicationCommand request, CancellationToken cancellationToken)
    {
        if (request.NewStatus == ApplicationStatus.Pending)
            throw new InvalidOperationException("Нельзя установить статус Pending вручную.");

        var application = await _context.Applications
            .Include(a => a.Applicant)
            .Include(a => a.SkillOffer).ThenInclude(o => o!.Account)
            .Include(a => a.SkillRequest).ThenInclude(r => r!.Account)
            .FirstOrDefaultAsync(a => a.ApplicationID == request.ApplicationID, cancellationToken);

        if (application is null)
            throw new NotFoundException(nameof(Domain.Entities.Application), request.ApplicationID);

        Guid ownerAccountId;
        string objectTitle;
        string? ownerTelegramId;

        if (application.SkillOffer is not null)
        {
            ownerAccountId = application.SkillOffer.AccountID;
            objectTitle = application.SkillOffer.Title;
            ownerTelegramId = application.SkillOffer.Account.TelegramID;
        }
        else if (application.SkillRequest is not null)
        {
            ownerAccountId = application.SkillRequest.AccountID;
            objectTitle = application.SkillRequest.Title;
            ownerTelegramId = application.SkillRequest.Account.TelegramID;
        }
        else
        {
            throw new InvalidOperationException("Заявка не привязана к предложению или запросу.");
        }

        if (ownerAccountId != request.OwnerAccountID)
            throw new UnauthorizedAccessException("Нет доступа к обработке этой заявки.");

        if (application.Status != ApplicationStatus.Pending)
            throw new InvalidOperationException("Заявка уже обработана.");

        application.Status = request.NewStatus;

        if (request.NewStatus == ApplicationStatus.Accepted)
        {
            // Создаём сделку
            var deal = new Deal
            {
                DealID = Guid.NewGuid(),
                ApplicationID = application.ApplicationID,
                InitiatorID = application.ApplicantID,
                PartnerID = ownerAccountId,
                Status = DealStatus.Active,
                CreatedAt = DateTime.UtcNow
            };
            _context.Deals.Add(deal);

            // Предложение закрываем после принятия, а запрос остаётся открытым,
            // чтобы владелец мог продолжать собирать отклики, пока не удалит его сам.
            if (application.SkillOffer is not null)
                application.SkillOffer.IsActive = false;

            // Уведомления в приложении
            _context.Notifications.Add(new Notification
            {
                NotificationID = Guid.NewGuid(),
                AccountID = application.ApplicantID,
                Type = NotificationType.ApplicationAccepted,
                Message = $"Ваш отклик на «{objectTitle}» принят. Сделка создана.",
                RelatedEntityId = deal.DealID,
                CreatedAt = DateTime.UtcNow
            });
            _context.Notifications.Add(new Notification
            {
                NotificationID = Guid.NewGuid(),
                AccountID = ownerAccountId,
                Type = NotificationType.DealCreated,
                Message = $"Создана новая сделка по «{objectTitle}».",
                RelatedEntityId = deal.DealID,
                CreatedAt = DateTime.UtcNow
            });
        }
        else if (request.NewStatus == ApplicationStatus.Rejected)
        {
            _context.Notifications.Add(new Notification
            {
                NotificationID = Guid.NewGuid(),
                AccountID = application.ApplicantID,
                Type = NotificationType.ApplicationRejected,
                Message = $"Ваш отклик на «{objectTitle}» отклонён.",
                RelatedEntityId = application.ApplicationID,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Telegram-уведомления
        var applicant = application.Applicant;
        if (request.NewStatus == ApplicationStatus.Accepted)
        {
            if (!string.IsNullOrEmpty(applicant.TelegramID) && applicant.NotificationsEnabled)
                await _telegram.SendMessageAsync(applicant.TelegramID,
                    $"✅ Ваш отклик на «{objectTitle}» принят! Сделка создана.", cancellationToken);

            if (!string.IsNullOrEmpty(ownerTelegramId))
            {
                var owner = application.SkillOffer?.Account ?? application.SkillRequest!.Account;
                if (owner.NotificationsEnabled)
                    await _telegram.SendMessageAsync(ownerTelegramId,
                        $"✅ Вы приняли отклик на «{objectTitle}». Сделка создана.\n" +
                        $"Откликнувшийся: @{applicant.TelegramID ?? "без Telegram"}", cancellationToken);
            }
        }
        else if (request.NewStatus == ApplicationStatus.Rejected)
        {
            if (!string.IsNullOrEmpty(applicant.TelegramID) && applicant.NotificationsEnabled)
                await _telegram.SendMessageAsync(applicant.TelegramID,
                    $"❌ Ваш отклик на «{objectTitle}» отклонён.", cancellationToken);
        }
    }
}
