using MediatR;

namespace Application.Features.Accounts.Commands.CreateAccount;

public class CreateAccountCommand : IRequest<Guid>
{
    public string Login { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? TelegramID { get; set; }
}


