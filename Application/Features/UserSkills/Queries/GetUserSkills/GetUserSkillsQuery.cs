using Domain.Enums;
using MediatR;

namespace Application.Features.UserSkills.Queries.GetUserSkills;

public record GetUserSkillsQuery(Guid AccountID) : IRequest<List<UserSkillDto>>;

public record UserSkillDto(
    Guid SkillID,
    string SkillName,
    SkillEpithet Epithet,
    SkillLevel Level,
    string? Description,
    string? LearnedAt,
    bool IsVerified
);
