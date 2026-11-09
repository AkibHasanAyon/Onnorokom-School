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
                .WithMessage($"Name must be at most {SchoolClass.MaxNameLength} characters.")
            // NUL can't be stored in PostgreSQL text (22021 → was a 500); other control characters
            // (BEL, ESC, line breaks inside the name...) make no sense in a class name shown to parents.
            .Must(n => n is null || !n.Trim().Any(char.IsControl))
                .WithMessage("Name must not contain control characters.");

        RuleFor(x => x.TotalSeats)
            .NotNull()
                .WithMessage("Total seats is required.")
            .InclusiveBetween(SchoolClass.MinSeats, SchoolClass.MaxSeats)
                .WithMessage($"Total seats must be between {SchoolClass.MinSeats} and {SchoolClass.MaxSeats}.");
    }
}
