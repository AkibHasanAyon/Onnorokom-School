using MediatR;
using SchoolAdmission.Application.Abstractions.Messaging;
using SchoolAdmission.Application.Abstractions.Persistence;

namespace SchoolAdmission.Application.Behaviors;

/// <summary>
/// Commands only: BEGIN (READ COMMITTED) → handler → SaveChanges → COMMIT.
/// Any exception disposes the transaction without committing, i.e. rolls back and releases row locks.
/// </summary>
public sealed class TransactionBehavior<TRequest, TResponse>(IUnitOfWork unitOfWork)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not ICommandMarker) return await next(cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var response = await next(cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return response;
    }
}
