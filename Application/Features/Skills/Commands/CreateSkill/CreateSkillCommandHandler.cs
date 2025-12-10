using Application.Common.Interfaces;
using Domain.Entities;
using MediatR;

namespace Application.Features.Skills.Commands.CreateSkill;

public class CreateSkillCommandHandler : IRequestHandler<CreateSkillCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateSkillCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateSkillCommand request, CancellationToken cancellationToken)
    {
        var skill = new SkillsCatalog
        {
            SkillID = Guid.NewGuid(),
            SkillName = request.SkillName,
            Epithet = request.Epithet
        };

        _context.SkillsCatalog.Add(skill);
        await _context.SaveChangesAsync(cancellationToken);

        return skill.SkillID;
    }
}


