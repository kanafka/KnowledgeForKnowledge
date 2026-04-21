using System.Security.Claims;
using Application.Features.SkillOffers.Commands.CreateSkillOffer;
using Application.Features.SkillOffers.Commands.DeleteSkillOffer;
using Application.Features.SkillOffers.Commands.UpdateSkillOffer;
using Application.Features.SkillOffers.Queries.GetSkillOfferById;
using Application.Features.SkillOffers.Queries.GetSkillOffers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace API.Controllers;

[ApiController]
[Route("api/skilloffers")]
public class SkillOffersController : BaseController
{
    private bool IsAdmin =>
        User.IsInRole("Admin");

    /// <summary>Список предложений с фильтрацией и пагинацией</summary>
    [HttpGet]
    public async Task<IActionResult> GetOffers(
        [FromQuery] Guid? skillId,
        [FromQuery] Guid? accountId,
        [FromQuery] bool? isActive,
        [FromQuery] string? search,
        [FromQuery] Guid? viewerAccountId,
        [FromQuery] bool? viewerHasSkill,
        [FromQuery] bool? requireBarter,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await Mediator.Send(
            new GetSkillOffersQuery(skillId, accountId, isActive, search, viewerAccountId, viewerHasSkill, requireBarter, page, pageSize));
        return Ok(result);
    }

    /// <summary>Карточка предложения по ID</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetOffer(Guid id)
    {
        var offer = await Mediator.Send(new GetSkillOfferByIdQuery(id));
        return Ok(offer);
    }

    /// <summary>Создать предложение</summary>
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateOffer([FromBody] CreateOfferRequest request)
    {
        var accountId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var offerId = await Mediator.Send(
            new CreateSkillOfferCommand(accountId, request.SkillID, request.Title, request.Details));
        return CreatedAtAction(nameof(GetOffer), new { id = offerId }, new { id = offerId });
    }

    /// <summary>Обновить предложение</summary>
    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> UpdateOffer(Guid id, [FromBody] UpdateOfferRequest request)
    {
        var accountId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await Mediator.Send(new UpdateSkillOfferCommand(id, accountId, request.Title, request.Details, request.IsActive));
        return NoContent();
    }

    /// <summary>Удалить предложение</summary>
    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteOffer(Guid id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] DeleteCardRequest? request)
    {
        var accountId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await Mediator.Send(new DeleteSkillOfferCommand(id, accountId, IsAdmin, request?.DeletionReason));
        return NoContent();
    }
}

public class CreateOfferRequest
{
    public Guid SkillID { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Details { get; init; }
}

public class UpdateOfferRequest
{
    public string Title { get; init; } = string.Empty;
    public string? Details { get; init; }
    public bool IsActive { get; init; }
}

public record DeleteCardRequest(string? DeletionReason);
