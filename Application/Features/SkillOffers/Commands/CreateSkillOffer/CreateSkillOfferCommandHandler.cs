using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.SkillOffers.Commands.CreateSkillOffer;

public class CreateSkillOfferCommandHandler : IRequestHandler<CreateSkillOfferCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateSkillOfferCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateSkillOfferCommand request, CancellationToken cancellationToken)
    {
        var skillExists = await _context.SkillsCatalog
            .AnyAsync(s => s.SkillID == request.SkillID, cancellationToken);
        if (!skillExists)
            throw new NotFoundException(nameof(Domain.Entities.SkillsCatalog), request.SkillID);

        var hasProfile = await _context.UserProfiles
            .AnyAsync(p => p.AccountID == request.AccountID, cancellationToken);
        if (!hasProfile)
            throw new ValidationException(new[] {
                new ValidationFailure("Profile", "Для создания предложения необходимо сначала заполнить профиль.")
            });

        var hasUserSkill = await _context.UserSkills
            .AnyAsync(us => us.AccountID == request.AccountID && us.SkillID == request.SkillID, cancellationToken);
        if (!hasUserSkill)
            throw new ValidationException(new[] {
                new ValidationFailure("SkillID", "Можно публиковать карточки только по навыкам, которые уже добавлены в личный кабинет.")
            });

        var offer = new SkillOffer
        {
            OfferID = Guid.NewGuid(),
            AccountID = request.AccountID,
            SkillID = request.SkillID,
            Title = request.Title,
            Details = request.Details,
            IsActive = true
        };

        _context.SkillOffers.Add(offer);
        await _context.SaveChangesAsync(cancellationToken);
        return offer.OfferID;
    }
}
