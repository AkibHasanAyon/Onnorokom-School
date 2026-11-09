namespace SchoolAdmission.Application.Abstractions.Persistence;

/// <summary>Transaction boundary for commands. Implemented with EF Core in Infrastructure.</summary>
public interface IUnitOfWork
{
    /// <summary>Begins a READ COMMITTED transaction. Disposing it without <see cref="IUnitOfWorkTransaction.CommitAsync"/> rolls back.</summary>
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct);

    /// <summary>Saves tracked changes. Unique-constraint violations surface as <c>ConflictException</c>.</summary>
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct);
}
