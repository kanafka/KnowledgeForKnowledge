using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SkillOffersController : ControllerBase
{
    private readonly IMediator _mediator;

    public SkillOffersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // TODO: Implement endpoints
    // [HttpGet]
    // [HttpGet("{id}")]
    // [HttpPost]
    // [HttpPut("{id}")]
    // [HttpDelete("{id}")]
}


