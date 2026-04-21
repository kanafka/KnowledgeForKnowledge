using MediatR;

namespace Application.Features.Accounts.Commands.CreateAccount;

public class CreateAccountCommand : IRequest<CreateAccountResult>
{
    public string Login { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? TelegramID { get; set; }
    public bool CreateTelegramLinkToken { get; set; }
}

public record CreateAccountResult(Guid AccountId, string? TelegramLinkToken);
