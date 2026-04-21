using Application.Features.Reviews.Commands.CreateReview;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace Tests.Validators;

public class CreateReviewCommandValidatorTests
{
    private readonly CreateReviewCommandValidator _validator = new();

    // --- DealID ---

    [Fact]
    public void DealID_Empty_HasError()
    {
        var cmd = new CreateReviewCommand(Guid.Empty, Guid.NewGuid(), 5, null);
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.DealID)
            .WithErrorMessage("DealID is required.");
    }

    [Fact]
    public void DealID_Valid_NoError()
    {
        var cmd = new CreateReviewCommand(Guid.NewGuid(), Guid.NewGuid(), 5, null);
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.DealID);
    }

    // --- Rating ---

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(6)]
    [InlineData(100)]
    public void Rating_OutOfRange_HasError(int rating)
    {
        var cmd = new CreateReviewCommand(Guid.NewGuid(), Guid.NewGuid(), rating, null);
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Rating)
            .WithErrorMessage("Rating must be between 1 and 5.");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Rating_Valid_NoError(int rating)
    {
        var cmd = new CreateReviewCommand(Guid.NewGuid(), Guid.NewGuid(), rating, null);
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.Rating);
    }

    // --- Comment ---

    [Fact]
    public void Comment_Null_NoError()
    {
        var cmd = new CreateReviewCommand(Guid.NewGuid(), Guid.NewGuid(), 5, null);
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.Comment);
    }

    [Fact]
    public void Comment_TooLong_HasError()
    {
        var cmd = new CreateReviewCommand(Guid.NewGuid(), Guid.NewGuid(), 5, new string('x', 2001));
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Comment)
            .WithErrorMessage("Comment must not exceed 2000 characters.");
    }

    [Fact]
    public void Comment_ExactlyMaxLength_NoError()
    {
        var cmd = new CreateReviewCommand(Guid.NewGuid(), Guid.NewGuid(), 5, new string('x', 2000));
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.Comment);
    }

    // --- Full valid ---

    [Fact]
    public void ValidCommand_PassesAllRules()
    {
        var cmd = new CreateReviewCommand(Guid.NewGuid(), Guid.NewGuid(), 4, "Great experience!");
        var result = _validator.TestValidate(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidCommand_NoComment_PassesAllRules()
    {
        var cmd = new CreateReviewCommand(Guid.NewGuid(), Guid.NewGuid(), 3, null);
        var result = _validator.TestValidate(cmd);
        result.IsValid.Should().BeTrue();
    }
}
