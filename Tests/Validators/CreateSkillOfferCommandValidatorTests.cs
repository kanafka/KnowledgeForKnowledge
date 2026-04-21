using Application.Features.SkillOffers.Commands.CreateSkillOffer;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace Tests.Validators;

public class CreateSkillOfferCommandValidatorTests
{
    private readonly CreateSkillOfferCommandValidator _validator = new();

    // --- SkillID ---

    [Fact]
    public void SkillID_Empty_HasError()
    {
        var cmd = new CreateSkillOfferCommand(Guid.NewGuid(), Guid.Empty, "Title", null);
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.SkillID)
            .WithErrorMessage("SkillID is required.");
    }

    [Fact]
    public void SkillID_Valid_NoError()
    {
        var cmd = new CreateSkillOfferCommand(Guid.NewGuid(), Guid.NewGuid(), "Title", null);
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.SkillID);
    }

    // --- Title ---

    [Fact]
    public void Title_Empty_HasError()
    {
        var cmd = new CreateSkillOfferCommand(Guid.NewGuid(), Guid.NewGuid(), "", null);
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage("Title is required.");
    }

    [Fact]
    public void Title_TooLong_HasError()
    {
        var cmd = new CreateSkillOfferCommand(Guid.NewGuid(), Guid.NewGuid(), new string('x', 101), null);
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage("Title must not exceed 100 characters.");
    }

    [Fact]
    public void Title_ExactlyMaxLength_NoError()
    {
        var cmd = new CreateSkillOfferCommand(Guid.NewGuid(), Guid.NewGuid(), new string('x', 100), null);
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Title_Valid_NoError()
    {
        var cmd = new CreateSkillOfferCommand(Guid.NewGuid(), Guid.NewGuid(), "Python tutoring", null);
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.Title);
    }

    // --- Details ---

    [Fact]
    public void Details_Null_NoError()
    {
        var cmd = new CreateSkillOfferCommand(Guid.NewGuid(), Guid.NewGuid(), "Title", null);
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.Details);
    }

    [Fact]
    public void Details_TooLong_HasError()
    {
        var cmd = new CreateSkillOfferCommand(Guid.NewGuid(), Guid.NewGuid(), "Title", new string('x', 2001));
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Details)
            .WithErrorMessage("Details must not exceed 2000 characters.");
    }

    [Fact]
    public void Details_ExactlyMaxLength_NoError()
    {
        var cmd = new CreateSkillOfferCommand(Guid.NewGuid(), Guid.NewGuid(), "Title", new string('x', 2000));
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.Details);
    }

    // --- Full valid ---

    [Fact]
    public void ValidCommand_PassesAllRules()
    {
        var cmd = new CreateSkillOfferCommand(Guid.NewGuid(), Guid.NewGuid(), "Python tutoring", "I can teach basics.");
        var result = _validator.TestValidate(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidCommand_NoDetails_PassesAllRules()
    {
        var cmd = new CreateSkillOfferCommand(Guid.NewGuid(), Guid.NewGuid(), "Python tutoring", null);
        var result = _validator.TestValidate(cmd);
        result.IsValid.Should().BeTrue();
    }
}
