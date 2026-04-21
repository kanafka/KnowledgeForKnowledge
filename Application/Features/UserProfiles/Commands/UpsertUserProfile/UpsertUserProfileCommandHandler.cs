using Application.Common.Interfaces;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.UserProfiles.Commands.UpsertUserProfile;

public class UpsertUserProfileCommandHandler : IRequestHandler<UpsertUserProfileCommand>
{
    private readonly IApplicationDbContext _context;

    public UpsertUserProfileCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(UpsertUserProfileCommand request, CancellationToken cancellationToken)
    {
        var profile = await _context.UserProfiles
            .FirstOrDefaultAsync(p => p.AccountID == request.AccountID, cancellationToken);

        if (profile is null)
        {
            profile = new UserProfile { AccountID = request.AccountID };
            _context.UserProfiles.Add(profile);
        }

        DateTime? normalizedDateOfBirth = request.DateOfBirth.HasValue
            ? DateTime.SpecifyKind(request.DateOfBirth.Value.Date, DateTimeKind.Utc)
            : null;

        profile.FullName = request.FullName;
        profile.DateOfBirth = normalizedDateOfBirth;
        profile.PhotoURL = request.PhotoURL;
        profile.ContactInfo = request.ContactInfo;
        profile.Description = request.Description;

        await _context.SaveChangesAsync(cancellationToken);
    }
}
