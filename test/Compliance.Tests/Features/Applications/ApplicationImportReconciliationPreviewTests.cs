using Bdgrz.Compliance.Features.Applications;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationImportReconciliationPreviewTests
{
    [Theory]
    [InlineData("Observed name", "unchanged")]
    [InlineData(" Observed name ", "unchanged")]
    [InlineData("Changed name", "changed")]
    public async Task ShouldClassifyObservationGivenCommittedSourceClaim(string name, string state)
    {
        // Arrange
        var (tenant, ledger, target, _) = Committed();
        var batch = Stage(tenant, "app-1", name);
        var fixture = await PreviewAsync(batch, ledger, target);

        // Act
        var result = await fixture.Handler.HandleAsync(new RequestContext<PreviewApplicationImport>(
            new PreviewApplicationImport(tenant, batch.Id), new()), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var row = Assert.Single(result.Value.Items);
        Assert.Equal(state, row.MatchState);
        Assert.Equal(target.Id, Assert.Single(row.CandidateApplicationIds));
        Assert.Equal(state == "changed" ? ["name"] : Array.Empty<string>(), row.ChangedFields);
        Assert.Empty(row.AcceptanceBlockers);
        Assert.Null(row.Correlation);
        Assert.Empty(new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents);
        Assert.Equal(1, target.Revision);
    }

    [Theory]
    [InlineData("revised")]
    [InlineData("retired")]
    [InlineData("missing")]
    public async Task ShouldBlockTargetGivenCommittedClaimWithChangedGovernance(string state)
    {
        // Arrange
        var (tenant, ledger, target, _) = Committed();
        target.ResolveImportVisibility(ledger);
        if (state == "revised")
            Assert.Null(target.Revise(1, "Governed revision", "Purpose", null, Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow));
        if (state == "retired")
            Assert.Null(target.Retire(1, DateTimeOffset.UtcNow, "Retire", null, Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow));
        var batch = Stage(tenant, "app-1", "Observed name");
        var fixture = await PreviewAsync(batch, ledger, state == "missing" ? new DeclaredApplication(tenant, target.Id) : target);

        // Act
        var result = await fixture.Handler.HandleAsync(new RequestContext<PreviewApplicationImport>(
            new PreviewApplicationImport(tenant, batch.Id), new()), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var row = Assert.Single(result.Value.Items);
        Assert.Equal("conflicting", row.MatchState);
        Assert.Contains("source_claim_target_changed", row.AcceptanceBlockers);
    }

    [Theory]
    [InlineData("partial", false)]
    [InlineData("declared_complete", true)]
    public async Task ShouldBlockMissingRetirementGivenCompleteSourceCoverage(string coverage, bool blocked)
    {
        // Arrange
        var (tenant, ledger, target, _) = Committed();
        var batch = Stage(tenant, "app-2", "New observation", coverage);
        var fixture = await PreviewAsync(batch, ledger, target);

        // Act
        var result = await fixture.Handler.HandleAsync(new RequestContext<PreviewApplicationImport>(
            new PreviewApplicationImport(tenant, batch.Id), new()), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var row = Assert.Single(result.Value.Items);
        Assert.Equal("unmatched", row.MatchState);
        Assert.Contains("correlation_required", row.AcceptanceBlockers);
        Assert.Equal(blocked, row.AcceptanceBlockers.Contains("missing_source_retirement_unavailable"));
        Assert.False(target.IsRetired);
    }

    [Fact]
    public async Task ShouldRejectPreviewGivenUndurableSourceCommit()
    {
        // Arrange
        var (tenant, ledger, target, _) = Committed(persistCommit: false);
        var batch = Stage(tenant, "app-1", "Observed name");
        var fixture = await PreviewAsync(batch, ledger, target);

        // Act
        var result = await fixture.Handler.HandleAsync(new RequestContext<PreviewApplicationImport>(
            new PreviewApplicationImport(tenant, batch.Id), new()), CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRejectContinuationGivenOtherBatchChangesSourceLedger(bool legacyRowsCursor)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var ledger = new ApplicationImportLedger(tenant, "manual", "applications");
        var batch = StageRows(tenant, [new("app-1", "One", "Purpose", null), new("app-2", "Two", "Purpose", null)]);
        var fixture = await PreviewAsync(batch, ledger);
        var first = await fixture.Handler.HandleAsync(new RequestContext<PreviewApplicationImport>(
            new PreviewApplicationImport(tenant, batch.Id, Limit: 1), new()), CancellationToken.None);
        Assert.True(first.IsSuccess);
        var cursor = legacyRowsCursor
            ? (await fixture.Directory.ListRowsAsync(tenant, batch.Id, 1, null)).NextCursor : first.Value.NextCursor;
        Assert.NotNull(cursor);
        fixture.Reader.Replace(Advanced(ledger, []));

        // Act
        var result = await fixture.Handler.HandleAsync(new RequestContext<PreviewApplicationImport>(
            new PreviewApplicationImport(tenant, batch.Id, Limit: 1, Cursor: cursor), new()), CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
    }

    [Fact]
    public async Task ShouldRejectPreviewGivenSourceLedgerChangesDuringTargetRead()
    {
        // Arrange
        var history = new List<DomainEvent>();
        var (tenant, ledger, target, _) = Committed(ledgerHistory: history);
        var batch = Stage(tenant, "app-1", "Observed name");
        var fixture = await PreviewAsync(batch, ledger, target);
        var advanced = Advanced(ledger, history);
        fixture.Reader.BeforeRead = source =>
        {
            if (source is DeclaredApplication)
                fixture.Reader.Replace(advanced);
        };

        // Act
        var result = await fixture.Handler.HandleAsync(new RequestContext<PreviewApplicationImport>(
            new PreviewApplicationImport(tenant, batch.Id), new()), CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
    }

    [Fact]
    public async Task ShouldUseReviewedRevisionGivenFreshCorrelationForCommittedClaim()
    {
        // Arrange
        var (tenant, ledger, target, _) = Committed();
        target.ResolveImportVisibility(ledger);
        Assert.Null(target.Revise(1, "Governed revision", "Purpose", null, Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow));
        var batch = Stage(tenant, "app-1", "Observed name");
        Assert.Null(ledger.Correlate(batch, new CorrelateApplicationImportRow(tenant, batch.Id,
            Assert.Single(batch.GetRows()).RowId, 1, "link_existing", target.Id, 2, "Reviewed current target"),
            target, Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow));
        var fixture = await PreviewAsync(batch, ledger, target);
        await ProjectAsync(fixture.Directory, tenant,
            Assert.Single(new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents));

        // Act
        var result = await fixture.Handler.HandleAsync(new RequestContext<PreviewApplicationImport>(
            new PreviewApplicationImport(tenant, batch.Id), new()), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var row = Assert.Single(result.Value.Items);
        Assert.Equal("unchanged", row.MatchState);
        Assert.Empty(row.ChangedFields);
        Assert.Empty(row.AcceptanceBlockers);
        Assert.Equal(2, Assert.IsType<ApplicationImportCorrelationView>(row.Correlation).ExpectedApplicationRevision);
        Assert.Equal("Reviewed current target", row.Correlation.Reason);
        Assert.Equal(2, target.Revision);
    }

    [Theory]
    [InlineData(false, "invalid")]
    [InlineData(true, "duplicate")]
    public async Task ShouldPreserveValidationGivenCommittedClaimForInvalidObservation(bool duplicated, string expected)
    {
        // Arrange
        var (tenant, ledger, target, _) = Committed();
        var input = new ApplicationImportInputRow("app-1", duplicated ? "Observed name" : "", "Purpose", null);
        var batch = StageRows(tenant, duplicated ? [input, input] : [input]);
        var fixture = await PreviewAsync(batch, ledger, target);

        // Act
        var result = await fixture.Handler.HandleAsync(new RequestContext<PreviewApplicationImport>(
            new PreviewApplicationImport(tenant, batch.Id), new()), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.All(result.Value.Items, row =>
        {
            Assert.Equal(expected, row.MatchState);
            Assert.Contains(duplicated ? "duplicate_source_record_id" : "name_required", row.AcceptanceBlockers);
        });
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("source")]
    [InlineData("namespace")]
    [InlineData("case")]
    public async Task ShouldKeepPreviewUnmatchedGivenDifferentSourceIdentity(string difference)
    {
        // Arrange
        var (tenant, ledger, target, _) = Committed();
        var batch = StageRows(difference == "tenant" ? Uuid.CreateVersion4() : tenant,
            [new(difference == "case" ? "APP-1" : "app-1", "Observed name", "Observed purpose", "Observed owner")],
            difference == "source" ? "other" : "manual", difference == "namespace" ? "other" : "applications");
        var fixture = await PreviewAsync(batch, ledger, target);

        // Act
        var result = await fixture.Handler.HandleAsync(new RequestContext<PreviewApplicationImport>(
            new PreviewApplicationImport(Uuid.Parse(batch.Stream.Realm, null), batch.Id), new()), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var row = Assert.Single(result.Value.Items);
        Assert.Equal("unmatched", row.MatchState);
        Assert.Empty(row.CandidateApplicationIds);
        Assert.Empty(row.ChangedFields);
        Assert.Contains("correlation_required", row.AcceptanceBlockers);
    }

    [Fact]
    public async Task ShouldCompareAllSourceFieldsGivenChangedObservation()
    {
        // Arrange
        var (tenant, ledger, target, _) = Committed();
        var batch = StageRows(tenant, [new("app-1", "Observed name", "Changed purpose", null)]);
        var fixture = await PreviewAsync(batch, ledger, target);

        // Act
        var result = await fixture.Handler.HandleAsync(new RequestContext<PreviewApplicationImport>(
            new PreviewApplicationImport(tenant, batch.Id), new()), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var row = Assert.Single(result.Value.Items);
        Assert.Equal("changed", row.MatchState);
        Assert.Equal(["purpose", "owner_reference"], row.ChangedFields);
        Assert.Equal(1, target.Revision);
    }

    [Theory]
    [InlineData("tenant", RequestErrorKind.Validation)]
    [InlineData("batch", RequestErrorKind.Validation)]
    [InlineData("format", RequestErrorKind.Validation)]
    [InlineData("source", RequestErrorKind.Conflict)]
    public async Task ShouldRejectContinuationGivenInvalidSourceFence(string difference, RequestErrorKind expected)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var ledger = new ApplicationImportLedger(tenant, "manual", "applications");
        var batch = StageRows(tenant, [new("app-1", "One", "Purpose", null), new("app-2", "Two", "Purpose", null)]);
        var fixture = await PreviewAsync(batch, ledger);
        var first = await fixture.Handler.HandleAsync(new RequestContext<PreviewApplicationImport>(
            new PreviewApplicationImport(tenant, batch.Id, Limit: 1), new()), CancellationToken.None);
        Assert.True(first.IsSuccess);
        var parts = Assert.IsType<string>(first.Value.NextCursor).Split(':', 6);
        if (difference == "tenant")
            parts[1] = Uuid.CreateVersion4().ToString();
        if (difference == "batch")
            parts[2] = Uuid.CreateVersion4().ToString();
        if (difference == "format")
            parts[4] = "-1";
        if (difference == "source")
            parts[4] = "1";

        // Act
        var result = await fixture.Handler.HandleAsync(new RequestContext<PreviewApplicationImport>(
            new PreviewApplicationImport(tenant, batch.Id, Limit: 1, Cursor: string.Join(':', parts)), new()), CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(expected, error.Kind);
        Assert.Equal(expected == RequestErrorKind.Conflict, error.IsTransient);
    }

    [Fact]
    public async Task ShouldBlockPreviewGivenCanceledBatch()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var ledger = new ApplicationImportLedger(tenant, "manual", "applications");
        var batch = Stage(tenant, "app-1", "Observed name");
        var fixture = await PreviewAsync(batch, ledger);
        Assert.Null(ledger.Cancel(batch, 1, "Cancel", Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow));
        await ProjectAsync(fixture.Directory, tenant,
            Assert.Single(new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents));

        // Act
        var result = await fixture.Handler.HandleAsync(new RequestContext<PreviewApplicationImport>(
            new PreviewApplicationImport(tenant, batch.Id), new()), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains("batch_canceled", Assert.Single(result.Value.Items).AcceptanceBlockers);
        Assert.Equal(2, ledger.GetRevision(batch));
    }

    [Fact]
    public async Task ShouldRejectPreviewGivenProjectedObservationDiffersFromStagedSource()
    {
        // Arrange
        var (tenant, ledger, target, _) = Committed();
        var batch = Stage(tenant, "app-1", "Observed name");
        var fixture = await PreviewAsync(batch, ledger, target);
        var directory = new ChangedRowDirectory(fixture.Directory);
        var handler = new PreviewApplicationImportHandler(directory,
            new ApplicationImportReadConsistency(directory, fixture.Reader), fixture.Reader);

        // Act
        var result = await handler.HandleAsync(new RequestContext<PreviewApplicationImport>(
            new PreviewApplicationImport(tenant, batch.Id), new()), CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
    }

    sealed class ChangedRowDirectory(IApplicationImportDirectoryReader inner) : IApplicationImportDirectoryReader
    {
        public ValueTask<ApplicationImportView?> GetAsync(Uuid tenantId, Uuid batchId, CancellationToken ct = default) =>
            inner.GetAsync(tenantId, batchId, ct);

        public async ValueTask<Page<ApplicationImportRowView>> ListRowsAsync(Uuid tenantId, Uuid batchId,
            int limit, string? cursor, CancellationToken ct = default)
        {
            var page = await inner.ListRowsAsync(tenantId, batchId, limit, cursor, ct);
            return new Page<ApplicationImportRowView>(page.Items.Select(row => row with { Name = "Changed projection" }).ToArray(), page.NextCursor);
        }
    }

    static ApplicationImportLedger Advanced(ApplicationImportLedger ledger, IReadOnlyList<DomainEvent> history)
    {
        var tenant = Uuid.Parse(ledger.Stream.Realm, null);
        var unrelated = Stage(tenant, "unrelated", "Unrelated");
        Assert.Null(ledger.Cancel(unrelated, 1, "Cancel unrelated", Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow));
        var saved = new ApplicationImportLedger(tenant, "manual", "applications");
        _ = new AggregateScenario<ApplicationImportLedger>(saved).Given(
            [.. history, .. new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents]);
        return saved;
    }

    static ImportBatch StageRows(Uuid tenant, IReadOnlyList<ApplicationImportInputRow> rows,
        string sourceKey = "manual", string sourceNamespace = "applications")
    {
        var request = new StageApplicationImport(tenant, Uuid.CreateVersion4(), sourceKey, sourceNamespace, "partial", rows);
        var batch = new ImportBatch(tenant, ImportBatch.BatchIdFor(request));
        Assert.True(batch.Stage(request, Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow).IsSuccess);
        var saved = new ImportBatch(tenant, batch.Id);
        _ = new AggregateScenario<ImportBatch>(saved).Given(new AggregateScenario<ImportBatch>(batch).PendingEvents.ToArray());
        return saved;
    }

    static async Task ProjectAsync(FitzApplicationImportDirectory directory, Uuid tenant, DomainEvent ev)
    {
        var identity = new CheckpointIdentity("ApplicationImportDirectoryV1",
            EventStreamPattern.ForPattern(tenant.ToString(), "application_imports"));
        await using var projection = await directory.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start));
        await directory.ApplyAsync(ev);
        await projection.CommitAsync(ProjectionCheckpoint.Start);
    }

    static async Task<(PreviewApplicationImportHandler Handler, FitzApplicationImportDirectory Directory, SourceReader Reader)> PreviewAsync(
        ImportBatch batch, ApplicationImportLedger ledger, params Aggregate[] targets)
    {
        var directory = new FitzApplicationImportDirectory(new InMemoryKvClient());
        var tenant = Uuid.Parse(batch.Stream.Realm, null);
        var identity = new CheckpointIdentity("ApplicationImportDirectoryV1",
            EventStreamPattern.ForPattern(tenant.ToString(), "application_imports"));
        var ev = new ApplicationImportStaged(tenant, batch.Id, Uuid.CreateVersion4(), batch.SourceKey!,
            batch.SourceNamespace!, batch.Coverage!, batch.ContentDigest!, batch.GetRows(),
            batch.SubmitterMemberId, batch.SubmitterDisplay!, DateTimeOffset.UtcNow);
        await using (var projection = await directory.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(ev);
            await projection.CommitAsync(ProjectionCheckpoint.Start);
        }
        var reader = new SourceReader([batch, ledger, .. targets]);
        return (new PreviewApplicationImportHandler(directory, new ApplicationImportReadConsistency(directory, reader), reader), directory, reader);
    }

    sealed class SourceReader(params Aggregate[] sources) : IAggregateReader
    {
        readonly Dictionary<EventStreamAddress, Aggregate> _sources = sources.ToDictionary(source => source.Stream);
        public Action<Aggregate>? BeforeRead { get; set; }
        public void Replace(Aggregate source) => _sources[source.Stream] = source;

        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            BeforeRead?.Invoke(aggregate);
            return ValueTask.FromResult((TAggregate)_sources.GetValueOrDefault(aggregate.Stream, aggregate));
        }
    }

    static ImportBatch Stage(Uuid tenant, string sourceRecordId, string name, string coverage = "partial")
    {
        var request = new StageApplicationImport(tenant, Uuid.CreateVersion4(), "manual", "applications", coverage,
            [new(sourceRecordId, name, "Observed purpose", "Observed owner")]);
        var batch = new ImportBatch(tenant, ImportBatch.BatchIdFor(request));
        Assert.True(batch.Stage(request, Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow).IsSuccess);
        var saved = new ImportBatch(tenant, batch.Id);
        _ = new AggregateScenario<ImportBatch>(saved).Given(new AggregateScenario<ImportBatch>(batch).PendingEvents.ToArray());
        return saved;
    }

    static (Uuid Tenant, ApplicationImportLedger Ledger, DeclaredApplication Target, ImportBatch Batch) Committed(
        bool persistCommit = true, List<DomainEvent>? ledgerHistory = null, List<DomainEvent>? targetHistory = null)
    {
        var tenant = Uuid.CreateVersion4();
        var actor = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var batch = Stage(tenant, "app-1", "Observed name");
        var target = new DeclaredApplication(tenant, Uuid.CreateVersion4());
        Assert.True(target.Declare("Governed name", "Governed purpose", null, actor, "Lead", now).IsSuccess);
        var plan = new ApplicationImportLedger(tenant, "manual", "applications");
        var row = Assert.Single(batch.GetRows());
        Assert.Null(plan.Correlate(batch, new CorrelateApplicationImportRow(tenant, batch.Id, row.RowId,
            1, "link_existing", target.Id, 1, "Reviewed identity"), target, actor, "Lead", now));
        var targets = new Dictionary<Uuid, DeclaredApplication> { [target.Id] = target };
        Assert.True(plan.BeginAcceptance(batch, 2, targets, actor, "Lead", now).IsSuccess);
        var ledger = new ApplicationImportLedger(tenant, "manual", "applications");
        var planEvents = new AggregateScenario<ApplicationImportLedger>(plan).PendingEvents.ToArray();
        _ = new AggregateScenario<ApplicationImportLedger>(ledger).Given(planEvents);
        Assert.True(target.RecordPendingImportEffect(ledger, batch, row.RowId, now).IsSuccess);
        targetHistory?.AddRange(new AggregateScenario<DeclaredApplication>(target).PendingEvents);
        var savedTarget = new DeclaredApplication(tenant, target.Id);
        _ = new AggregateScenario<DeclaredApplication>(savedTarget).Given(
            new AggregateScenario<DeclaredApplication>(target).PendingEvents.ToArray());
        Assert.True(ledger.Commit(batch, 5, new Dictionary<Uuid, DeclaredApplication> { [target.Id] = savedTarget }, now).IsSuccess);
        if (!persistCommit)
            return (tenant, ledger, savedTarget, batch);
        ledgerHistory?.AddRange([.. planEvents, .. new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents]);
        var savedLedger = new ApplicationImportLedger(tenant, "manual", "applications");
        _ = new AggregateScenario<ApplicationImportLedger>(savedLedger).Given(
            [.. planEvents, .. new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents]);
        return (tenant, savedLedger, savedTarget, batch);
    }
}
