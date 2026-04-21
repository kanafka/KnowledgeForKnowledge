using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Deals.Queries.GetDeals;

public class GetDealsQueryHandler : IRequestHandler<GetDealsQuery, GetDealsResult>
{
    private readonly IApplicationDbContext _context;

    public GetDealsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<GetDealsResult> Handle(GetDealsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Deals
            .Include(d => d.Initiator).ThenInclude(a => a.UserProfile)
            .Include(d => d.Partner).ThenInclude(a => a.UserProfile)
            .Include(d => d.Application).ThenInclude(a => a.SkillOffer)
            .Include(d => d.Application).ThenInclude(a => a.SkillRequest)
            .Include(d => d.Reviews)
            .Where(d => d.InitiatorID == request.AccountID || d.PartnerID == request.AccountID)
            .OrderByDescending(d => d.CreatedAt);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(d => new DealDto(
                d.DealID,
                d.ApplicationID,
                d.InitiatorID,
                d.Initiator.UserProfile != null ? d.Initiator.UserProfile.FullName : d.Initiator.Login,
                d.PartnerID,
                d.Partner.UserProfile != null ? d.Partner.UserProfile.FullName : d.Partner.Login,
                d.Status.ToString(),
                d.Reviews.Any(r => r.AuthorID == request.AccountID),
                d.CreatedAt,
                d.CompletedAt,
                d.Application.SkillOffer != null ? d.Application.SkillOffer.Title : null,
                d.Application.SkillRequest != null ? d.Application.SkillRequest.Title : null))
            .ToListAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling(total / (double)request.PageSize);
        return new GetDealsResult(items, total, request.Page, request.PageSize, totalPages);
    }
}
