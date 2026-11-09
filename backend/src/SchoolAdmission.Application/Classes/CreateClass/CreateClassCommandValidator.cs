using FluentValidation;
using SchoolAdmission.Domain.Entities;

namespace SchoolAdmission.Application.Classes.CreateClass;

public sealed class CreateClassCommandValidator : AbstractValidator<CreateClassCommand>
{
    public CreateClassCommandValidator()
    {
        RuleFor(x => x.Name)
            .Must(n => !string.IsNullOrWhiteSpace(n))
                .WithMessage("Name is required.")
            .Must(n => n is null || n.Trim().Length <= SchoolClass.MaxNameLength)
                .WithMessage($"Name must be at most {SchoolClass.MaxNameLength} characters.");

        RuleFor(x => x.TotalSeats)
            .NotNull()
                .WithMessage("Total seats is required.")
            .InclusiveBetween(SchoolClass.MinSeats, SchoolClass.MaxSeats)
                .WithMessage($"Total seats must be between {SchoolClass.MinSeats} and {SchoolClass.MaxSeats}.");
    }
}
