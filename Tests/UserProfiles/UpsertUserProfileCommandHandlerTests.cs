using Application.Features.UserProfiles.Commands.UpsertUserProfile;
using FluentAssertions;
using Tests.Helpers;

namespace Tests.UserProfiles;

public class UpsertUserProfileCommandHandlerTests
{
    private static UpsertUserProfileCommandHandler CreateHandler(Infrastructure.Data.ApplicationDbContext ctx)
        => new(ctx);

    [Fact]
    public async Task Handle_NewProfile_CreatesProfile()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        ctx.Accounts.Add(account);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        await handler.Handle(
            new UpsertUserProfileCommand(account.AccountID, "John Doe", null, null, null, "I'm a dev"),
            CancellationToken.None);

        var profile = await ctx.UserProfiles.FindAsync(account.AccountID);
        profile.Should().NotBeNull();
        profile!.FullName.Should().Be("John Doe");
        profile.Description.Should().Be("I'm a dev");
        profile.DateOfBirth.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ExistingProfile_UpdatesInPlace()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        var existing = Fakes.Profile(account.AccountID, "Old Name");
        ctx.Accounts.Add(account);
        ctx.UserProfiles.Add(existing);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        await handler.Handle(
            new UpsertUserProfileCommand(account.AccountID, "New Name", null, "https://photo.url", "tg:@test", "Updated desc"),
            CancellationToken.None);

        ctx.UserProfiles.Should().HaveCount(1);
        var profile = await ctx.UserProfiles.FindAsync(account.AccountID);
        profile!.FullName.Should().Be("New Name");
        profile.PhotoURL.Should().Be("https://photo.url");
        profile.ContactInfo.Should().Be("tg:@test");
        profile.Description.Should().Be("Updated desc");
    }

    [Fact]
    public async Task Handle_DateOfBirth_NormalizedToUtc()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        ctx.Accounts.Add(account);
        await ctx.SaveChangesAsync();

        var dob = new DateTime(1995, 6, 15, 12, 30, 0, DateTimeKind.Unspecified);
        var handler = CreateHandler(ctx);
        await handler.Handle(
            new UpsertUserProfileCommand(account.AccountID, "Jane", dob, null, null, null),
            CancellationToken.None);

        var profile = await ctx.UserProfiles.FindAsync(account.AccountID);
        profile!.DateOfBirth.Should().NotBeNull();
        profile.DateOfBirth!.Value.Kind.Should().Be(DateTimeKind.Utc);
        profile.DateOfBirth.Value.Date.Should().Be(dob.Date);
    }

    [Fact]
    public async Task Handle_MultipleUpserts_OnlyOneProfileExists()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        ctx.Accounts.Add(account);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        await handler.Handle(new UpsertUserProfileCommand(account.AccountID, "First", null, null, null, null), CancellationToken.None);
        await handler.Handle(new UpsertUserProfileCommand(account.AccountID, "Second", null, null, null, null), CancellationToken.None);
        await handler.Handle(new UpsertUserProfileCommand(account.AccountID, "Third", null, null, null, null), CancellationToken.None);

        ctx.UserProfiles.Should().HaveCount(1);
        var profile = await ctx.UserProfiles.FindAsync(account.AccountID);
        profile!.FullName.Should().Be("Third");
    }
}
