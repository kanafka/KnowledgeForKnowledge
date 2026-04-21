using System.Security.Claims;
using Application.Features.Accounts.Commands.ChangePassword;
using Application.Features.Accounts.Commands.CreateAccount;
using Application.Features.Accounts.Commands.DeactivateAccount;
using Application.Features.Accounts.Commands.UpdateAccount;
using Application.Features.Accounts.Queries.GetAccount;
using Application.Features.Accounts.Queries.GetAccounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/accounts")]
public class AccountsController : BaseController
{
    private Guid CurrentAccountId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private bool IsAdmin =>
        User.IsInRole("Admin");

    /// <summary>Список всех аккаунтов с поиском (только Admin)</summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAccounts(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await Mediator.Send(new GetAccountsQuery(search, page, pageSize));
        return Ok(result);
    }

    /// <summary>Регистрация нового аккаунта</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreateAccountResult), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAccount([FromBody] CreateAccountCommand command)
    {
        var result = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetAccount), new { id = result.AccountId }, result);
    }

    /// <summary>Получить аккаунт текущего пользователя</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetMe()
    {
        var account = await Mediator.Send(new GetAccountQuery { AccountID = CurrentAccountId });
        return Ok(account);
    }

    /// <summary>Получить аккаунт по ID</summary>
    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetAccount(Guid id)
    {
        var account = await Mediator.Send(new GetAccountQuery { AccountID = id });
        return Ok(account);
    }

    /// <summary>Обновить данные аккаунта (TelegramID)</summary>
    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> UpdateAccount(Guid id, [FromBody] UpdateAccountRequest request)
    {
        if (CurrentAccountId != id) return Forbid();
        await Mediator.Send(new UpdateAccountCommand(id, request.TelegramID));
        return NoContent();
    }

    /// <summary>Сменить пароль (требует знание текущего пароля)</summary>
    [HttpPut("{id:guid}/password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(Guid id, [FromBody] ChangePasswordRequest request)
    {
        if (CurrentAccountId != id) return Forbid();
        await Mediator.Send(new ChangePasswordCommand(id, request.CurrentPassword, request.NewPassword));
        return NoContent();
    }

    /// <summary>Деактивировать аккаунт (себя или любой — для Admin)</summary>
    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        await Mediator.Send(new DeactivateAccountCommand(id, CurrentAccountId, IsAdmin));
        return NoContent();
    }

    /// <summary>Реактивировать аккаунт (только Admin)</summary>
    [HttpPut("{id:guid}/activate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Activate(Guid id)
    {
        var account = await Mediator.Send(new GetAccountQuery { AccountID = id });
        if (account is null) return NotFound();

        // Прямое обновление через context — минимальная логика
        var entity = await HttpContext.RequestServices
            .GetRequiredService<Application.Common.Interfaces.IApplicationDbContext>()
            .Accounts.FindAsync(id);
        if (entity is null) return NotFound();
        entity.IsActive = true;
        await HttpContext.RequestServices
            .GetRequiredService<Application.Common.Interfaces.IApplicationDbContext>()
            .SaveChangesAsync();
        return NoContent();
    }
}

public record UpdateAccountRequest(string? TelegramID);

public class ChangePasswordRequest
{
    public string CurrentPassword { get; init; } = string.Empty;
    public string NewPassword { get; init; } = string.Empty;
}
