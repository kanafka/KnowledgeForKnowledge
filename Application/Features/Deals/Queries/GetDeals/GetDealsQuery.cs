using MediatR;

namespace Application.Features.Deals.Queries.GetDeals;

public record GetDealsQuery(Guid AccountID, int Page = 1, int PageSize = 20) : IRequest<GetDealsResult>;

public record DealDto(
    Guid DealID,
    Guid ApplicationID,
    Guid InitiatorID,
    string InitiatorName,
    Guid PartnerID,
    string PartnerName,
    string Status,
    bool MyReviewExists,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    string? OfferTitle,
    string? RequestTitle);

public record GetDealsResult(List<DealDto> Items, int TotalCount, int Page, int PageSize, int TotalPages);
