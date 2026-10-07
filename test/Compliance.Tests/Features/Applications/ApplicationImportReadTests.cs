using Bdgrz.Compliance.Features.Applications;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationImportReadTests
{
    [Theory]
    [InlineData("cancel", "canceled")]
    [InlineData("correlate", "preview_ready")]
    [InlineData("accept", "accepting")]
    public async Task ShouldReportLagAndRecoverGivenTransitionOnSourceLedger(string transition, string expectedState)
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
        if (transition == "cancel")
            Assert.Null(ledger.Cancel(source, 1, "Superseded", actorId, "Lead", now));
        else
        {
            Assert.Null(ledger.Correlate(source, new CorrelateApplicationImportRow(tenantId,
                source.Id, staged.Rows[0].RowId, 1, "create_new", null, null, "Reviewed identity"),
                null, actorId, "Lead", now));
            if (transition == "accept")
                Assert.True(ledger.BeginAcceptance(source, 2, new Dictionary<Uuid, DeclaredApplication>(), actorId, "Lead", now).IsSuccess);
        }
        var lifecycle = new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents.ToArray();
        var revision = ledger.GetRevision(source);
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
            foreach (var ev in lifecycle)
                await directory.ApplyAsync(ev);
            await projection.CommitAsync(ProjectionCheckpoint.Start);
        }
        var recovered = await consistency.GetFreshAsync(tenantId, source.Id, revision, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(lagging.Error).Kind);
        Assert.True(Assert.IsType<RequestError>(lagging.Error).IsTransient);
        Assert.True(recovered.IsSuccess);
        Assert.Equal(revision, recovered.Value.Revision);
        Assert.Equal(expectedState, recovered.Value.State);
    }

    [Fact]
    public async Task ShouldShowAttributedChoiceGivenFreshPreviewWithoutAcceptedClaims()
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
        var ledger = new ApplicationImportLedger(tenantId, "manual", "applications");
        Assert.Null(ledger.Correlate(source, new CorrelateApplicationImportRow(tenantId,
            source.Id, staged.Rows[0].RowId, 1, "create_new", null, null, "Reviewed identity"),
            null, actorId, "Lead", now));
        var correlated = Assert.Single(new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents);
        var directory = new FitzApplicationImportDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity("ApplicationImportDirectoryV1",
            EventStreamPattern.ForPattern(tenantId.ToString(), "application_imports"));
        await using (var projection = await directory.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(staged);
            await directory.ApplyAsync(correlated);
            await projection.CommitAsync(ProjectionCheckpoint.Start);
        }
        var reader = new SourceReader(source, ledger);
        var consistency = new ApplicationImportReadConsistency(directory, reader);
        var preview = new PreviewApplicationImportHandler(directory, consistency, reader);

        // Act
        var result = await preview.HandleAsync(new RequestContext<PreviewApplicationImport>(
            new PreviewApplicationImport(tenantId, source.Id, MinimumRevision: 2), new()), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var row = Assert.Single(result.Value.Items);
        var choice = Assert.IsType<ApplicationImportCorrelationView>(row.Correlation);
        Assert.Equal("create_new", choice.Decision);
        Assert.Equal(actorId, choice.ActorMemberId);
        Assert.Equal("Reviewed identity", choice.Reason);
        Assert.Equal(2, choice.Revision);
        Assert.Equal("new", row.MatchState);
        Assert.Empty(row.AcceptanceBlockers);
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
