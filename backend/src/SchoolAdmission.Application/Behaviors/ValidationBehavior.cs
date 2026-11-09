using FluentValidation;
using MediatR;
using SchoolAdmission.Application.Common.Exceptions;

namespace SchoolAdmission.Application.Behaviors;

/// <summary>Runs every FluentValidation validator for the request; any failure → 400 before the handler runs.</summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var all = validators as IValidator<TRequest>[] ?? validators.ToArray();
        if (all.Length == 0) return await next(cancellationToken);

        var context = new ValidationContext<TRequest>(request);
        var failures = new List<FluentValidation.Results.ValidationFailure>();
        foreach (var validator in all) // sequential: validators may share scoped services
        {
            var result = await validator.ValidateAsync(context, cancellationToken);
            failures.AddRange(result.Errors.Where(f => f is not null));
        }

        if (failures.Count > 0) throw new RequestValidationException(failures);
        return await next(cancellationToken);
    }
}
