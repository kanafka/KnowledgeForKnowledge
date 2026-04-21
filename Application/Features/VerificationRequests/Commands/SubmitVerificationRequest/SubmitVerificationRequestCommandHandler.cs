using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.VerificationRequests.Commands.SubmitVerificationRequest;

public class SubmitVerificationRequestCommandHandler
    : IRequestHandler<SubmitVerificationRequestCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public SubmitVerificationRequestCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(
        SubmitVerificationRequestCommand request,
        CancellationToken cancellationToken)
    {
        if (request.RequestType == VerificationRequestType.SkillVerify && !request.ProofID.HasValue)
            throw new ValidationException(new[] {
                new ValidationFailure("ProofID", "Для проверки навыка нужно выбрать пруф.")
            });

        Proof? proof = null;

        if (request.ProofID.HasValue)
        {
            proof = await _context.Proofs
                .FirstOrDefaultAsync(p => p.ProofID == request.ProofID.Value, cancellationToken)
                ?? throw new ValidationException(new[] {
                    new ValidationFailure("ProofID", "Указанный пруф не найден.")
                });

            if (proof.AccountID != request.AccountID)
                throw new UnauthorizedAccessException("Нельзя отправлять на проверку чужой пруф.");
        }

        if (request.RequestType == VerificationRequestType.SkillVerify)
        {
            if (proof?.SkillID is null)
                throw new ValidationException(new[] {
                    new ValidationFailure("ProofID", "Для проверки навыка пруф должен быть привязан к конкретному навыку.")
                });

            if (proof.IsVerified)
                throw new InvalidOperationException("Этот пруф уже подтвержден администратором.");
        }

        var hasPendingRequest = await _context.VerificationRequests
            .AnyAsync(r =>
                r.AccountID == request.AccountID
                && r.RequestType == request.RequestType
                && r.ProofID == request.ProofID
                && r.Status == VerificationStatus.Pending,
                cancellationToken);

        if (hasPendingRequest)
            throw new InvalidOperationException("По этому пруфу уже есть заявка на проверку.");

        var entity = new VerificationRequest
        {
            RequestID   = Guid.NewGuid(),
            AccountID   = request.AccountID,
            RequestType = request.RequestType,
            ProofID     = request.ProofID,
            Status      = VerificationStatus.Pending,
            CreatedAt   = DateTime.UtcNow
        };

        _context.VerificationRequests.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return entity.RequestID;
    }
}
