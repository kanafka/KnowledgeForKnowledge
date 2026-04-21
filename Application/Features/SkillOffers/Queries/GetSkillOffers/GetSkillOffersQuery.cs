using Application.Common.Models;
using MediatR;

namespace Application.Features.SkillOffers.Queries.GetSkillOffers;

public record GetSkillOffersQuery(
    Guid? SkillID,
    Guid? AccountID,
    bool? IsActive,
    string? Search,
    Guid? ViewerAccountID,
    bool? ViewerHasSkill,
    bool? RequireBarter,
    int Page = 1,
    int PageSize = 20
) : IRequest<PagedResult<SkillOfferDto>>;

public record SkillOfferDto(
    Guid OfferID,
    Guid AccountID,
    string AuthorName,
    string? AuthorPhotoURL,
    Guid SkillID,
    string SkillName,
    string Title,
    string? Details,
    bool IsActive
);
