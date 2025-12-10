using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProofsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProofsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // TODO: Implement endpoints
    // [HttpGet("{accountId}")]
    // [HttpPost]
    // [HttpPut("{id}")]
    // [HttpDelete("{id}")]
}


