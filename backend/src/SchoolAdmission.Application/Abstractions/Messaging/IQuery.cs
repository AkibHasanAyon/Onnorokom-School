using MediatR;

namespace SchoolAdmission.Application.Abstractions.Messaging;

/// <summary>A read-only use case. No transaction; uses read stores (no-tracking projections).</summary>
public interface IQuery<out TResponse> : IRequest<TResponse>;
