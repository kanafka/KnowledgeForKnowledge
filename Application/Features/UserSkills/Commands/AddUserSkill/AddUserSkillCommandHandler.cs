using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.UserSkills.Commands.AddUserSkill;

public class AddUserSkillCommandHandler : IRequestHandler<AddUserSkillCommand>
{
    private readonly IApplicationDbContext _context;

    public AddUserSkillCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(AddUserSkillCommand request, CancellationToken cancellationToken)
    {
        var skillExists = await _context.SkillsCatalog
            .AnyAsync(skill => skill.SkillID == request.SkillID, cancellationToken);

        if (!skillExists)
            throw new NotFoundException(nameof(SkillsCatalog), request.SkillID);

        var normalizedDescription = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();
        var normalizedLearnedAt = string.IsNullOrWhiteSpace(request.LearnedAt)
            ? null
            : request.LearnedAt.Trim();

        var existingSkill = await _context.UserSkills
            .FirstOrDefaultAsync(us => us.AccountID == request.AccountID && us.SkillID == request.SkillID, cancellationToken);

        if (existingSkill is not null)
        {
            existingSkill.SkillLevel = request.Level;
            existingSkill.Description = normalizedDescription;
            existingSkill.LearnedAt = normalizedLearnedAt;

            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        _context.UserSkills.Add(new UserSkill
        {
            AccountID = request.AccountID,
            SkillID = request.SkillID,
            SkillLevel = request.Level,
            Description = normalizedDescription,
            LearnedAt = normalizedLearnedAt,
            IsVerified = false
        });

        await _context.SaveChangesAsync(cancellationToken);
    }
}
