using Bdgrz.Compliance.Features.Applications;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationImportPreviewCorrelationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldDistinguishInvalidCursorGivenLegacyCursorAfterLifecycleChange(bool validCursor)
    {
        // Arrange
        var fixture = await StagedAsync();
        var oldPage = await fixture.Directory.ListRowsAsync(fixture.TenantId, fixture.Batch.Id, 1, null);
        Assert.NotNull(oldPage.NextCursor);
        Assert.Null(fixture.Ledger.Correlate(fixture.Batch, new CorrelateApplicationImportRow(
            fixture.TenantId, fixture.Batch.Id, fixture.Rows[1].RowId, 1, "create_new", null, null,
            "Reviewed identity"), null, fixture.ActorId, "Lead", fixture.Now));
        await fixture.ApplyAsync(Assert.Single(new AggregateScenario<ApplicationImportLedger>(fixture.Ledger).PendingEvents));
        var cursor = validCursor ? oldPage.NextCursor : "not a cursor";

        // Act
        var result = await fixture.Handler.HandleAsync(new RequestContext<PreviewApplicationImport>(
            new PreviewApplicationImport(fixture.TenantId, fixture.Batch.Id, Limit: 1, Cursor: cursor), new()), CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(validCursor ? RequestErrorKind.Conflict : RequestErrorKind.Validation, error.Kind);
        Assert.Equal(validCursor, error.IsTransient);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldShareCursorGivenRowAndPreviewPaging(bool rowsFirst)
    {
        // Arrange
        var fixture = await StagedAsync();
        var reader = new SourceReader(fixture.Batch, fixture.Ledger, fixture.Application);
        var rows = new ListApplicationImportRowsHandler(fixture.Directory,
            new ApplicationImportReadConsistency(fixture.Directory, reader));
        string? cursor;
        if (rowsFirst)
        {
            var first = await rows.HandleAsync(new RequestContext<ListApplicationImportRows>(
                new ListApplicationImportRows(fixture.TenantId, fixture.Batch.Id, Limit: 1), new()), CancellationToken.None);
            Assert.True(first.IsSuccess);
            cursor = first.Value.NextCursor;
        }
        else
        {
            var first = await fixture.Handler.HandleAsync(new RequestContext<PreviewApplicationImport>(
                new PreviewApplicationImport(fixture.TenantId, fixture.Batch.Id, Limit: 1), new()), CancellationToken.None);
            Assert.True(first.IsSuccess);
            cursor = first.Value.NextCursor;
        }
        Assert.NotNull(cursor);

        // Act
        var preview = rowsFirst ? await fixture.Handler.HandleAsync(new RequestContext<PreviewApplicationImport>(
            new PreviewApplicationImport(fixture.TenantId, fixture.Batch.Id, Limit: 1, Cursor: cursor), new()), CancellationToken.None) : default;
        var page = !rowsFirst ? await rows.HandleAsync(new RequestContext<ListApplicationImportRows>(
            new ListApplicationImportRows(fixture.TenantId, fixture.Batch.Id, Limit: 1, Cursor: cursor), new()), CancellationToken.None) : default;

        // Assert
        if (rowsFirst)
        {
            Assert.True(preview.IsSuccess);
            Assert.Equal(fixture.Rows[1].RowId, Assert.Single(preview.Value.Items).RowId);
        }
        else
        {
            Assert.True(page.IsSuccess);
            Assert.Equal(fixture.Rows[1].RowId, Assert.Single(page.Value.Items).RowId);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldKeepPagingConsistentGivenLifecycleChangeBetweenPages(bool changed)
    {
        // Arrange
        var fixture = await StagedAsync();
        var first = await fixture.Handler.HandleAsync(new RequestContext<PreviewApplicationImport>(
            new PreviewApplicationImport(fixture.TenantId, fixture.Batch.Id, Limit: 1), new()), CancellationToken.None);
        Assert.True(first.IsSuccess);
        Assert.NotNull(first.Value.NextCursor);
        if (changed)
        {
            Assert.Null(fixture.Ledger.Correlate(fixture.Batch, new CorrelateApplicationImportRow(
                fixture.TenantId, fixture.Batch.Id, fixture.Rows[1].RowId, 1, "create_new", null, null,
                "Reviewed identity"), null, fixture.ActorId, "Lead", fixture.Now));
            await fixture.ApplyAsync(Assert.Single(new AggregateScenario<ApplicationImportLedger>(fixture.Ledger).PendingEvents));
        }

        // Act
        var next = await fixture.Handler.HandleAsync(new RequestContext<PreviewApplicationImport>(
            new PreviewApplicationImport(fixture.TenantId, fixture.Batch.Id, Limit: 1,
                Cursor: first.Value.NextCursor), new()), CancellationToken.None);

        // Assert
        if (changed)
        {
            Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(next.Error).Kind);
            Assert.True(Assert.IsType<RequestError>(next.Error).IsTransient);
        }
        else
        {
            Assert.True(next.IsSuccess);
            Assert.Equal(fixture.Rows[1].RowId, Assert.Single(next.Value.Items).RowId);
            Assert.Null(next.Value.NextCursor);
        }
    }

    [Theory]
    [InlineData("unchanged")]
    [InlineData("revised")]
    [InlineData("retired")]
    public async Task ShouldRecheckLinkedTargetGivenPreviewAfterCorrelation(string targetState)
    {
        // Arrange
        var fixture = await StagedAsync();
        Assert.Null(fixture.Ledger.Correlate(fixture.Batch, new CorrelateApplicationImportRow(
            fixture.TenantId, fixture.Batch.Id, fixture.Rows[0].RowId, 1, "link_existing",
            fixture.Application.Id, 1, "Reviewed identity"), fixture.Application, fixture.ActorId, "Lead", fixture.Now));
        await fixture.ApplyAsync(Assert.Single(new AggregateScenario<ApplicationImportLedger>(fixture.Ledger).PendingEvents));
        if (targetState == "revised")
            Assert.Null(fixture.Application.Revise(1, "Changed manually", "Governed purpose", null,
                fixture.ActorId, "Lead", fixture.Now));
        if (targetState == "retired")
            Assert.Null(fixture.Application.Retire(1, fixture.Now, "Retired", null, fixture.ActorId, "Lead", fixture.Now));

        // Act
        var result = await fixture.Handler.HandleAsync(new RequestContext<PreviewApplicationImport>(
            new PreviewApplicationImport(fixture.TenantId, fixture.Batch.Id), new()), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var row = result.Value.Items[0];
        Assert.Equal(fixture.Application.Id, Assert.IsType<ApplicationImportCorrelationView>(row.Correlation).ApplicationId);
        Assert.Equal(targetState != "unchanged", row.AcceptanceBlockers.Contains("correlation_target_changed"));
        Assert.DoesNotContain("source_claims_unavailable", row.AcceptanceBlockers);
    }

    static async Task<Fixture> StagedAsync()
    {
        var tenantId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var request = new StageApplicationImport(tenantId, Uuid.CreateVersion4(), "manual", "applications", "partial",
            [new("app-1", "Payroll", "Pay staff", null), new("app-2", "CRM", "Manage customers", null)]);
        var batch = new ImportBatch(tenantId, ImportBatch.BatchIdFor(request));
        Assert.True(batch.Stage(request, actorId, "Lead", now).IsSuccess);
        var staged = Assert.IsType<ApplicationImportStaged>(Assert.Single(new AggregateScenario<ImportBatch>(batch).PendingEvents));
        var ledger = new ApplicationImportLedger(tenantId, "manual", "applications");
        var application = new DeclaredApplication(tenantId, Uuid.CreateVersion4());
        Assert.True(application.Declare("Manual application", "Governed purpose", null, actorId, "Lead", now).IsSuccess);
        var directory = new FitzApplicationImportDirectory(new InMemoryKvClient());
        var reader = new SourceReader(batch, ledger, application);
        var handler = new PreviewApplicationImportHandler(directory, new ApplicationImportReadConsistency(directory, reader), reader);
        var fixture = new Fixture(tenantId, actorId, now, batch, staged.Rows, ledger, application, directory, handler);
        await fixture.ApplyAsync(staged);
        return fixture;
    }

    sealed record Fixture(Uuid TenantId, Uuid ActorId, DateTimeOffset Now, ImportBatch Batch,
        IReadOnlyList<ApplicationImportStagedRow> Rows, ApplicationImportLedger Ledger,
        DeclaredApplication Application, FitzApplicationImportDirectory Directory, PreviewApplicationImportHandler Handler)
    {
        public async Task ApplyAsync(DomainEvent ev)
        {
            var identity = new CheckpointIdentity("ApplicationImportDirectoryV1",
                EventStreamPattern.ForPattern(TenantId.ToString(), "application_imports"));
            await using var projection = await Directory.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start));
            await Directory.ApplyAsync(ev);
            await projection.CommitAsync(ProjectionCheckpoint.Start);
        }
    }

    sealed class SourceReader(params Aggregate[] sources) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate => ValueTask.FromResult(
            (TAggregate)(sources.SingleOrDefault(source => source.Stream == aggregate.Stream) ?? aggregate));
    }
}
