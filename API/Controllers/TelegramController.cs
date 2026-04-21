using System.Security.Claims;
using System.Text.Json;
using Application.Common.Interfaces;
using Application.Features.Accounts.Commands.GenerateTelegramLinkToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace API.Controllers;

[ApiController]
[Route("api/telegram")]
public class TelegramController : BaseController
{
    private readonly IApplicationDbContext _context;
    private readonly IConfiguration _config;
    private readonly ITelegramService _telegram;

    public TelegramController(IApplicationDbContext context, IConfiguration config, ITelegramService telegram)
    {
        _context = context;
        _config = config;
        _telegram = telegram;
    }

    /// <summary>
    /// Webhook от Telegram Bot API.
    /// Для защиты установите секрет при регистрации вебхука:
    /// POST https://api.telegram.org/bot{TOKEN}/setWebhook?url=...&secret_token=YOUR_SECRET
    /// И пропишите Telegram:WebhookSecret в конфигурации.
    /// </summary>
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook([FromBody] JsonElement update)
    {
        // Проверка подписи (если настроена)
        var secret = _config["Telegram:WebhookSecret"];
        if (!string.IsNullOrEmpty(secret))
        {
            var header = Request.Headers["X-Telegram-Bot-Api-Secret-Token"].FirstOrDefault();
            if (header != secret)
                return Unauthorized();
        }

        try
        {
            if (!update.TryGetProperty("message", out var message))
                return Ok();

            if (!message.TryGetProperty("text", out var textElem)
                || !message.TryGetProperty("from", out var from))
                return Ok();

            var text = textElem.GetString() ?? string.Empty;
            var chatId = from.GetProperty("id").GetInt64().ToString();

            if (text.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
            {
                var parts = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length < 2)
                {
                    await _telegram.SendMessageAsync(chatId,
                        "Я на связи, но для привязки аккаунта нужна персональная команда вида /start TOKEN. Вернитесь на сайт и скопируйте команду из блока привязки Telegram.");
                    return Ok();
                }

                var token = parts[1].Trim().ToUpperInvariant();
                var account = await _context.Accounts
                    .FirstOrDefaultAsync(a => a.TelegramLinkToken == token);

                if (account is not null)
                {
                    account.TelegramID = chatId;
                    account.TelegramLinkToken = null;
                    await _context.SaveChangesAsync();
                    await _telegram.SendMessageAsync(chatId, "Telegram привязан к аккаунту. Теперь можно запросить код входа на сайте.");
                }
                else
                {
                    await _telegram.SendMessageAsync(chatId,
                        "Не нашёл аккаунт по этому токену. На сайте нажмите «Войти» ещё раз и скопируйте свежую команду /start TOKEN.");
                }
            }
        }
        catch
        {
            // Telegram всегда должен получить 200
        }

        return Ok();
    }

    /// <summary>
    /// Сгенерировать токен привязки Telegram.
    /// Пользователь отправляет боту: /start TOKEN
    /// </summary>
    [HttpPost("generate-link-token")]
    [Authorize]
    public async Task<IActionResult> GenerateLinkToken()
    {
        var accountId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var token = await Mediator.Send(new GenerateTelegramLinkTokenCommand(accountId));
        return Ok(new { token });
    }

    /// <summary>Получить настройки уведомлений текущего пользователя</summary>
    [HttpGet("notifications/settings")]
    [Authorize]
    public async Task<IActionResult> GetNotificationSettings()
    {
        var accountId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var account = await _context.Accounts.FindAsync(accountId);
        if (account is null) return NotFound();
        return Ok(new { notificationsEnabled = account.NotificationsEnabled });
    }

    /// <summary>Включить / отключить Telegram-уведомления</summary>
    [HttpPut("notifications/settings")]
    [Authorize]
    public async Task<IActionResult> UpdateNotificationSettings([FromBody] NotificationSettingsRequest request)
    {
        var accountId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var account = await _context.Accounts.FindAsync(accountId);
        if (account is null) return NotFound();

        account.NotificationsEnabled = request.NotificationsEnabled;
        await _context.SaveChangesAsync();
        return NoContent();
    }
}

public record NotificationSettingsRequest(bool NotificationsEnabled);
