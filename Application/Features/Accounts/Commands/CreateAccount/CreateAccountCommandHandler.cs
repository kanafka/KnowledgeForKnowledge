using Application.Common.Interfaces;
using Domain.Entities;
using MediatR;

namespace Application.Features.Accounts.Commands.CreateAccount;

public class CreateAccountCommandHandler : IRequestHandler<CreateAccountCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateAccountCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateAccountCommand request, CancellationToken cancellationToken)
    {
        // TODO: Implement password hashing
        var account = new Account
        {
            AccountID = Guid.NewGuid(),
            Login = request.Login,
            PasswordHash = request.Password, // TODO: Hash password
            TelegramID = request.TelegramID,
            IsAdmin = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync(cancellationToken);

        return account.AccountID;
    }
}


