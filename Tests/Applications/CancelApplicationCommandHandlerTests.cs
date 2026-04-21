using Application.Common.Exceptions;
using Application.Features.Applications.Commands.CancelApplication;
using Domain.Enums;
using FluentAssertions;
using Tests.Helpers;

namespace Tests.Applications;

public class CancelApplicationCommandHandlerTests
{
    private static CancelApplicationCommandHandler CreateHandler(Infrastructure.Data.ApplicationDbContext ctx)
        => new(ctx);

    [Fact]
    public async Task Handle_ApplicationNotFound_ThrowsNotFoundException()
    {
        var ctx = TestDbContext.Create();
        var handler = CreateHandler(ctx);

        var act = () => handler.Handle(
            new CancelApplicationCommand(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WrongApplicant_ThrowsUnauthorized()
    {
        var ctx = TestDbContext.Create();
        var app = Fakes.Application();
        ctx.Applications.Add(app);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var act = () => handler.Handle(
            new CancelApplicationCommand(app.ApplicationID, Guid.NewGuid()),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*доступа*");
    }

    [Fact]
    public async Task Handle_AlreadyAccepted_ThrowsInvalidOperation()
    {
        var ctx = TestDbContext.Create();
        var applicant = Fakes.Account();
        var app = Fakes.Application(applicantId: applicant.AccountID, status: ApplicationStatus.Accepted);
        ctx.Accounts.Add(applicant);
        ctx.Applications.Add(app);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var act = () => handler.Handle(
            new CancelApplicationCommand(app.ApplicationID, applicant.AccountID),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*обработанный*");
    }

    [Fact]
    public async Task Handle_AlreadyRejected_ThrowsInvalidOperation()
    {
        var ctx = TestDbContext.Create();
        var applicant = Fakes.Account();
        var app = Fakes.Application(applicantId: applicant.AccountID, status: ApplicationStatus.Rejected);
        ctx.Accounts.Add(applicant);
        ctx.Applications.Add(app);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var act = () => handler.Handle(
            new CancelApplicationCommand(app.ApplicationID, applicant.AccountID),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Handle_PendingApplication_DeletesIt()
    {
        var ctx = TestDbContext.Create();
        var applicant = Fakes.Account();
        var app = Fakes.Application(applicantId: applicant.AccountID, status: ApplicationStatus.Pending);
        ctx.Accounts.Add(applicant);
        ctx.Applications.Add(app);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        await handler.Handle(
            new CancelApplicationCommand(app.ApplicationID, applicant.AccountID),
            CancellationToken.None);

        var deleted = await ctx.Applications.FindAsync(app.ApplicationID);
        deleted.Should().BeNull();
    }
}
