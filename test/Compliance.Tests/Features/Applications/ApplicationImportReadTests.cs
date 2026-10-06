using Bdgrz.Compliance.Features.Applications;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationImportReadTests
{
    [Fact]
    public async Task ShouldReportLagAndRecoverGivenCancellationOnSourceLedger()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var request = new StageApplicationImport(tenantId, Uuid.CreateVersion4(), "manual",
            "applications", "partial", [new ApplicationImportInputRow("app-1", "Payroll", "Pay staff", null)]);
        var source = new ImportBatch(tenantId, ImportBatch.BatchIdFor(request));
        Assert.True(source.Stage(request, actorId, "Lead", now).IsSuccess);
        var staged = Assert.IsType<ApplicationImportStaged>(
            Assert.Single(new AggregateScenario<ImportBatch>(source).PendingEvents));
        var ledger = new ApplicationImportLedger(tenantId, request.SourceKey, request.SourceNamespace);
        Assert.Null(ledger.Cancel(source, 1, "Superseded", actorId, "Lead", now));
        var canceled = Assert.IsType<ApplicationImportCanceled>(
            Assert.Single(new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents));
        var directory = new FitzApplicationImportDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity("ApplicationImportDirectoryV1",
            EventStreamPattern.ForPattern(tenantId.ToString(), "application_imports"));
        await using (var projection = await directory.BeginAsync(
            new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(staged);
            await projection.CommitAsync(ProjectionCheckpoint.Start);
        }
        var consistency = new ApplicationImportReadConsistency(directory, new SourceReader(source, ledger));

        // Act
        var lagging = await consistency.GetFreshAsync(tenantId, source.Id, null, CancellationToken.None);
        await using (var projection = await directory.BeginAsync(
            new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(canceled);
            await projection.CommitAsync(ProjectionCheckpoint.Start);
        }
        var recovered = await consistency.GetFreshAsync(tenantId, source.Id, 2, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(lagging.Error).Kind);
        Assert.True(Assert.IsType<RequestError>(lagging.Error).IsTransient);
        Assert.True(recovered.IsSuccess);
        Assert.Equal(2, recovered.Value.Revision);
        Assert.Equal("canceled", recovered.Value.State);
    }

    [Theory]
    [InlineData(1, "projection")]
    [InlineData(2, "source")]
    public async Task ShouldReportLagGivenStagedSourceWithoutProjectedRows(
        long minimumRevision, string laggingLayer)
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var request = new StageApplicationImport(tenantId, Uuid.CreateVersion4(),
            "source", "primary", "partial",
            [new ApplicationImportInputRow("record", "Payroll", "Purpose", null)]);
        var batchId = ImportBatch.BatchIdFor(request);
        var source = new ImportBatch(tenantId, batchId);
        Assert.True(source.Stage(request, Uuid.CreateVersion4(), "Manager",
            DateTimeOffset.UtcNow).IsSuccess);
        var directory = new FitzApplicationImportDirectory(new InMemoryKvClient());
        var consistency = new ApplicationImportReadConsistency(directory, new SourceReader(source));

        // Act
        var batch = await consistency.GetFreshAsync(tenantId, batchId, minimumRevision,
            CancellationToken.None);
        var rows = await new ListApplicationImportRowsHandler(directory, consistency)
            .HandleAsync(new RequestContext<ListApplicationImportRows>(
                new ListApplicationImportRows(tenantId, batchId,
                    MinimumRevision: minimumRevision), new()), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(batch.Error).Kind);
        Assert.True(Assert.IsType<RequestError>(batch.Error).IsTransient);
        Assert.Contains(laggingLayer, Assert.IsType<RequestError>(batch.Error).Message,
            StringComparison.Ordinal);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(rows.Error).Kind);
        Assert.True(Assert.IsType<RequestError>(rows.Error).IsTransient);
    }

    [Fact]
    public async Task ShouldHideUnknownBatchGivenUncreatedSource()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var batchId = Uuid.CreateVersion4();
        var directory = new FitzApplicationImportDirectory(new InMemoryKvClient());
        var consistency = new ApplicationImportReadConsistency(directory,
            new SourceReader(new ImportBatch(tenantId, batchId)));

        // Act
        var result = await consistency.GetFreshAsync(tenantId, batchId, null,
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(result.Error).Kind);
    }

    sealed class SourceReader(params Aggregate[] sources) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult((TAggregate)(sources.SingleOrDefault(source =>
                source.Stream == aggregate.Stream) ?? aggregate));
    }
}
