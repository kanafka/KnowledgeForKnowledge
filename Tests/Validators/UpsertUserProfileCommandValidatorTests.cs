using Application.Features.UserProfiles.Commands.UpsertUserProfile;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace Tests.Validators;

public class UpsertUserProfileCommandValidatorTests
{
    private readonly UpsertUserProfileCommandValidator _validator = new();

    // --- FullName ---

    [Fact]
    public void FullName_Empty_HasError()
    {
        var cmd = new UpsertUserProfileCommand(Guid.NewGuid(), "", null, null, null, null);
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.FullName)
            .WithErrorMessage("FullName is required.");
    }

    [Fact]
    public void FullName_TooLong_HasError()
    {
        var cmd = new UpsertUserProfileCommand(Guid.NewGuid(), new string('x', 151), null, null, null, null);
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.FullName)
            .WithErrorMessage("FullName must not exceed 150 characters.");
    }

    [Fact]
    public void FullName_ExactlyMaxLength_NoError()
    {
        var cmd = new UpsertUserProfileCommand(Guid.NewGuid(), new string('x', 150), null, null, null, null);
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.FullName);
    }

    [Fact]
    public void FullName_Valid_NoError()
    {
        var cmd = new UpsertUserProfileCommand(Guid.NewGuid(), "Иван Иванов", null, null, null, null);
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.FullName);
    }

    // --- DateOfBirth ---

    [Fact]
    public void DateOfBirth_Null_NoError()
    {
        var cmd = new UpsertUserProfileCommand(Guid.NewGuid(), "Name", null, null, null, null);
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.DateOfBirth);
    }

    [Fact]
    public void DateOfBirth_FutureDate_HasError()
    {
        var cmd = new UpsertUserProfileCommand(Guid.NewGuid(), "Name", DateTime.UtcNow.AddDays(1), null, null, null);
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.DateOfBirth)
            .WithErrorMessage("Date of birth must be in the past.");
    }

    [Fact]
    public void DateOfBirth_TooFarInPast_HasError()
    {
        var cmd = new UpsertUserProfileCommand(Guid.NewGuid(), "Name", DateTime.UtcNow.AddYears(-121), null, null, null);
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.DateOfBirth)
            .WithErrorMessage("Date of birth is not valid.");
    }

    [Fact]
    public void DateOfBirth_ValidPastDate_NoError()
    {
        var cmd = new UpsertUserProfileCommand(Guid.NewGuid(), "Name", new DateTime(1990, 5, 15), null, null, null);
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.DateOfBirth);
    }

    // --- ContactInfo ---

    [Fact]
    public void ContactInfo_Null_NoError()
    {
        var cmd = new UpsertUserProfileCommand(Guid.NewGuid(), "Name", null, null, null, null);
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.ContactInfo);
    }

    [Fact]
    public void ContactInfo_TooLong_HasError()
    {
        var cmd = new UpsertUserProfileCommand(Guid.NewGuid(), "Name", null, null, new string('x', 256), null);
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.ContactInfo)
            .WithErrorMessage("ContactInfo must not exceed 255 characters.");
    }

    [Fact]
    public void ContactInfo_ExactlyMaxLength_NoError()
    {
        var cmd = new UpsertUserProfileCommand(Guid.NewGuid(), "Name", null, null, new string('x', 255), null);
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.ContactInfo);
    }

    // --- Description ---

    [Fact]
    public void Description_Null_NoError()
    {
        var cmd = new UpsertUserProfileCommand(Guid.NewGuid(), "Name", null, null, null, null);
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Description_TooLong_HasError()
    {
        var cmd = new UpsertUserProfileCommand(Guid.NewGuid(), "Name", null, null, null, new string('x', 3001));
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Description)
            .WithErrorMessage("Description must not exceed 3000 characters.");
    }

    [Fact]
    public void Description_ExactlyMaxLength_NoError()
    {
        var cmd = new UpsertUserProfileCommand(Guid.NewGuid(), "Name", null, null, null, new string('x', 3000));
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    // --- Full valid ---

    [Fact]
    public void ValidCommand_AllFields_PassesAllRules()
    {
        var cmd = new UpsertUserProfileCommand(
            Guid.NewGuid(),
            "Иван Иванов",
            new DateTime(1990, 5, 15),
            "https://photo.url",
            "tg:@ivan",
            "Backend developer with 5 years of experience.");
        var result = _validator.TestValidate(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidCommand_MinimalFields_PassesAllRules()
    {
        var cmd = new UpsertUserProfileCommand(Guid.NewGuid(), "Иван", null, null, null, null);
        var result = _validator.TestValidate(cmd);
        result.IsValid.Should().BeTrue();
    }
}
