using MediatR;

namespace Application.Features.Accounts.Queries.GetAccount;

public class GetAccountQuery : IRequest<AccountDto?>
{
    public Guid AccountID { get; set; }
}

public class AccountDto
{
    public Guid AccountID { get; set; }
    public string Login { get; set; } = string.Empty;
    public string? TelegramID { get; set; }
    public bool IsAdmin { get; set; }
    public DateTime CreatedAt { get; set; }
}


