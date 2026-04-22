using Application.Features.Applications.Queries.GetApplications;
using Domain.Enums;
using FluentAssertions;
using Tests.Helpers;

namespace Tests.Applications;

public class GetApplicationsQueryHandlerTests
{
    private static GetApplicationsQueryHandler CreateHandler(Infrastructure.Data.ApplicationDbContext ctx) => new(ctx);

    [Fact]
    public async Task Handle_NoApplications_ReturnsEmpty()
    {
        var ctx = TestDbContext.Create();
        var handler = CreateHandler(ctx);
        var result = await handler.Handle(
            new GetApplicationsQuery(Guid.NewGuid(), ApplicationQueryType.Incoming, null),
            CancellationToken.None);
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Incoming_ReturnsApplicationsToMyOffer()
    {
        var ctx = TestDbContext.Create();
        var offerOwner = Fakes.Account();
        var applicant = Fakes.Account(login: "a@t.com");
        var skill = Fakes.Skill();
        var offer = Fakes.Offer(accountId: offerOwner.AccountID, skillId: skill.SkillID);
        var app = Fakes.Application(applicantId: applicant.AccountID, offerId: offer.OfferID);
        ctx.Accounts.AddRange(offerOwner, applicant);
        ctx.SkillsCatalog.Add(skill);
        ctx.SkillOffers.Add(offer);
        ctx.Applications.Add(app);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(
            new GetApplicationsQuery(offerOwner.AccountID, ApplicationQueryType.Incoming, null),
            CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items[0].ApplicantID.Should().Be(applicant.AccountID);
    }

    [Fact]
    public async Task Handle_Outgoing_ReturnsMyApplications()
    {
        var ctx = TestDbContext.Create();
        var offerOwner = Fakes.Account();
        var applicant = Fakes.Account(login: "a@t.com");
        var skill = Fakes.Skill();
        var offer = Fakes.Offer(accountId: offerOwner.AccountID, skillId: skill.SkillID);
        var app = Fakes.Application(applicantId: applicant.AccountID, offerId: offer.OfferID);
        ctx.Accounts.AddRange(offerOwner, applicant);
        ctx.SkillsCatalog.Add(skill);
        ctx.SkillOffers.Add(offer);
        ctx.Applications.Add(app);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(
            new GetApplicationsQuery(applicant.AccountID, ApplicationQueryType.Outgoing, null),
            CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items[0].OfferID.Should().Be(offer.OfferID);
    }

    [Fact]
    public async Task Handle_Processed_ReturnsAcceptedAndRejected()
    {
        var ctx = TestDbContext.Create();
        var offerOwner = Fakes.Account();
        var applicant = Fakes.Account(login: "a@t.com");
        var skill = Fakes.Skill();
        var offer = Fakes.Offer(accountId: offerOwner.AccountID, skillId: skill.SkillID);
        var pending = Fakes.Application(applicantId: applicant.AccountID, offerId: offer.OfferID, status: ApplicationStatus.Pending);
        var accepted = Fakes.Application(applicantId: applicant.AccountID, offerId: offer.OfferID, status: ApplicationStatus.Accepted);
        ctx.Accounts.AddRange(offerOwner, applicant);
        ctx.SkillsCatalog.Add(skill);
        ctx.SkillOffers.Add(offer);
        ctx.Applications.AddRange(pending, accepted);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(
            new GetApplicationsQuery(offerOwner.AccountID, ApplicationQueryType.Processed, null),
            CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items[0].Status.Should().Be(ApplicationStatus.Accepted);
    }

    [Fact]
    public async Task Handle_FilterByStatus_ReturnsOnlyThat()
    {
        var ctx = TestDbContext.Create();
        var offerOwner = Fakes.Account();
        var applicant = Fakes.Account(login: "a@t.com");
        var skill = Fakes.Skill();
        var offer = Fakes.Offer(accountId: offerOwner.AccountID, skillId: skill.SkillID);
        var rejected = Fakes.Application(applicantId: applicant.AccountID, offerId: offer.OfferID, status: ApplicationStatus.Rejected);
        var accepted = Fakes.Application(applicantId: applicant.AccountID, offerId: offer.OfferID, status: ApplicationStatus.Accepted);
        ctx.Accounts.AddRange(offerOwner, applicant);
        ctx.SkillsCatalog.Add(skill);
        ctx.SkillOffers.Add(offer);
        ctx.Applications.AddRange(rejected, accepted);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(
            new GetApplicationsQuery(offerOwner.AccountID, ApplicationQueryType.Processed, ApplicationStatus.Rejected),
            CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items[0].Status.Should().Be(ApplicationStatus.Rejected);
    }

    [Fact]
    public async Task Handle_Incoming_ToSkillRequest()
    {
        var ctx = TestDbContext.Create();
        var requestOwner = Fakes.Account();
        var applicant = Fakes.Account(login: "a@t.com");
        var skill = Fakes.Skill();
        var request = Fakes.Request(accountId: requestOwner.AccountID, skillId: skill.SkillID);
        var app = Fakes.Application(applicantId: applicant.AccountID, requestId: request.RequestID);
        ctx.Accounts.AddRange(requestOwner, applicant);
        ctx.SkillsCatalog.Add(skill);
        ctx.SkillRequests.Add(request);
        ctx.Applications.Add(app);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(
            new GetApplicationsQuery(requestOwner.AccountID, ApplicationQueryType.Incoming, null),
            CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items[0].SkillRequestID.Should().Be(request.RequestID);
    }

    [Fact]
    public async Task Handle_ApplicantWithProfile_ReturnsProfileName()
    {
        var ctx = TestDbContext.Create();
        var offerOwner = Fakes.Account();
        var applicant = Fakes.Account(login: "a@t.com");
        ctx.UserProfiles.Add(Fakes.Profile(applicant.AccountID, "Anna Smith"));
        var skill = Fakes.Skill();
        var offer = Fakes.Offer(accountId: offerOwner.AccountID, skillId: skill.SkillID);
        var app = Fakes.Application(applicantId: applicant.AccountID, offerId: offer.OfferID);
        ctx.Accounts.AddRange(offerOwner, applicant);
        ctx.SkillsCatalog.Add(skill);
        ctx.SkillOffers.Add(offer);
        ctx.Applications.Add(app);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(
            new GetApplicationsQuery(offerOwner.AccountID, ApplicationQueryType.Incoming, null),
            CancellationToken.None);

        result.Items[0].ApplicantName.Should().Be("Anna Smith");
    }

    [Fact]
    public async Task Handle_Pagination_Works()
    {
        var ctx = TestDbContext.Create();
        var offerOwner = Fakes.Account();
        var skill = Fakes.Skill();
        ctx.Accounts.Add(offerOwner);
        ctx.SkillsCatalog.Add(skill);

        for (var i = 0; i < 6; i++)
        {
            var applicant = Fakes.Account(login: $"a{i}@t.com");
            var offer = Fakes.Offer(accountId: offerOwner.AccountID, skillId: skill.SkillID);
            var app = Fakes.Application(applicantId: applicant.AccountID, offerId: offer.OfferID);
            ctx.Accounts.Add(applicant);
            ctx.SkillOffers.Add(offer);
            ctx.Applications.Add(app);
        }
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(
            new GetApplicationsQuery(offerOwner.AccountID, ApplicationQueryType.Incoming, null, Page: 2, PageSize: 4),
            CancellationToken.None);

        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(6);
    }
}
