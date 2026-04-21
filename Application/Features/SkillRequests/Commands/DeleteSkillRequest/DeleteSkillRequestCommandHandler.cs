using Application.Common.Exceptions;
using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.SkillRequests.Commands.DeleteSkillRequest;

public class DeleteSkillRequestCommandHandler : IRequestHandler<DeleteSkillRequestCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ITelegramService _telegram;

    public DeleteSkillRequestCommandHandler(IApplicationDbContext context, ITelegramService telegram)
    {
        _context = context;
        _telegram = telegram;
    }

    public async Task Handle(DeleteSkillRequestCommand request, CancellationToken cancellationToken)
    {
        var skillRequest = await _context.SkillRequests
            .Include(r => r.Account)
            .Include(r => r.SkillsCatalog)
            .FirstOrDefaultAsync(r => r.RequestID == request.RequestID, cancellationToken);

        if (skillRequest is null)
            throw new NotFoundException(nameof(Domain.Entities.SkillRequest), request.RequestID);

        if (skillRequest.AccountID != request.AccountID && !request.IsAdmin)
            throw new UnauthorizedAccessException("Нельзя удалить чужой запрос.");

        var shouldNotifyOwner =
            request.IsAdmin &&
            skillRequest.AccountID != request.AccountID &&
            !string.IsNullOrWhiteSpace(skillRequest.Account.TelegramID);
        var ownerTelegramId = skillRequest.Account.TelegramID;
        var requestTitle = skillRequest.Title;
        var skillName = skillRequest.SkillsCatalog.SkillName;
        var deletionReason = string.IsNullOrWhiteSpace(request.DeletionReason)
            ? "Причина не указана."
            : request.DeletionReason.Trim();

        var removableApplications = await _context.Applications
            .Include(application => application.Deal)
            .Where(application => application.SkillRequestID == skillRequest.RequestID && application.Deal == null)
            .ToListAsync(cancellationToken);

        _context.Applications.RemoveRange(removableApplications);
        _context.SkillRequests.Remove(skillRequest);
        await _context.SaveChangesAsync(cancellationToken);

        if (shouldNotifyOwner && !string.IsNullOrWhiteSpace(ownerTelegramId))
        {
            await _telegram.SendMessageAsync(
                ownerTelegramId,
                $"Ваш запрос «{requestTitle}» по навыку «{skillName}» удалён администратором.\nПричина: {deletionReason}",
                cancellationToken);
        }
    }
}
