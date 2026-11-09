using FluentValidation;
using SchoolAdmission.Application.Behaviors;
using SchoolAdmission.Application.Common.Exceptions;
using SchoolAdmission.Application.Common.Validation;
using SchoolAdmission.Domain.Errors;

namespace SchoolAdmission.UnitTests.Application;

public sealed class ValidationBehaviorTests
{
    private sealed record SampleRequest(string? StudentId, string? Name, int? TotalSeats);

    private sealed class StudentValidator : AbstractValidator<SampleRequest>
    {
        public StudentValidator() => RuleFor(x => x.StudentId).ValidStudentId();
    }

    private sealed class BodyValidator : AbstractValidator<SampleRequest>
    {
        public BodyValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required.");
            RuleFor(x => x.TotalSeats).NotNull().InclusiveBetween(1, 10_000).WithMessage("Total seats must be between 1 and 10000.");
        }
    }

    private static async Task<(bool nextCalled, Exception? error)> Run(SampleRequest request, params IValidator<SampleRequest>[] validators)
    {
        var behavior = new ValidationBehavior<SampleRequest, string>(validators);
        var called = false;
        try
        {
            await behavior.Handle(request, _ => { called = true; return Task.FromResult("ok"); }, CancellationToken.None);
            return (called, null);
        }
        catch (Exception ex)
        {
            return (called, ex);
        }
    }

    [Fact]
    public async Task NoValidators_CallsNext()
    {
        var (called, error) = await Run(new SampleRequest(null, null, null));
        Assert.True(called);
        Assert.Null(error);
    }

    [Fact]
    public async Task ValidRequest_CallsNext()
    {
        var (called, error) = await Run(new SampleRequest(" 20152691234567890 ", "Class 6", 10), new StudentValidator(), new BodyValidator());
        Assert.True(called);
        Assert.Null(error);
    }

    [Fact]
    public async Task InvalidStudentId_Throws400WithInvalidStudentIdCode_AndSkipsHandler()
    {
        var (called, error) = await Run(new SampleRequest("123", "Class 6", 10), new StudentValidator(), new BodyValidator());

        Assert.False(called);
        var ex = Assert.IsType<RequestValidationException>(error);
        Assert.Equal(ErrorCodes.InvalidStudentId, ex.Code);
        Assert.Equal(StudentIdRules.Message, ex.Message);
        Assert.Equal([StudentIdRules.Message], ex.Errors["X-Student-Id"]);
    }

    [Fact]
    public async Task InvalidBodyFields_ThrowValidationFailed_WithCamelCaseFieldErrors()
    {
        var (called, error) = await Run(new SampleRequest(null, "", 0), new BodyValidator());

        Assert.False(called);
        var ex = Assert.IsType<RequestValidationException>(error);
        Assert.Equal(ErrorCodes.ValidationFailed, ex.Code);
        Assert.Equal("One or more fields are invalid.", ex.Message);
        Assert.Equal(["Name is required."], ex.Errors["name"]);
        Assert.Equal(["Total seats must be between 1 and 10000."], ex.Errors["totalSeats"]);
    }
}
