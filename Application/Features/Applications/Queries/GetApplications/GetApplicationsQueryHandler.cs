using Application.Common.Interfaces;
using Application.Common.Models;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Applications.Queries.GetApplications;

public class GetApplicationsQueryHandler : IRequestHandler<GetApplicationsQuery, PagedResult<ApplicationDto>>
{
    private readonly IApplicationDbContext _context;

    public GetApplicationsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<ApplicationDto>> Handle(GetApplicationsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Applications
            .Include(a => a.Applicant).ThenInclude(acc => acc.UserProfile)
            .Include(a => a.SkillOffer).ThenInclude(o => o!.SkillsCatalog)
            .Include(a => a.SkillOffer).ThenInclude(o => o!.Account)
            .Include(a => a.SkillRequest).ThenInclude(r => r!.SkillsCatalog)
            .Include(a => a.SkillRequest).ThenInclude(r => r!.Account)
            .Where(a => a.SkillOffer != null || a.SkillRequest != null)
            .AsQueryable();

        query = request.QueryType switch
        {
            ApplicationQueryType.Incoming =>
                query.Where(a =>
                    (a.SkillOffer != null && a.SkillOffer.AccountID == request.CurrentAccountID
                     || a.SkillRequest != null && a.SkillRequest.AccountID == request.CurrentAccountID)
                    && a.Status == ApplicationStatus.Pending),
            ApplicationQueryType.Outgoing =>
                query.Where(a => a.ApplicantID == request.CurrentAccountID && a.Status == ApplicationStatus.Pending),
            ApplicationQueryType.Processed =>
                query.Where(a =>
                    ((a.SkillOffer != null && a.SkillOffer.AccountID == request.CurrentAccountID)
                     || (a.SkillRequest != null && a.SkillRequest.AccountID == request.CurrentAccountID)
                     || a.ApplicantID == request.CurrentAccountID)
                    && a.Status != ApplicationStatus.Pending),
            _ => query
        };

        if (request.Status.HasValue)
            query = query.Where(a => a.Status == request.Status.Value);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new ApplicationDto(
                a.ApplicationID,
                a.ApplicantID,
                a.Applicant.UserProfile != null ? a.Applicant.UserProfile.FullName : a.Applicant.Login,
                a.Applicant.TelegramID,
                a.OfferID,
                a.SkillOffer != null ? a.SkillOffer.Title : null,
                a.SkillRequestID,
                a.SkillRequest != null ? a.SkillRequest.Title : null,
                a.SkillOffer != null ? a.SkillOffer.SkillsCatalog.SkillName
                    : a.SkillRequest != null ? a.SkillRequest.SkillsCatalog.SkillName : null,
                a.Status,
                a.Message,
                a.CreatedAt))
            .ToListAsync(cancellationToken);

        return PagedResult<ApplicationDto>.Create(items, total, request.Page, request.PageSize);
    }
}
