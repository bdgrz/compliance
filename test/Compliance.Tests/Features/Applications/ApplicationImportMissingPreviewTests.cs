using System.Text.Json;
using Bdgrz.Compliance.Features.Applications;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationImportMissingPreviewTests
{
    [Theory]
    [InlineData("partial", 0)]
    [InlineData("declared_complete", 1)]
    public async Task ShouldProposeOmissionGivenSourceCoverage(string coverage, int expected)
    {
        // Arrange
        var fixture = await CreateAsync(1, coverage);

        // Act
        var result = await fixture.ReadAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value.Items.Count);
        Assert.Null(result.Value.NextCursor);
        Assert.All(result.Value.Items, row =>
        {
            Assert.Equal("missing_from_source", row.MatchState);
            Assert.Equal(fixture.Batch.Id, row.BatchId);
            Assert.Equal(fixture.OriginalBatch.Id, row.LastObservedBatchId);
            Assert.Equal(fixture.Targets[0].Id, row.ApplicationId);
            Assert.Contains("missing_source_retirement_unavailable", row.AcceptanceBlockers);
            Assert.Contains("retirement_impact_unavailable", row.AcceptanceBlockers);
            Assert.Equal("Observed 0", row.Name);
            Assert.Equal(1, row.ExpectedApplicationRevision);
            Assert.Equal(1, row.CurrentApplicationRevision);
            Assert.NotEqual(Uuid.Empty, row.SourceClaimId);
        });
        Assert.All(fixture.Targets, target => Assert.False(target.IsRetired));
        Assert.Empty(new AggregateScenario<ApplicationImportLedger>(fixture.Ledger).PendingEvents);
    }

    [Fact]
    public async Task ShouldPageAllOmissionsGivenBoundedPreview()
    {
        // Arrange
        var fixture = await CreateAsync(3);

        // Act
        var first = await fixture.ReadAsync(limit: 2);
        Assert.True(first.IsSuccess);
        var second = await fixture.ReadAsync(limit: 2, cursor: Assert.IsType<string>(first.Value.NextCursor));

        // Assert
        Assert.True(second.IsSuccess);
        Assert.Equal(["app-0", "app-1", "app-2"], first.Value.Items.Concat(second.Value.Items).Select(row => row.SourceRecordId));
        Assert.Null(second.Value.NextCursor);
        Assert.Equal(3, first.Value.Items.Concat(second.Value.Items).Select(row => row.SourceClaimId).Distinct().Count());
    }

    [Theory]
    [InlineData("revised")]
    [InlineData("retired")]
    [InlineData("missing")]
    public async Task ShouldBlockRetirementGivenChangedClaimedTarget(string state)
    {
        // Arrange
        var fixture = await CreateAsync();
        var target = fixture.Targets[0];
        target.ResolveImportVisibility(fixture.Ledger);
        if (state == "revised")
            Assert.Null(target.Revise(1, "Changed", "Purpose", null, Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow));
        if (state == "retired")
            Assert.Null(target.Retire(1, DateTimeOffset.UtcNow, "Retired", null, Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow));
        if (state == "missing")
            fixture.Reader.Replace(new DeclaredApplication(fixture.Tenant, target.Id));

        // Act
        var result = await fixture.ReadAsync();

        // Assert
        Assert.True(result.IsSuccess);
        var row = Assert.Single(result.Value.Items);
        Assert.Equal("missing_from_source", row.MatchState);
        Assert.Contains("source_claim_target_changed", row.AcceptanceBlockers);
    }

    [Fact]
    public async Task ShouldRejectOmissionsGivenUndurableCommit()
    {
        // Arrange
        var fixture = await CreateAsync(persistCommit: false);

        // Act
        var result = await fixture.ReadAsync();

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public async Task ShouldRejectMissingPreviewGivenInvalidLimit(int limit)
    {
        // Arrange
        var fixture = await CreateAsync();

        // Act
        var result = await fixture.ReadAsync(limit);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Theory]
    [InlineData("tenant", RequestErrorKind.Validation)]
    [InlineData("batch", RequestErrorKind.Validation)]
    [InlineData("offset", RequestErrorKind.Validation)]
    [InlineData("bounds", RequestErrorKind.Validation)]
    [InlineData("format", RequestErrorKind.Validation)]
    [InlineData("source", RequestErrorKind.Conflict)]
    [InlineData("revision", RequestErrorKind.Conflict)]
    public async Task ShouldRejectContinuationGivenInvalidMissingCursor(string difference, RequestErrorKind expected)
    {
        // Arrange
        var fixture = await CreateAsync(3);
        var first = await fixture.ReadAsync(1);
        Assert.True(first.IsSuccess);
        var parts = Assert.IsType<string>(first.Value.NextCursor).Split(':');
        if (difference == "tenant")
            parts[1] = Uuid.CreateVersion4().ToString();
        if (difference == "batch")
            parts[2] = Uuid.CreateVersion4().ToString();
        if (difference == "offset")
            parts[5] = "0";
        if (difference == "bounds")
            parts[5] = "3";
        if (difference == "format")
            parts[4] = "-1";
        if (difference == "source")
            parts[4] = "0";
        if (difference == "revision")
            parts[3] = "2";

        // Act
        var result = await fixture.ReadAsync(1, string.Join(':', parts));

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(expected, error.Kind);
        Assert.Equal(expected == RequestErrorKind.Conflict, error.IsTransient);
    }

    [Fact]
    public async Task ShouldRejectContinuationGivenAnotherBatchAdvancesSource()
    {
        // Arrange
        var fixture = await CreateAsync(3);
        var first = await fixture.ReadAsync(1);
        Assert.True(first.IsSuccess);
        fixture.Reader.Replace(Advanced(fixture));

        // Act
        var result = await fixture.ReadAsync(1, Assert.IsType<string>(first.Value.NextCursor));

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
    }

    [Fact]
    public async Task ShouldRejectProposalsGivenSourceChangesDuringTargetRead()
    {
        // Arrange
        var fixture = await CreateAsync();
        var advanced = Advanced(fixture);
        fixture.Reader.BeforeRead = source =>
        {
            if (source is DeclaredApplication)
                fixture.Reader.Replace(advanced);
        };

        // Act
        var result = await fixture.ReadAsync();

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
    }

    [Theory]
    [InlineData("present", 0)]
    [InlineData("case", 1)]
    [InlineData("source", 0)]
    [InlineData("namespace", 0)]
    [InlineData("tenant", 0)]
    public async Task ShouldScopeOmissionsGivenExactSourceIdentity(string difference, int expected)
    {
        // Arrange
        var fixture = await CreateAsync();
        var tenant = difference == "tenant" ? Uuid.CreateVersion4() : fixture.Tenant;
        var batch = Stage(tenant, "declared_complete", [new(difference == "case" ? "APP-0" : "app-0", "", "Purpose", null)],
            difference == "source" ? "other" : "manual", difference == "namespace" ? "other" : "applications");
        await AddBatchAsync(fixture, batch);

        // Act
        var result = await fixture.Handler.HandleAsync(new RequestContext<PreviewMissingApplicationImportRows>(
            new PreviewMissingApplicationImportRows(tenant, batch.Id), new()), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value.Items.Count);
        Assert.All(result.Value.Items, row => Assert.Equal("app-0", row.SourceRecordId));
    }

    [Fact]
    public async Task ShouldPreserveClaimIdentityGivenDifferentCompleteBatch()
    {
        // Arrange
        var fixture = await CreateAsync();
        var first = await fixture.ReadAsync();
        Assert.True(first.IsSuccess);
        var batch = Stage(fixture.Tenant, "declared_complete", [new("another", "Another", "Purpose", null)]);
        await AddBatchAsync(fixture, batch);

        // Act
        var result = await fixture.Handler.HandleAsync(new RequestContext<PreviewMissingApplicationImportRows>(
            new PreviewMissingApplicationImportRows(fixture.Tenant, batch.Id), new()), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(Assert.Single(first.Value.Items).SourceClaimId, Assert.Single(result.Value.Items).SourceClaimId);
        Assert.Equal(batch.Id, result.Value.Items[0].BatchId);
        Assert.Equal(fixture.OriginalBatch.Id, result.Value.Items[0].LastObservedBatchId);
    }

    [Fact]
    public async Task ShouldBlockProposalGivenCanceledCompleteBatch()
    {
        // Arrange
        var fixture = await CreateAsync();
        Assert.Null(fixture.Ledger.Cancel(fixture.Batch, 1, "Canceled", Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow));
        await ProjectAsync(fixture, Assert.Single(new AggregateScenario<ApplicationImportLedger>(fixture.Ledger).PendingEvents));

        // Act
        var result = await fixture.ReadAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains("batch_canceled", Assert.Single(result.Value.Items).AcceptanceBlockers);
        Assert.False(fixture.Targets[0].IsRetired);
    }

    [Theory]
    [InlineData(0, RequestErrorKind.Validation)]
    [InlineData(2, RequestErrorKind.Conflict)]
    public async Task ShouldRequireFreshBatchGivenMinimumRevision(long revision, RequestErrorKind expected)
    {
        // Arrange
        var fixture = await CreateAsync();

        // Act
        var result = await fixture.Handler.HandleAsync(new RequestContext<PreviewMissingApplicationImportRows>(
            new PreviewMissingApplicationImportRows(fixture.Tenant, fixture.Batch.Id, MinimumRevision: revision), new()), CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(expected, error.Kind);
        Assert.Equal(expected == RequestErrorKind.Conflict, error.IsTransient);
    }

    [Fact]
    public async Task ShouldIdentifyProposalGivenSnakeCaseWireContract()
    {
        // Arrange
        var fixture = await CreateAsync();
        var result = await fixture.ReadAsync();
        Assert.True(result.IsSuccess);

        // Act
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(Assert.Single(result.Value.Items),
            ComplianceCoreJsonContext.Default.ApplicationImportMissingRow));

        // Assert
        Assert.True(json.RootElement.TryGetProperty("source_claim_id", out _));
        Assert.True(json.RootElement.TryGetProperty("last_observed_batch_id", out _));
        Assert.True(json.RootElement.TryGetProperty("current_application_revision", out _));
        Assert.False(json.RootElement.TryGetProperty("row_id", out _));
        Assert.Equal("missing_from_source", json.RootElement.GetProperty("match_state").GetString());
    }

    [Fact]
    public async Task ShouldOrderOmissionsGivenExactCaseAndColonInSourceIds()
    {
        // Arrange
        var fixture = await CreateAsync(sourceIds: ["Z:3", "a:2", "A:1", "é"]);

        // Act
        var first = await fixture.ReadAsync(2);
        Assert.True(first.IsSuccess);
        var second = await fixture.ReadAsync(2, Assert.IsType<string>(first.Value.NextCursor));

        // Assert
        Assert.True(second.IsSuccess);
        Assert.Equal(["A:1", "Z:3", "a:2", "é"], first.Value.Items.Concat(second.Value.Items).Select(row => row.SourceRecordId));
        Assert.Null(second.Value.NextCursor);
    }

    static ApplicationImportLedger Advanced(Fixture fixture)
    {
        var batch = Stage(fixture.Tenant, "partial", [new("other", "Other", "Purpose", null)]);
        Assert.Null(fixture.Ledger.Cancel(batch, 1, "Other canceled", Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow));
        var saved = new ApplicationImportLedger(fixture.Tenant, "manual", "applications");
        _ = new AggregateScenario<ApplicationImportLedger>(saved).Given(
            [.. fixture.History, .. new AggregateScenario<ApplicationImportLedger>(fixture.Ledger).PendingEvents]);
        return saved;
    }

    static async Task AddBatchAsync(Fixture fixture, ImportBatch batch)
    {
        fixture.Reader.Replace(batch);
        await ProjectAsync(fixture, new ApplicationImportStaged(Uuid.Parse(batch.Stream.Realm, null), batch.Id,
            Uuid.CreateVersion4(), batch.SourceKey!, batch.SourceNamespace!, batch.Coverage!, batch.ContentDigest!,
            batch.GetRows(), batch.SubmitterMemberId, batch.SubmitterDisplay!, DateTimeOffset.UtcNow));
    }

    static async Task ProjectAsync(Fixture fixture, DomainEvent ev)
    {
        var identity = new CheckpointIdentity("ApplicationImportDirectoryV1", EventStreamPattern.ForPattern((ev is ApplicationImportStaged staged ? staged.TenantId : fixture.Tenant).ToString(), "application_imports"));
        await using var projection = await fixture.Directory.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start));
        await fixture.Directory.ApplyAsync(ev);
        await projection.CommitAsync(ProjectionCheckpoint.Start);
    }

    static ImportBatch Stage(Uuid tenant, string coverage, IReadOnlyList<ApplicationImportInputRow> rows,
        string sourceKey = "manual", string sourceNamespace = "applications")
    {
        var request = new StageApplicationImport(tenant, Uuid.CreateVersion4(), sourceKey, sourceNamespace, coverage, rows);
        var batch = new ImportBatch(tenant, ImportBatch.BatchIdFor(request));
        Assert.True(batch.Stage(request, Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow).IsSuccess);
        var saved = new ImportBatch(tenant, batch.Id);
        _ = new AggregateScenario<ImportBatch>(saved).Given(new AggregateScenario<ImportBatch>(batch).PendingEvents.ToArray());
        return saved;
    }

    static async Task<Fixture> CreateAsync(int count = 1, string coverage = "declared_complete", bool persistCommit = true, IReadOnlyList<string>? sourceIds = null)
    {
        count = sourceIds?.Count ?? count;
        var tenant = Uuid.CreateVersion4();
        var actor = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var original = Stage(tenant, "partial", Enumerable.Range(0, count).Select(i =>
            new ApplicationImportInputRow(sourceIds?[i] ?? $"app-{i}", $"Observed {i}", "Purpose", "Owner")).ToArray());
        var plan = new ApplicationImportLedger(tenant, "manual", "applications");
        foreach (var row in original.GetRows())
            Assert.Null(plan.Correlate(original, new CorrelateApplicationImportRow(tenant, original.Id, row.RowId,
                plan.GetRevision(original), "create_new", null, null, "New source identity"), null, actor, "Lead", now));
        Assert.True(plan.BeginAcceptance(original, plan.GetRevision(original), new Dictionary<Uuid, DeclaredApplication>(), actor, "Lead", now).IsSuccess);
        var ledger = new ApplicationImportLedger(tenant, "manual", "applications");
        var history = new AggregateScenario<ApplicationImportLedger>(plan).PendingEvents.ToList();
        _ = new AggregateScenario<ApplicationImportLedger>(ledger).Given(history.ToArray());
        var targets = new List<DeclaredApplication>();
        foreach (var row in ledger.GetFrozenPlan(original.Id)!.Rows)
        {
            var target = new DeclaredApplication(tenant, row.ApplicationId);
            Assert.True(target.RecordPendingImportEffect(ledger, original, row.RowId, now).IsSuccess);
            var saved = new DeclaredApplication(tenant, row.ApplicationId);
            _ = new AggregateScenario<DeclaredApplication>(saved).Given(new AggregateScenario<DeclaredApplication>(target).PendingEvents.ToArray());
            targets.Add(saved);
        }
        Assert.True(ledger.Commit(original, ledger.GetRevision(original), targets.ToDictionary(target => target.Id), now).IsSuccess);
        if (persistCommit)
        {
            history.AddRange(new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents);
            ledger = new ApplicationImportLedger(tenant, "manual", "applications");
            _ = new AggregateScenario<ApplicationImportLedger>(ledger).Given(history.ToArray());
        }
        var batch = Stage(tenant, coverage, [new("present", "Present", "Purpose", null)]);
        var directory = new FitzApplicationImportDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity("ApplicationImportDirectoryV1", EventStreamPattern.ForPattern(tenant.ToString(), "application_imports"));
        await using (var projection = await directory.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new ApplicationImportStaged(tenant, batch.Id, Uuid.CreateVersion4(), "manual", "applications", coverage,
                batch.ContentDigest!, batch.GetRows(), batch.SubmitterMemberId, batch.SubmitterDisplay!, now));
            await projection.CommitAsync(ProjectionCheckpoint.Start);
        }
        var reader = new SourceReader([batch, original, ledger, .. targets]);
        var handler = new PreviewMissingApplicationImportRowsHandler(new ApplicationImportReadConsistency(directory, reader), reader);
        return new Fixture(tenant, batch, original, ledger, targets, history, reader, directory, handler);
    }

    sealed record Fixture(Uuid Tenant, ImportBatch Batch, ImportBatch OriginalBatch, ApplicationImportLedger Ledger,
        IReadOnlyList<DeclaredApplication> Targets, IReadOnlyList<DomainEvent> History, SourceReader Reader,
        FitzApplicationImportDirectory Directory, PreviewMissingApplicationImportRowsHandler Handler)
    {
        public ValueTask<Result<Page<ApplicationImportMissingRow>>> ReadAsync(int? limit = null, string? cursor = null) =>
            Handler.HandleAsync(new RequestContext<PreviewMissingApplicationImportRows>(
                new PreviewMissingApplicationImportRows(Tenant, Batch.Id, limit, cursor), new()), CancellationToken.None);
    }

    sealed class SourceReader(params Aggregate[] sources) : IAggregateReader
    {
        readonly Dictionary<EventStreamAddress, Aggregate> _sources = sources.ToDictionary(source => source.Stream);
        public Action<Aggregate>? BeforeRead { get; set; }
        public void Replace(Aggregate source) => _sources[source.Stream] = source;
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
            where TAggregate : Aggregate
        {
            BeforeRead?.Invoke(aggregate);
            return ValueTask.FromResult((TAggregate)_sources.GetValueOrDefault(aggregate.Stream, aggregate));
        }
    }
}
