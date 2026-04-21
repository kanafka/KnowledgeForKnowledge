using Application.Features.UserSkills.Commands.AddUserSkill;
using Domain.Enums;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace Tests.Validators;

public class AddUserSkillCommandValidatorTests
{
    private readonly AddUserSkillCommandValidator _validator = new();

    // --- SkillID ---

    [Fact]
    public void SkillID_Empty_HasError()
    {
        var cmd = new AddUserSkillCommand(Guid.NewGuid(), Guid.Empty, SkillLevel.Middle, null, null);
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.SkillID)
            .WithErrorMessage("SkillID is required.");
    }

    [Fact]
    public void SkillID_Valid_NoError()
    {
        var cmd = new AddUserSkillCommand(Guid.NewGuid(), Guid.NewGuid(), SkillLevel.Middle, null, null);
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.SkillID);
    }

    // --- Description ---

    [Fact]
    public void Description_Null_NoError()
    {
        var cmd = new AddUserSkillCommand(Guid.NewGuid(), Guid.NewGuid(), SkillLevel.Middle, null, null);
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Description_TooLong_HasError()
    {
        var cmd = new AddUserSkillCommand(Guid.NewGuid(), Guid.NewGuid(), SkillLevel.Middle, new string('x', 501), null);
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Description)
            .WithErrorMessage("Description must not exceed 500 characters.");
    }

    [Fact]
    public void Description_ExactlyMaxLength_NoError()
    {
        var cmd = new AddUserSkillCommand(Guid.NewGuid(), Guid.NewGuid(), SkillLevel.Middle, new string('x', 500), null);
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    // --- LearnedAt ---

    [Fact]
    public void LearnedAt_Null_NoError()
    {
        var cmd = new AddUserSkillCommand(Guid.NewGuid(), Guid.NewGuid(), SkillLevel.Middle, null, null);
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.LearnedAt);
    }

    [Fact]
    public void LearnedAt_TooLong_HasError()
    {
        var cmd = new AddUserSkillCommand(Guid.NewGuid(), Guid.NewGuid(), SkillLevel.Middle, null, new string('x', 151));
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.LearnedAt)
            .WithErrorMessage("LearnedAt must not exceed 150 characters.");
    }

    [Fact]
    public void LearnedAt_ExactlyMaxLength_NoError()
    {
        var cmd = new AddUserSkillCommand(Guid.NewGuid(), Guid.NewGuid(), SkillLevel.Middle, null, new string('x', 150));
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.LearnedAt);
    }

    // --- All SkillLevel values allowed ---

    [Theory]
    [InlineData(SkillLevel.Junior)]
    [InlineData(SkillLevel.Middle)]
    [InlineData(SkillLevel.Senior)]
    public void AllSkillLevels_Valid(SkillLevel level)
    {
        var cmd = new AddUserSkillCommand(Guid.NewGuid(), Guid.NewGuid(), level, null, null);
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.Level);
    }

    // --- Full valid ---

    [Fact]
    public void ValidCommand_AllFields_PassesAllRules()
    {
        var cmd = new AddUserSkillCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            SkillLevel.Senior,
            "Extensive experience in Python development.",
            "2019");
        var result = _validator.TestValidate(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidCommand_MinimalFields_PassesAllRules()
    {
        var cmd = new AddUserSkillCommand(Guid.NewGuid(), Guid.NewGuid(), SkillLevel.Junior, null, null);
        var result = _validator.TestValidate(cmd);
        result.IsValid.Should().BeTrue();
    }
}
