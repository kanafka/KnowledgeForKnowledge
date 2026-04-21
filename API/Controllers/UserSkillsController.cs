using System.Security.Claims;
using Application.Features.UserSkills.Commands.AddUserSkill;
using Application.Features.UserSkills.Commands.RemoveUserSkill;
using Application.Features.UserSkills.Queries.GetUserSkills;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/userskills")]
[Authorize]
public class UserSkillsController : BaseController
{
    /// <summary>Получить навыки пользователя</summary>
    [HttpGet("{accountId:guid}")]
    public async Task<IActionResult> GetUserSkills(Guid accountId)
    {
        var skills = await Mediator.Send(new GetUserSkillsQuery(accountId));
        return Ok(skills);
    }

    /// <summary>Добавить навык текущему пользователю</summary>
    [HttpPost]
    public async Task<IActionResult> AddSkill([FromBody] AddSkillRequest request)
    {
        var accountId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await Mediator.Send(new AddUserSkillCommand(
            accountId,
            request.SkillID,
            request.Level,
            request.Description,
            request.LearnedAt));
        return NoContent();
    }

    /// <summary>Удалить навык текущего пользователя</summary>
    [HttpDelete("{skillId:guid}")]
    public async Task<IActionResult> RemoveSkill(Guid skillId)
    {
        var accountId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await Mediator.Send(new RemoveUserSkillCommand(accountId, skillId));
        return NoContent();
    }
}

public record AddSkillRequest(
    Guid SkillID,
    SkillLevel Level,
    string? Description,
    string? LearnedAt);
