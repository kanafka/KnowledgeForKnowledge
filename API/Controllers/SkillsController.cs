using Application.Features.Skills.Commands.CreateSkill;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SkillsController : ControllerBase
{
    private readonly IMediator _mediator;

    public SkillsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<ActionResult<Guid>> CreateSkill([FromBody] CreateSkillCommand command)
    {
        var skillId = await _mediator.Send(command);
        return Ok(skillId);
    }

    // TODO: Add other endpoints
    // [HttpGet]
    // [HttpGet("{id}")]
    // [HttpPut("{id}")]
    // [HttpDelete("{id}")]
}


