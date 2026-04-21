using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.SkillRequests.Commands.CreateSkillRequest;

public class CreateSkillRequestCommandHandler : IRequestHandler<CreateSkillRequestCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateSkillRequestCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateSkillRequestCommand request, CancellationToken cancellationToken)
    {
        var skillExists = await _context.SkillsCatalog
            .AnyAsync(s => s.SkillID == request.SkillID, cancellationToken);
        if (!skillExists)
            throw new NotFoundException(nameof(Domain.Entities.SkillsCatalog), request.SkillID);

        var hasProfile = await _context.UserProfiles
            .AnyAsync(p => p.AccountID == request.AccountID, cancellationToken);
        if (!hasProfile)
            throw new ValidationException(new[]
            {
                new ValidationFailure("Profile", "Для создания запроса необходимо сначала заполнить профиль.")
            });

        var skillRequest = new SkillRequest
        {
            RequestID = Guid.NewGuid(),
            AccountID = request.AccountID,
            SkillID = request.SkillID,
            Title = request.Title,
            Details = request.Details,
            Status = RequestStatus.Open
        };

        _context.SkillRequests.Add(skillRequest);
        await _context.SaveChangesAsync(cancellationToken);
        return skillRequest.RequestID;
    }
}
