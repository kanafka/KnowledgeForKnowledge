using FluentValidation;

namespace Application.Features.Skills.Commands.CreateSkill;

public class CreateSkillCommandValidator : AbstractValidator<CreateSkillCommand>
{
    public CreateSkillCommandValidator()
    {
        RuleFor(x => x.SkillName)
            .NotEmpty().WithMessage("SkillName is required")
            .MaximumLength(100).WithMessage("SkillName must not exceed 100 characters");
    }
}


