using Application.Common.Interfaces;
using Application.Common.Models;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.SkillOffers.Queries.GetSkillOffers;

public class GetSkillOffersQueryHandler : IRequestHandler<GetSkillOffersQuery, PagedResult<SkillOfferDto>>
{
    private readonly IApplicationDbContext _context;

    public GetSkillOffersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<SkillOfferDto>> Handle(GetSkillOffersQuery request, CancellationToken cancellationToken)
    {
        var query = _context.SkillOffers
            .Include(offer => offer.SkillsCatalog)
            .Include(offer => offer.Account)
                .ThenInclude(account => account.UserProfile)
            .AsQueryable();

        if (request.SkillID.HasValue)
            query = query.Where(offer => offer.SkillID == request.SkillID.Value);

        if (request.AccountID.HasValue)
            query = query.Where(offer => offer.AccountID == request.AccountID.Value);

        if (request.IsActive.HasValue)
            query = query.Where(offer => offer.IsActive == request.IsActive.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.ToLower();
            query = query.Where(offer =>
                offer.Title.ToLower().Contains(search) ||
                (offer.Details != null && offer.Details.ToLower().Contains(search)) ||
                offer.SkillsCatalog.SkillName.ToLower().Contains(search));
        }

        if (request.ViewerAccountID.HasValue && request.ViewerHasSkill.HasValue)
        {
            if (request.ViewerHasSkill.Value)
            {
                query = query.Where(offer =>
                    _context.UserSkills.Any(
                        userSkill => userSkill.AccountID == request.ViewerAccountID.Value && userSkill.SkillID == offer.SkillID));
            }
            else
            {
                query = query.Where(offer =>
                    !_context.UserSkills.Any(
                        userSkill => userSkill.AccountID == request.ViewerAccountID.Value && userSkill.SkillID == offer.SkillID));
            }
        }

        if (request.ViewerAccountID.HasValue && request.RequireBarter == true)
        {
            query = query.Where(offer =>
                _context.SkillRequests.Any(skillRequest =>
                    skillRequest.AccountID == offer.AccountID &&
                    skillRequest.Status == RequestStatus.Open &&
                    _context.UserSkills.Any(
                        userSkill =>
                            userSkill.AccountID == request.ViewerAccountID.Value &&
                            userSkill.SkillID == skillRequest.SkillID)));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(offer => offer.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(offer => new SkillOfferDto(
                offer.OfferID,
                offer.AccountID,
                offer.Account.UserProfile != null ? offer.Account.UserProfile.FullName : offer.Account.Login,
                offer.Account.UserProfile != null ? offer.Account.UserProfile.PhotoURL : null,
                offer.SkillID,
                offer.SkillsCatalog.SkillName,
                offer.Title,
                offer.Details,
                offer.IsActive))
            .ToListAsync(cancellationToken);

        return PagedResult<SkillOfferDto>.Create(items, total, request.Page, request.PageSize);
    }
}
