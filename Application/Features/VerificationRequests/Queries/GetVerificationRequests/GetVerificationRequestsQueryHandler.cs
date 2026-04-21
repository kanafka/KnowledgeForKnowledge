using Application.Common.Interfaces;
using Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.VerificationRequests.Queries.GetVerificationRequests;

public class GetVerificationRequestsQueryHandler
    : IRequestHandler<GetVerificationRequestsQuery, PagedResult<VerificationRequestDto>>
{
    private readonly IApplicationDbContext _context;

    public GetVerificationRequestsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<VerificationRequestDto>> Handle(
        GetVerificationRequestsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.VerificationRequests
            .AsNoTracking()
            .Include(r => r.Account).ThenInclude(a => a.UserProfile)
            .Include(r => r.Proof).ThenInclude(p => p!.SkillsCatalog)
            .AsQueryable();

        if (request.AccountID.HasValue)
            query = query.Where(r => r.AccountID == request.AccountID.Value);

        if (request.Status.HasValue)
            query = query.Where(r => r.Status == request.Status.Value);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(r => new VerificationRequestDto(
                r.RequestID,
                r.AccountID,
                r.Account.UserProfile != null ? r.Account.UserProfile.FullName : r.Account.Login,
                r.RequestType,
                r.Status,
                r.ProofID,
                r.Proof != null ? r.Proof.FileURL : null,
                r.Proof != null ? r.Proof.SkillID : null,
                r.Proof != null && r.Proof.SkillsCatalog != null ? r.Proof.SkillsCatalog.SkillName : null,
                r.Proof != null && r.Proof.SkillID.HasValue
                    ? _context.UserSkills
                        .Where(us => us.AccountID == r.AccountID && us.SkillID == r.Proof.SkillID.Value)
                        .Select(us => (Domain.Enums.SkillLevel?)us.SkillLevel)
                        .FirstOrDefault()
                    : null,
                r.CreatedAt))
            .ToListAsync(cancellationToken);

        return PagedResult<VerificationRequestDto>.Create(items, total, request.Page, request.PageSize);
    }
}
