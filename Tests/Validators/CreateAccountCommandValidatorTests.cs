using Application.Features.Accounts.Commands.CreateAccount;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace Tests.Validators;

public class CreateAccountCommandValidatorTests
{
    private readonly CreateAccountCommandValidator _validator = new();

    // --- Login ---

    [Fact]
    public void Login_Empty_HasError()
    {
        var cmd = new CreateAccountCommand { Login = "", Password = "Valid1pass" };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Login)
            .WithErrorMessage("Login is required.");
    }

    [Fact]
    public void Login_NotEmail_HasError()
    {
        var cmd = new CreateAccountCommand { Login = "notanemail", Password = "Valid1pass" };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Login)
            .WithErrorMessage("Login must be a valid email address.");
    }

    [Fact]
    public void Login_TooLong_HasError()
    {
        var cmd = new CreateAccountCommand
        {
            Login = new string('a', 45) + "@test.com",  // > 50 chars
            Password = "Valid1pass"
        };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Login)
            .WithErrorMessage("Login must not exceed 50 characters.");
    }

    [Fact]
    public void Login_ValidEmail_NoError()
    {
        var cmd = new CreateAccountCommand { Login = "user@test.com", Password = "Valid1pass" };
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.Login);
    }

    // --- Password ---

    [Fact]
    public void Password_Empty_HasError()
    {
        var cmd = new CreateAccountCommand { Login = "u@t.com", Password = "" };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password is required.");
    }

    [Fact]
    public void Password_TooShort_HasError()
    {
        var cmd = new CreateAccountCommand { Login = "u@t.com", Password = "Ab1" };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must be at least 8 characters.");
    }

    [Fact]
    public void Password_NoUppercase_HasError()
    {
        var cmd = new CreateAccountCommand { Login = "u@t.com", Password = "alllower1" };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one uppercase letter.");
    }

    [Fact]
    public void Password_NoDigit_HasError()
    {
        var cmd = new CreateAccountCommand { Login = "u@t.com", Password = "NoDigitPass" };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one digit.");
    }

    [Fact]
    public void Password_Valid_NoError()
    {
        var cmd = new CreateAccountCommand { Login = "u@t.com", Password = "Valid1pass" };
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    // --- TelegramID (optional) ---

    [Fact]
    public void TelegramID_Null_NoError()
    {
        var cmd = new CreateAccountCommand { Login = "u@t.com", Password = "Valid1pass", TelegramID = null };
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.TelegramID);
    }

    [Fact]
    public void TelegramID_TooLong_HasError()
    {
        var cmd = new CreateAccountCommand
        {
            Login = "u@t.com",
            Password = "Valid1pass",
            TelegramID = new string('x', 51)
        };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.TelegramID)
            .WithErrorMessage("TelegramID must not exceed 50 characters.");
    }

    [Fact]
    public void TelegramID_ExactlyMaxLength_NoError()
    {
        var cmd = new CreateAccountCommand
        {
            Login = "u@t.com",
            Password = "Valid1pass",
            TelegramID = new string('x', 50)
        };
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.TelegramID);
    }

    // --- Full valid command ---

    [Fact]
    public void ValidCommand_PassesAllRules()
    {
        var cmd = new CreateAccountCommand { Login = "user@example.com", Password = "Secure1pass" };
        var result = _validator.TestValidate(cmd);
        result.IsValid.Should().BeTrue();
    }
}
