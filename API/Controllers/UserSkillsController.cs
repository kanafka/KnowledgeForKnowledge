using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserSkillsController : ControllerBase
{
    private readonly IMediator _mediator;

    public UserSkillsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // TODO: Implement endpoints
    // [HttpGet("{accountId}")]
    // [HttpPost]
    // [HttpPut]
    // [HttpDelete]
}


