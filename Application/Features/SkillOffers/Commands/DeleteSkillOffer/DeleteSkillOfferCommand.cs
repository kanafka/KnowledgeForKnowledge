using MediatR;

namespace Application.Features.SkillOffers.Commands.DeleteSkillOffer;

public record DeleteSkillOfferCommand(
    Guid OfferID,
    Guid AccountID,
    bool IsAdmin = false,
    string? DeletionReason = null) : IRequest;
