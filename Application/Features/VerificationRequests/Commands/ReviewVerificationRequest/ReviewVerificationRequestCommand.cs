using Domain.Enums;
using MediatR;

namespace Application.Features.VerificationRequests.Commands.ReviewVerificationRequest;

/// <summary>Одобрить или отклонить заявку на верификацию (только Admin).</summary>
public record ReviewVerificationRequestCommand(
    Guid RequestID,
    VerificationStatus NewStatus,
    string? RejectionReason = null
) : IRequest;
