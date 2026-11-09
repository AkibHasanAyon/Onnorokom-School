using SchoolAdmission.Application.Abstractions.Messaging;
using SchoolAdmission.Application.Abstractions.Persistence;
using SchoolAdmission.Application.Behaviors;

namespace SchoolAdmission.UnitTests.Application;

public sealed class TransactionBehaviorTests
{
    private sealed record SampleCommand : ICommand<string>;
    private sealed record SampleQuery : IQuery<string>;

    private sealed class RecordingUnitOfWork : IUnitOfWork
    {
        public List<string> Calls { get; } = [];

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct)
        {
            Calls.Add("begin");
            return Task.FromResult<IUnitOfWorkTransaction>(new Tx(Calls));
        }

        public Task SaveChangesAsync(CancellationToken ct)
        {
            Calls.Add("save");
            return Task.CompletedTask;
        }

        private sealed class Tx(List<string> calls) : IUnitOfWorkTransaction
        {
            public Task CommitAsync(CancellationToken ct) { calls.Add("commit"); return Task.CompletedTask; }
            public ValueTask DisposeAsync() { calls.Add("dispose"); return ValueTask.CompletedTask; }
        }
    }

    [Fact]
    public async Task Command_RunsHandlerInsideTransaction_ThenSavesAndCommits()
    {
        var uow = new RecordingUnitOfWork();
        var behavior = new TransactionBehavior<SampleCommand, string>(uow);

        var result = await behavior.Handle(new SampleCommand(), _ => { uow.Calls.Add("handler"); return Task.FromResult("done"); }, CancellationToken.None);

        Assert.Equal("done", result);
        Assert.Equal(["begin", "handler", "save", "commit", "dispose"], uow.Calls);
    }

    [Fact]
    public async Task Command_WhenHandlerThrows_DisposesWithoutSavingOrCommitting()
    {
        var uow = new RecordingUnitOfWork();
        var behavior = new TransactionBehavior<SampleCommand, string>(uow);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.Handle(new SampleCommand(), _ => throw new InvalidOperationException("boom"), CancellationToken.None));

        Assert.Equal(["begin", "dispose"], uow.Calls); // dispose without commit = rollback
    }

    [Fact]
    public async Task Query_RunsWithoutTransaction()
    {
        var uow = new RecordingUnitOfWork();
        var behavior = new TransactionBehavior<SampleQuery, string>(uow);

        var result = await behavior.Handle(new SampleQuery(), _ => Task.FromResult("read"), CancellationToken.None);

        Assert.Equal("read", result);
        Assert.Empty(uow.Calls);
    }
}
