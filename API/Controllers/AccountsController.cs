using Application.Features.Accounts.Commands.CreateAccount;
using Application.Features.Accounts.Queries.GetAccount;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AccountsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<ActionResult<Guid>> CreateAccount([FromBody] CreateAccountCommand command)
    {
        var accountId = await _mediator.Send(command);
        return Ok(accountId);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AccountDto>> GetAccount(Guid id)
    {
        var query = new GetAccountQuery { AccountID = id };
        var account = await _mediator.Send(query);
        
        if (account == null)
        {
            return NotFound();
        }
        
        return Ok(account);
    }
}


