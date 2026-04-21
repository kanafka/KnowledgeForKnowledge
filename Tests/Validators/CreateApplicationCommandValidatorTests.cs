using Application.Features.Applications.Commands.CreateApplication;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace Tests.Validators;

public class CreateApplicationCommandValidatorTests
{
    private readonly CreateApplicationCommandValidator _validator = new();

    // --- XOR rule: exactly one of OfferID / SkillRequestID ---

    [Fact]
    public void BothNull_HasError()
    {
        var cmd = new CreateApplicationCommand(Guid.NewGuid(), null, null, null);
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x)
            .WithErrorMessage("Укажите ровно одно из полей: OfferID или SkillRequestID.");
    }

    [Fact]
    public void BothProvided_HasError()
    {
        var cmd = new CreateApplicationCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null);
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x)
            .WithErrorMessage("Укажите ровно одно из полей: OfferID или SkillRequestID.");
    }

    [Fact]
    public void OnlyOfferId_NoError()
    {
        var cmd = new CreateApplicationCommand(Guid.NewGuid(), Guid.NewGuid(), null, null);
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x);
    }

    [Fact]
    public void OnlySkillRequestId_NoError()
    {
        var cmd = new CreateApplicationCommand(Guid.NewGuid(), null, Guid.NewGuid(), null);
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x);
    }

    // --- Message ---

    [Fact]
    public void Message_Null_NoError()
    {
        var cmd = new CreateApplicationCommand(Guid.NewGuid(), Guid.NewGuid(), null, null);
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.Message);
    }

    [Fact]
    public void Message_TooLong_HasError()
    {
        var cmd = new CreateApplicationCommand(Guid.NewGuid(), Guid.NewGuid(), null, new string('x', 1001));
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Message)
            .WithErrorMessage("Message must not exceed 1000 characters.");
    }

    [Fact]
    public void Message_ExactlyMaxLength_NoError()
    {
        var cmd = new CreateApplicationCommand(Guid.NewGuid(), Guid.NewGuid(), null, new string('x', 1000));
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.Message);
    }

    [Fact]
    public void ValidCommand_PassesAllRules()
    {
        var cmd = new CreateApplicationCommand(Guid.NewGuid(), Guid.NewGuid(), null, "Hi, I'd like to help!");
        var result = _validator.TestValidate(cmd);
        result.IsValid.Should().BeTrue();
    }
}
