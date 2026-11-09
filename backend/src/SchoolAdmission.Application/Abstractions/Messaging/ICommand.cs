using MediatR;

namespace SchoolAdmission.Application.Abstractions.Messaging;

/// <summary>Marks a request that changes state; <c>TransactionBehavior</c> wraps it in a DB transaction.</summary>
public interface ICommandMarker;

/// <summary>A state-changing use case. Runs inside a READ COMMITTED transaction and uses repositories.</summary>
public interface ICommand<out TResponse> : IRequest<TResponse>, ICommandMarker;
