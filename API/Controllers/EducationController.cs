using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EducationController : ControllerBase
{
    private readonly IMediator _mediator;

    public EducationController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // TODO: Implement endpoints
    // [HttpGet("{accountId}")]
    // [HttpPost]
    // [HttpPut("{id}")]
    // [HttpDelete("{id}")]
}


