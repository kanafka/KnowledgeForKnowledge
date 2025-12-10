using FluentValidation;

namespace Application.Features.Accounts.Commands.CreateAccount;

public class CreateAccountCommandValidator : AbstractValidator<CreateAccountCommand>
{
    public CreateAccountCommandValidator()
    {
        RuleFor(x => x.Login)
            .NotEmpty().WithMessage("Login is required")
            .MaximumLength(50).WithMessage("Login must not exceed 50 characters")
            .EmailAddress().WithMessage("Login must be a valid email address");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(6).WithMessage("Password must be at least 6 characters");

        RuleFor(x => x.TelegramID)
            .MaximumLength(50).WithMessage("TelegramID must not exceed 50 characters")
            .When(x => !string.IsNullOrEmpty(x.TelegramID));
    }
}


