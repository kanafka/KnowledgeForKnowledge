using Application.Common.Exceptions;
using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Notifications.Commands.MarkNotificationsRead;

public class MarkNotificationsReadCommandHandler : IRequestHandler<MarkNotificationsReadCommand>
{
    private readonly IApplicationDbContext _context;

    public MarkNotificationsReadCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task Handle(MarkNotificationsReadCommand request, CancellationToken cancellationToken)
    {
        if (request.NotificationID.HasValue)
        {
            // Throw 404 only if the notification doesn't exist at all.
            // If it exists but belongs to another user, silently do nothing (security: don't reveal IDs).
            var exists = await _context.Notifications
                .AnyAsync(x => x.NotificationID == request.NotificationID.Value, cancellationToken);
            if (!exists)
                throw new NotFoundException(nameof(Domain.Entities.Notification), request.NotificationID.Value);

            var n = await _context.Notifications
                .FirstOrDefaultAsync(x => x.NotificationID == request.NotificationID.Value
                                       && x.AccountID == request.AccountID, cancellationToken);
            if (n is not null) n.IsRead = true;
        }
        else
        {
            // ExecuteUpdateAsync is not supported by EF Core InMemory provider.
            // Load matching rows and update them in memory instead.
            var unread = await _context.Notifications
                .Where(x => x.AccountID == request.AccountID && !x.IsRead)
                .ToListAsync(cancellationToken);
            foreach (var n in unread)
                n.IsRead = true;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
