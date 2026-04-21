using FluentValidation;

namespace Application.Features.UserSkills.Commands.AddUserSkill;

public class AddUserSkillCommandValidator : AbstractValidator<AddUserSkillCommand>
{
    public AddUserSkillCommandValidator()
    {
        RuleFor(x => x.SkillID)
            .NotEmpty().WithMessage("SkillID is required.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters.")
            .When(x => x.Description != null);

        RuleFor(x => x.LearnedAt)
            .MaximumLength(150).WithMessage("LearnedAt must not exceed 150 characters.")
            .When(x => x.LearnedAt != null);
    }
}
