using Application.Common.Exceptions;
using Application.Features.Education.Commands.AddEducation;
using Application.Features.Education.Commands.DeleteEducation;
using FluentAssertions;
using Tests.Helpers;

namespace Tests.Education;

public class EducationCommandHandlerTests
{
    private static AddEducationCommandHandler CreateAddHandler(Infrastructure.Data.ApplicationDbContext ctx)
        => new(ctx);

    private static DeleteEducationCommandHandler CreateDeleteHandler(Infrastructure.Data.ApplicationDbContext ctx)
        => new(ctx);

    // --- AddEducation ---

    [Fact]
    public async Task AddEducation_Valid_CreatesRecord()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        ctx.Accounts.Add(account);
        await ctx.SaveChangesAsync();

        var handler = CreateAddHandler(ctx);
        var educationId = await handler.Handle(
            new AddEducationCommand(account.AccountID, "НИУ ВШЭ", "Computer Science", 2020),
            CancellationToken.None);

        educationId.Should().NotBeEmpty();
        var edu = await ctx.Educations.FindAsync(educationId);
        edu.Should().NotBeNull();
        edu!.InstitutionName.Should().Be("НИУ ВШЭ");
        edu.DegreeField.Should().Be("Computer Science");
        edu.YearCompleted.Should().Be(2020);
        edu.AccountID.Should().Be(account.AccountID);
    }

    [Fact]
    public async Task AddEducation_MultipleRecords_AllSaved()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        ctx.Accounts.Add(account);
        await ctx.SaveChangesAsync();

        var handler = CreateAddHandler(ctx);
        await handler.Handle(new AddEducationCommand(account.AccountID, "School", null, 2015), CancellationToken.None);
        await handler.Handle(new AddEducationCommand(account.AccountID, "University", "Math", 2019), CancellationToken.None);
        await handler.Handle(new AddEducationCommand(account.AccountID, "Online course", "ML", null), CancellationToken.None);

        ctx.Educations.Count(e => e.AccountID == account.AccountID).Should().Be(3);
    }

    [Fact]
    public async Task AddEducation_NullOptionalFields_Saves()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        ctx.Accounts.Add(account);
        await ctx.SaveChangesAsync();

        var handler = CreateAddHandler(ctx);
        var id = await handler.Handle(
            new AddEducationCommand(account.AccountID, "Some School", null, null),
            CancellationToken.None);

        var edu = await ctx.Educations.FindAsync(id);
        edu!.DegreeField.Should().BeNull();
        edu.YearCompleted.Should().BeNull();
    }

    // --- DeleteEducation ---

    [Fact]
    public async Task DeleteEducation_NotFound_ThrowsNotFoundException()
    {
        var ctx = TestDbContext.Create();
        var handler = CreateDeleteHandler(ctx);

        var act = () => handler.Handle(
            new DeleteEducationCommand(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeleteEducation_WrongOwner_ThrowsUnauthorized()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        var edu = Fakes.Education(accountId: account.AccountID);
        ctx.Accounts.Add(account);
        ctx.Educations.Add(edu);
        await ctx.SaveChangesAsync();

        var handler = CreateDeleteHandler(ctx);
        var act = () => handler.Handle(
            new DeleteEducationCommand(edu.EducationID, Guid.NewGuid()),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*доступа*");
    }

    [Fact]
    public async Task DeleteEducation_Owner_DeletesRecord()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        var edu = Fakes.Education(accountId: account.AccountID);
        ctx.Accounts.Add(account);
        ctx.Educations.Add(edu);
        await ctx.SaveChangesAsync();

        var handler = CreateDeleteHandler(ctx);
        await handler.Handle(
            new DeleteEducationCommand(edu.EducationID, account.AccountID),
            CancellationToken.None);

        var deleted = await ctx.Educations.FindAsync(edu.EducationID);
        deleted.Should().BeNull();
    }
}
