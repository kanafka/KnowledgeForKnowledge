using Domain.Enums;
using MediatR;

namespace Application.Features.Skills.Commands.CreateSkill;

public class CreateSkillCommand : IRequest<Guid>
{
    public string SkillName { get; set; } = string.Empty;
    public SkillEpithet Epithet { get; set; }
}


