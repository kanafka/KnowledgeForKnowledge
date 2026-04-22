using Application.Common.Exceptions;
using Application.Features.UserProfiles.Queries.GetUserProfile;
using FluentAssertions;
using Tests.Helpers;

namespace Tests.UserProfiles;

public class GetUserProfileQueryHandlerTests
{
    private static GetUserProfileQueryHandler CreateHandler(Infrastructure.Data.ApplicationDbContext ctx)
        => new(ctx);

    [Fact]
    public async Task Handle_AccountNotFound_ThrowsNotFoundException()
    {
        var ctx = TestDbContext.Create();
        var handler = CreateHandler(ctx);

        var act = () => handler.Handle(
            new GetUserProfileQuery(Guid.NewGuid()),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_AccountExistsNoProfile_ReturnsPartialDto()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account(login: "noname@t.com");
        ctx.Accounts.Add(account);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(
            new GetUserProfileQuery(account.AccountID),
            CancellationToken.None);

        result.HasProfile.Should().BeFalse();
        result.AccountID.Should().Be(account.AccountID);
        result.FullName.Should().Be("noname@t.com"); // Login used as fallback
        result.DateOfBirth.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithProfile_ReturnsFullDto()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        var profile = Fakes.Profile(account.AccountID, "John Doe");
        profile.Description = "A developer";
        profile.ContactInfo = "tg:@john";
        ctx.Accounts.Add(account);
        ctx.UserProfiles.Add(profile);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(
            new GetUserProfileQuery(account.AccountID, RequestingAccountID: account.AccountID),
            CancellationToken.None);

        result.HasProfile.Should().BeTrue();
        result.FullName.Should().Be("John Doe");
        result.Description.Should().Be("A developer");
        result.ContactInfo.Should().Be("tg:@john");
    }

    [Fact]
    public async Task Handle_OtherUserRequestingContactInfo_HidesContactInfo()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        var profile = Fakes.Profile(account.AccountID, "Jane");
        profile.ContactInfo = "secret@contact.com";
        var other = Fakes.Account(login: "other@t.com");
        ctx.Accounts.AddRange(account, other);
        ctx.UserProfiles.Add(profile);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(
            new GetUserProfileQuery(account.AccountID, RequestingAccountID: other.AccountID),
            CancellationToken.None);

        result.ContactInfo.Should().BeNull();
    }

    [Fact]
    public async Task Handle_OwnerRequestingOwnProfile_ShowsContactInfo()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        var profile = Fakes.Profile(account.AccountID, "Jane");
        profile.ContactInfo = "secret@contact.com";
        ctx.Accounts.Add(account);
        ctx.UserProfiles.Add(profile);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(
            new GetUserProfileQuery(account.AccountID, RequestingAccountID: account.AccountID),
            CancellationToken.None);

        result.ContactInfo.Should().Be("secret@contact.com");
    }

    [Fact]
    public async Task Handle_AdminRequestingProfile_ShowsContactInfo()
    {
        var ctx = TestDbContext.Create();
        var owner = Fakes.Account();
        var admin = Fakes.Account(login: "admin@t.com", isAdmin: true);
        var profile = Fakes.Profile(owner.AccountID, "User");
        profile.ContactInfo = "private@info.com";
        ctx.Accounts.AddRange(owner, admin);
        ctx.UserProfiles.Add(profile);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(
            new GetUserProfileQuery(owner.AccountID, RequestingAccountID: admin.AccountID),
            CancellationToken.None);

        result.ContactInfo.Should().Be("private@info.com");
    }

    [Fact]
    public async Task Handle_NoRequestingAccount_HidesContactInfo()
    {
        var ctx = TestDbContext.Create();
        var account = Fakes.Account();
        var profile = Fakes.Profile(account.AccountID, "User");
        profile.ContactInfo = "hidden@contact.com";
        ctx.Accounts.Add(account);
        ctx.UserProfiles.Add(profile);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(
            new GetUserProfileQuery(account.AccountID, RequestingAccountID: null),
            CancellationToken.None);

        result.ContactInfo.Should().BeNull();
    }
}
