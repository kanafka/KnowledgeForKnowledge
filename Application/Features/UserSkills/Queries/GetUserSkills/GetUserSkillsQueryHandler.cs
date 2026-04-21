using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.UserSkills.Queries.GetUserSkills;

public class GetUserSkillsQueryHandler : IRequestHandler<GetUserSkillsQuery, List<UserSkillDto>>
{
    private readonly IApplicationDbContext _context;

    public GetUserSkillsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<UserSkillDto>> Handle(GetUserSkillsQuery request, CancellationToken cancellationToken)
    {
        return await _context.UserSkills
            .Include(us => us.SkillsCatalog)
            .Where(us => us.AccountID == request.AccountID)
            .Select(us => new UserSkillDto(
                us.SkillID,
                us.SkillsCatalog.SkillName,
                us.SkillsCatalog.Epithet,
                us.SkillLevel,
                us.Description,
                us.LearnedAt,
                us.IsVerified))
            .ToListAsync(cancellationToken);
    }
}
