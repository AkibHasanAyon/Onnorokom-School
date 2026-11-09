namespace SchoolAdmission.IntegrationTests.Infrastructure;

/// <summary>
/// Every concurrency test class goes in this collection, so bursts (~200 connections each) run one at a time.
/// Otherwise parallel bursts can exceed PostgreSQL's max_connections=300 (53300 too many clients).
/// </summary>
[CollectionDefinition(Name)]
public sealed class ConcurrencyCollection
{
    public const string Name = "Concurrency";
}
