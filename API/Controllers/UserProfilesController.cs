using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserProfilesController : ControllerBase
{
    private readonly IMediator _mediator;

    public UserProfilesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // TODO: Implement endpoints
    // [HttpGet("{accountId}")]
    // [HttpPost]
    // [HttpPut("{accountId}")]
}


