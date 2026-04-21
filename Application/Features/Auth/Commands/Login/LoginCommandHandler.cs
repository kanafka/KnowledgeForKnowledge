using System.Security.Cryptography;
using Application.Common.Interfaces;
using Application.Features.Auth.Commands.VerifyOtp;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Application.Features.Auth.Commands.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResult>
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITelegramService _telegram;
    private readonly IMemoryCache _cache;

    public LoginCommandHandler(
        IApplicationDbContext context,
        IJwtService jwtService,
        IPasswordHasher passwordHasher,
        ITelegramService telegram,
        IMemoryCache cache)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _telegram = telegram;
        _cache = cache;
    }

    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Login == request.Login, cancellationToken);

        if (account is null)
            throw new UnauthorizedAccessException("Неверный логин или пароль.");

        if (!account.IsActive)
            throw new UnauthorizedAccessException("Аккаунт деактивирован.");

        if (account.LockoutUntil.HasValue && account.LockoutUntil.Value > DateTime.UtcNow)
        {
            var remaining = (int)(account.LockoutUntil.Value - DateTime.UtcNow).TotalMinutes + 1;
            throw new UnauthorizedAccessException(
                $"Аккаунт временно заблокирован. Попробуйте через {remaining} мин.");
        }

        if (account.LockoutUntil.HasValue && account.LockoutUntil.Value <= DateTime.UtcNow)
        {
            account.LockoutUntil = null;
            account.FailedLoginAttempts = 0;
        }

        if (!_passwordHasher.Verify(request.Password, account.PasswordHash))
        {
            account.FailedLoginAttempts++;
            if (account.FailedLoginAttempts >= MaxFailedAttempts)
            {
                account.LockoutUntil = DateTime.UtcNow.Add(LockoutDuration);
                account.FailedLoginAttempts = 0;
                await _context.SaveChangesAsync(cancellationToken);
                throw new UnauthorizedAccessException(
                    $"Слишком много неверных попыток. Аккаунт заблокирован на {(int)LockoutDuration.TotalMinutes} мин.");
            }

            await _context.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAccessException("Неверный логин или пароль.");
        }

        if (account.FailedLoginAttempts > 0)
        {
            account.FailedLoginAttempts = 0;
            account.LockoutUntil = null;
        }

        if (string.IsNullOrEmpty(account.TelegramID))
        {
            account.TelegramLinkToken ??= await GenerateUniqueTelegramLinkToken(cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            return new LoginResult(
                string.Empty,
                account.AccountID,
                account.IsAdmin,
                RequiresTelegramLink: true,
                TelegramLinkToken: account.TelegramLinkToken);
        }

        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var sessionId = Guid.NewGuid().ToString("N");

        _cache.Set($"otp:{sessionId}", new OtpSession(account.AccountID, code, 0),
            TimeSpan.FromMinutes(5));

        await _context.SaveChangesAsync(cancellationToken);
        await _telegram.SendOtpAsync(account.TelegramID, code, cancellationToken);

        return new LoginResult(string.Empty, account.AccountID, account.IsAdmin,
            RequiresOtp: true, SessionId: sessionId);
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
