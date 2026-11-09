using System.Data;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Npgsql;
using SchoolAdmission.Application.Abstractions.Persistence;
using SchoolAdmission.Application.Common.Exceptions;
using SchoolAdmission.Domain.Errors;

namespace SchoolAdmission.Infrastructure.Persistence;

internal sealed class UnitOfWork(AdmissionDbContext db, ILogger<UnitOfWork> logger) : IUnitOfWork
{
    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct) =>
        // READ COMMITTED + explicit row locks. Never SERIALIZABLE (40001 failures under bursts).
        new EfTransaction(await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct));

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && Translate(pg) is { } translated)
        {
            throw translated;
        }
    }

    /// <summary>Maps constraint violations to application errors (Database.md §6); null = not ours to translate.</summary>
    private Exception? Translate(PostgresException pg)
    {
        switch (pg.SqlState)
        {
            case PostgresErrorCodes.UniqueViolation when pg.ConstraintName == "ux_classes_normalized_name":
                return new ConflictException(ErrorCodes.ClassNameExists, "A class with this name already exists.");

            case PostgresErrorCodes.UniqueViolation when pg.ConstraintName == "ux_holds_class_student_active":
                return new ConflictException(ErrorCodes.ActiveHoldExists,
                    "This student already has an active application for this class.");

            case PostgresErrorCodes.CheckViolation:
            case PostgresErrorCodes.CharacterNotInRepertoire:   // 22021, e.g. a NUL character in text
            case PostgresErrorCodes.UntranslatableCharacter:    // 22P05
            case PostgresErrorCodes.StringDataRightTruncation:  // 22001, value longer than the column
                // The database rejected a value the validators let through: a validation rule is missing.
                // Still the client's input → 400, not 500.
                logger.LogError("Database rejected input ({SqlState} {Constraint}); a validation rule is missing",
                    pg.SqlState, pg.ConstraintName);
                return new RequestValidationException(
                    [new ValidationFailure(string.Empty, "One or more fields are invalid.") { ErrorCode = ErrorCodes.ValidationFailed }]);

            default:
                return null;
        }
    }

    private sealed class EfTransaction(IDbContextTransaction transaction) : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken ct) => transaction.CommitAsync(ct);
        public ValueTask DisposeAsync() => transaction.DisposeAsync(); // rolls back if not committed
    }
}
