using System.Security.Claims;
using Application.Features.VerificationRequests.Commands.ReviewVerificationRequest;
using Application.Features.VerificationRequests.Commands.SubmitVerificationRequest;
using Application.Features.VerificationRequests.Queries.GetVerificationRequests;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/verification")]
[Authorize]
public class VerificationRequestsController : BaseController
{
    private Guid CurrentAccountId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Получить список заявок.
    /// Admin: все заявки (accountId не обязателен).
    /// Обычный пользователь: только свои.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? accountId,
        [FromQuery] VerificationStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var isAdmin = User.IsInRole("Admin");

        // Обычный пользователь может видеть только свои заявки
        var filterAccount = isAdmin ? accountId : CurrentAccountId;

        var result = await Mediator.Send(
            new GetVerificationRequestsQuery(filterAccount, status, page, pageSize));

        return Ok(result);
    }

    /// <summary>Подать заявку на верификацию.</summary>
    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] SubmitVerificationRequest request)
    {
        var id = await Mediator.Send(
            new SubmitVerificationRequestCommand(
                CurrentAccountId,
                request.RequestType,
                request.ProofID));

        return Created($"/api/verification?accountId={CurrentAccountId}", new { id });
    }

    /// <summary>Рассмотреть заявку (только Admin).</summary>
    [HttpPut("{id:guid}/review")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Review(Guid id, [FromBody] ReviewBody body)
    {
        await Mediator.Send(new ReviewVerificationRequestCommand(id, body.Status, body.RejectionReason));
        return NoContent();
    }
}

public record SubmitVerificationRequest(VerificationRequestType RequestType, Guid? ProofID);
public record ReviewBody(VerificationStatus Status, string? RejectionReason);
