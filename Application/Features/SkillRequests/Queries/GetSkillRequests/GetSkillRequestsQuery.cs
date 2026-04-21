using Application.Common.Models;
using Domain.Enums;
using MediatR;

namespace Application.Features.SkillRequests.Queries.GetSkillRequests;

public record GetSkillRequestsQuery(
    Guid? SkillID,
    Guid? AccountID,
    RequestStatus? Status,
    string? Search,
    Guid? HelperAccountID,
    bool? CanHelp,
    bool? RequireBarter,
    int Page = 1,
    int PageSize = 20
) : IRequest<PagedResult<SkillRequestDto>>;

public record SkillRequestDto(
    Guid RequestID,
    Guid AccountID,
    string AuthorName,
    string? AuthorPhotoURL,
    Guid SkillID,
    string SkillName,
    string Title,
    string? Details,
    RequestStatus Status
);
