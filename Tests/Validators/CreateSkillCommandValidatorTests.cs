using Application.Features.Skills.Commands.CreateSkill;
using Domain.Enums;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace Tests.Validators;

public class CreateSkillCommandValidatorTests
{
    private readonly CreateSkillCommandValidator _validator = new();

    // --- SkillName ---

    [Fact]
    public void SkillName_Empty_HasError()
    {
        var cmd = new CreateSkillCommand { SkillName = "", Epithet = SkillEpithet.IT };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.SkillName)
            .WithErrorMessage("SkillName is required");
    }

    [Fact]
    public void SkillName_Whitespace_HasError()
    {
        var cmd = new CreateSkillCommand { SkillName = "   ", Epithet = SkillEpithet.IT };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.SkillName);
    }

    [Fact]
    public void SkillName_TooLong_HasError()
    {
        var cmd = new CreateSkillCommand { SkillName = new string('x', 101), Epithet = SkillEpithet.IT };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.SkillName)
            .WithErrorMessage("SkillName must not exceed 100 characters");
    }

    [Fact]
    public void SkillName_ExactlyMaxLength_NoError()
    {
        var cmd = new CreateSkillCommand { SkillName = new string('x', 100), Epithet = SkillEpithet.IT };
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.SkillName);
    }

    [Fact]
    public void SkillName_Valid_NoError()
    {
        var cmd = new CreateSkillCommand { SkillName = "Python", Epithet = SkillEpithet.IT };
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.SkillName);
    }

    // --- Full valid ---

    [Fact]
    public void ValidCommand_PassesAllRules()
    {
        var cmd = new CreateSkillCommand { SkillName = "Python", Epithet = SkillEpithet.IT };
        var result = _validator.TestValidate(cmd);
        result.IsValid.Should().BeTrue();
    }
}
