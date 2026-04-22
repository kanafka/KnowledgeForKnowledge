using Application.Features.Skills.Queries.GetSkills;
using Domain.Enums;
using FluentAssertions;
using Tests.Helpers;

namespace Tests.Skills;

public class GetSkillsQueryHandlerTests
{
    private static GetSkillsQueryHandler CreateHandler(Infrastructure.Data.ApplicationDbContext ctx) => new(ctx);

    [Fact]
    public async Task Handle_NoCatalog_ReturnsEmpty()
    {
        var ctx = TestDbContext.Create();
        var handler = CreateHandler(ctx);
        var result = await handler.Handle(new GetSkillsQuery(null, null), CancellationToken.None);
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_NoFilter_ReturnsAll()
    {
        var ctx = TestDbContext.Create();
        ctx.SkillsCatalog.AddRange(
            Fakes.Skill(name: "Python"),
            Fakes.Skill(name: "Java"),
            Fakes.Skill(name: "C#"));
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(new GetSkillsQuery(null, null), CancellationToken.None);

        result.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task Handle_SearchByName_FiltersCorrectly()
    {
        var ctx = TestDbContext.Create();
        ctx.SkillsCatalog.AddRange(
            Fakes.Skill(name: "Python"),
            Fakes.Skill(name: "Java"),
            Fakes.Skill(name: "JavaScript"));
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(new GetSkillsQuery("java", null), CancellationToken.None);

        result.TotalCount.Should().Be(2);
        result.Items.Should().Contain(s => s.SkillName == "Java");
        result.Items.Should().Contain(s => s.SkillName == "JavaScript");
    }

    [Fact]
    public async Task Handle_SearchCaseInsensitive()
    {
        var ctx = TestDbContext.Create();
        ctx.SkillsCatalog.Add(Fakes.Skill(name: "Python"));
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(new GetSkillsQuery("PYTHON", null), CancellationToken.None);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_FilterByEpithet_ReturnsOnlyMatching()
    {
        var ctx = TestDbContext.Create();
        var it = new Domain.Entities.SkillsCatalog { SkillID = Guid.NewGuid(), SkillName = "Python", Epithet = SkillEpithet.IT };
        var creative = new Domain.Entities.SkillsCatalog { SkillID = Guid.NewGuid(), SkillName = "Drawing", Epithet = SkillEpithet.Music };
        ctx.SkillsCatalog.AddRange(it, creative);
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(new GetSkillsQuery(null, SkillEpithet.IT), CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items[0].SkillName.Should().Be("Python");
    }

    [Fact]
    public async Task Handle_SearchAndEpithetCombined()
    {
        var ctx = TestDbContext.Create();
        ctx.SkillsCatalog.AddRange(
            new Domain.Entities.SkillsCatalog { SkillID = Guid.NewGuid(), SkillName = "Python", Epithet = SkillEpithet.IT },
            new Domain.Entities.SkillsCatalog { SkillID = Guid.NewGuid(), SkillName = "Python painting", Epithet = SkillEpithet.Music });
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(new GetSkillsQuery("python", SkillEpithet.IT), CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items[0].Epithet.Should().Be(SkillEpithet.IT);
    }

    [Fact]
    public async Task Handle_ResultsOrderedByName()
    {
        var ctx = TestDbContext.Create();
        ctx.SkillsCatalog.AddRange(
            Fakes.Skill(name: "Rust"),
            Fakes.Skill(name: "C#"),
            Fakes.Skill(name: "Go"));
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var result = await handler.Handle(new GetSkillsQuery(null, null), CancellationToken.None);

        result.Items[0].SkillName.Should().Be("C#");
        result.Items[1].SkillName.Should().Be("Go");
        result.Items[2].SkillName.Should().Be("Rust");
    }

    [Fact]
    public async Task Handle_Pagination_Works()
    {
        var ctx = TestDbContext.Create();
        for (var i = 1; i <= 8; i++)
            ctx.SkillsCatalog.Add(Fakes.Skill(name: $"Skill{i:D2}"));
        await ctx.SaveChangesAsync();

        var handler = CreateHandler(ctx);
        var page1 = await handler.Handle(new GetSkillsQuery(null, null, Page: 1, PageSize: 5), CancellationToken.None);
        var page2 = await handler.Handle(new GetSkillsQuery(null, null, Page: 2, PageSize: 5), CancellationToken.None);

        page1.Items.Should().HaveCount(5);
        page2.Items.Should().HaveCount(3);
        page1.TotalCount.Should().Be(8);
    }
}
