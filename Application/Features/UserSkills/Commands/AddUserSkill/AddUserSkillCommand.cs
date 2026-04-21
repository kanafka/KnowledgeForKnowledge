using Domain.Enums;
using MediatR;

namespace Application.Features.UserSkills.Commands.AddUserSkill;

public record AddUserSkillCommand(
    Guid AccountID,
    Guid SkillID,
    SkillLevel Level,
    string? Description,
    string? LearnedAt
) : IRequest;
