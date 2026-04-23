using System.Security.Cryptography;
using Application.Common.Interfaces;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Accounts.Commands.CreateAccount;

public class CreateAccountCommandHandler : IRequestHandler<CreateAccountCommand, CreateAccountResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;

    public CreateAccountCommandHandler(IApplicationDbContext context, IPasswordHasher passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    public async Task<CreateAccountResult> Handle(CreateAccountCommand request, CancellationToken cancellationToken)
    {
        var exists = await _context.Accounts
            .AnyAsync(a => a.Login == request.Login, cancellationToken);
        if (exists)
            throw new InvalidOperationException("An account with this login already exists.");

        var telegramLinkToken = string.IsNullOrWhiteSpace(request.TelegramID) && request.CreateTelegramLinkToken
            ? await GenerateUniqueTelegramLinkToken(cancellationToken)
            : null;

        var account = new Account
        {
            AccountID   = Guid.NewGuid(),
            Login       = request.Login,
            PasswordHash = _passwordHasher.Hash(request.Password),
            TelegramID  = request.TelegramID,
            TelegramLinkToken = telegramLinkToken,
            IsAdmin     = false,
            CreatedAt   = DateTime.UtcNow
        };

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync(cancellationToken);

        return new CreateAccountResult(account.AccountID, telegramLinkToken);
    }

    private async Task<string> GenerateUniqueTelegramLinkToken(CancellationToken cancellationToken)
    {
        string token;
        do
        {
            token = GenerateToken();
        }
        while (await _context.Accounts.AnyAsync(account => account.TelegramLinkToken == token, cancellationToken));

        return token;
    }

    private static string GenerateToken()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return new string(Enumerable.Range(0, 8)
            .Select(_ => chars[RandomNumberGenerator.GetInt32(chars.Length)])
            .ToArray());
    }
}
