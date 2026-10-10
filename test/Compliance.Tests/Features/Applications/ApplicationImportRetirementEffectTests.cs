using Bdgrz.Compliance.Features.Applications;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationImportRetirementEffectTests
{
    [Fact]
    public async Task ShouldProjectRetirementOnlyAfterDurableSourceCommitGivenCompleteCurrentImpact()
    {
        // Arrange
        var (tenant, history, targetHistory, source, target, firstBatch) = SeedCommittedClaim();
        target.ResolveImportVisibility(source);
        var client = new InMemoryKvClient();
        var directory = new FitzApplicationDirectory(client);
        var query = new FitzApplicationDirectory(client);
        var identity = new CheckpointIdentity("ApplicationDirectoryV2",
            EventStreamPattern.ForPattern(tenant.ToString()));
        var other = new DeclaredApplication(tenant, Uuid.CreateVersion4());
        Assert.True(other.Declare("Another application", "Another purpose", null, Uuid.CreateVersion4(),
            "Lead", DateTimeOffset.UtcNow).IsSuccess);
        await using (var initial = await directory.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(Assert.IsType<ApplicationDeclared>(targetHistory[0]));
            await directory.ApplyAsync(Assert.IsType<ApplicationDeclared>(
                Assert.Single(new AggregateScenario<DeclaredApplication>(other).PendingEvents)));
            await initial.CommitAsync(ProjectionCheckpoint.Start);
        }
        var beforeCommitPage = await query.ListAsync(tenant, 1, null);
        Assert.NotNull(beforeCommitPage.NextCursor);
        var beforeCommit = await query.GetAsync(tenant, target.Id);
        Assert.Equal("active", beforeCommit?.Lifecycle);
        Assert.Null(await query.GetRevisionAsync(tenant, target.Id, 2));
        var batch = Stage(tenant, "app-2", "New source record", "declared_complete",
            ApplicationImportLedger.MaximumCommitEffectRows - 1);
        var correlatingActor = Uuid.CreateVersion4();
        foreach (var sourceRow in batch.GetRows())
            Assert.Null(source.Correlate(batch, new CorrelateApplicationImportRow(tenant, batch.Id,
                sourceRow.RowId, source.GetRevision(batch), "create_new", null, null,
                "Reviewed new identity"), null, correlatingActor, "Lead", DateTimeOffset.UtcNow));
        history.AddRange(new AggregateScenario<ApplicationImportLedger>(source).PendingEvents);
        var correlated = LoadLedger(tenant, history);
        var proposalAt = DateTimeOffset.UtcNow;
        Assert.True(correlated.FreezeRetirementProposal(batch, correlated.GetRevision(batch),
            correlated.CommittedStreamPosition,
            new Dictionary<Uuid, DeclaredApplication> { [target.Id] = target }, Uuid.CreateVersion4(),
            "Lead", "Source no longer reports this application", proposalAt).IsSuccess);
        history.AddRange(new AggregateScenario<ApplicationImportLedger>(correlated).PendingEvents);
        var proposed = LoadLedger(tenant, history);
        var proposal = Assert.IsType<ApplicationImportRetirementProposal>(proposed.GetRetirementProposal(batch));
        var impact = CompleteImpact(tenant, target);
        var impacts = new Dictionary<Uuid, ApplicationChangePreview> { [target.Id] = impact };
        Assert.True(proposed.BeginAcceptance(batch, proposal.Revision,
            new Dictionary<Uuid, DeclaredApplication> { [target.Id] = target },
            Uuid.CreateVersion4(), "Lead", proposalAt.AddMinutes(1), impacts).IsSuccess);
        history.AddRange(new AggregateScenario<ApplicationImportLedger>(proposed).PendingEvents);
        var accepting = LoadLedger(tenant, history);
        var plan = Assert.IsType<ApplicationImportFrozenPlan>(accepting.GetFrozenPlan(batch.Id));
        var targets = new Dictionary<Uuid, DeclaredApplication>();
        var targetHistories = new Dictionary<Uuid, List<DomainEvent>> { [target.Id] = targetHistory };
        foreach (var row in plan.Rows)
        {
            var rowTarget = row.ApplicationId == target.Id
                ? LoadApplication(tenant, target.Id, targetHistory, Settled(firstBatch.Id))
                : new DeclaredApplication(tenant, row.ApplicationId);
            Assert.True(rowTarget.RecordPendingImportEffect(accepting, batch, row.RowId,
                proposalAt.AddMinutes(2)).IsSuccess);
            var pending = new AggregateScenario<DeclaredApplication>(rowTarget).PendingEvents;
            if (!targetHistories.TryGetValue(rowTarget.Id, out var rowHistory))
                targetHistories.Add(rowTarget.Id, rowHistory = []);
            rowHistory.AddRange(pending);
            targets.Add(rowTarget.Id, LoadApplication(tenant, rowTarget.Id, rowHistory,
                rowTarget.Id == target.Id ? Settled(firstBatch.Id) : Settled()));
        }
        var beforeMarker = await query.GetAsync(tenant, target.Id);
        Assert.Equal("active", beforeMarker?.Lifecycle);
        Assert.Equal(1, beforeMarker?.Revision);
        Assert.Null(await query.GetAsync(tenant,
            plan.Rows.First(row => row.Decision == "create_new").ApplicationId));

        // Act
        var committedAt = proposalAt.AddMinutes(3);
        var commit = accepting.Commit(batch, accepting.GetRevision(batch), targets, committedAt, impacts);
        var commitEvents = new AggregateScenario<ApplicationImportLedger>(accepting).PendingEvents;
        history.AddRange(commitEvents);
        var committed = LoadLedger(tenant, history);
        var marker = Assert.IsType<ApplicationImportCommitted>(committed.GetCommit(batch.Id));
        var frozenPlan = Assert.IsType<ApplicationImportFrozenPlan>(committed.GetFrozenPlan(batch.Id));

        var rollbackCheckpoint = await directory.LoadCheckpointAsync(tenant);
        await using (var rollback = await directory.BeginAsync(new ProjectionBatchContext(identity, rollbackCheckpoint)))
            await directory.ApplyCommittedImportAsync(marker, frozenPlan);
        var afterRollback = await query.GetAsync(tenant, target.Id);
        Assert.Equal("active", afterRollback?.Lifecycle);
        Assert.Equal(1, afterRollback?.Revision);
        Assert.Null(afterRollback?.Retirement);
        Assert.Null(await query.GetRevisionAsync(tenant, target.Id, 2));

        var projectionCheckpoint = await directory.LoadCheckpointAsync(tenant);
        await using (var projection = await directory.BeginAsync(new ProjectionBatchContext(identity, projectionCheckpoint)))
        {
            var reader = new SourceMapReader([committed, .. targets.Values]);
            await new ApplicationDirectoryProjector(directory, reader).HandleAsync(marker,
                new ProjectionContext(identity), CancellationToken.None);
            await projection.CommitAsync(projectionCheckpoint);
        }
        var retired = LoadApplication(tenant, target.Id, targetHistory, Settled(firstBatch.Id));
        retired.ResolveImportVisibility(committed);
        var projectedRetired = await query.GetAsync(tenant, target.Id);
        var retirementRevision = await query.GetRevisionAsync(tenant, target.Id, 2);

        // Assert
        Assert.True(commit.IsSuccess);
        Assert.Equal(ApplicationImportLedger.MaximumCommitEffectRows, marker.Effects.Count);
        Assert.Equal(committedAt, committed.GetCommit(batch.Id)!.CommittedAt);
        Assert.True(retired.IsRetired);
        Assert.Equal(2, retired.Revision);
        Assert.Equal(ApplicationImportLedger.MaximumCommitEffectRows - 1, committed.GetSourceClaims().Count);
        Assert.Contains(committed.GetSourceClaims(), claim => claim.Observation.SourceRecordId == "app-2");
        Assert.DoesNotContain(committed.GetSourceClaims(), claim => claim.Observation.SourceRecordId == "app-1");
        Assert.All(committed.GetCommit(batch.Id)!.Effects, proof => Assert.NotEqual(Uuid.Empty, proof.EventId));
        Assert.Equal("retired", projectedRetired?.Lifecycle);
        Assert.Equal(2, projectedRetired?.Revision);
        Assert.Equal(committedAt, projectedRetired?.Retirement?.EffectiveAt);
        Assert.Equal(proposal.Start.Reason, projectedRetired?.Retirement?.Reason);
        Assert.Equal(plan.Start.ApproverMemberId, projectedRetired?.LastChangedByMemberId);
        Assert.Equal(committedAt, projectedRetired?.LastChangedAt);
        Assert.Equal("retired", retirementRevision?.Lifecycle);
        Assert.Equal(plan.Start.ApproverMemberId, retirementRevision?.LastChangedByMemberId);
        await Assert.ThrowsAsync<ApplicationImportVisibilityChangedException>(() => query.ListAsync(tenant, 1,
            beforeCommitPage.NextCursor).AsTask());
    }

    sealed record ProjectionContext(CheckpointIdentity Identity) : IProjectorContext
    {
        public bool IsRebuild => Identity.RebuildId is not null;
    }

    sealed class SourceMapReader(Aggregate[] sources) : IAggregateReader
    {
        public ValueTask<T> HydrateAsync<T>(T aggregate, CancellationToken ct = default) where T : Aggregate =>
            ValueTask.FromResult(sources.FirstOrDefault(source => source.Stream == aggregate.Stream) is { } source
                ? (T)source : aggregate);
    }

    static (Uuid Tenant, List<DomainEvent> SourceHistory, List<DomainEvent> TargetHistory,
        ApplicationImportLedger Source, DeclaredApplication Target, ImportBatch Batch) SeedCommittedClaim()
    {
        var tenant = Uuid.CreateVersion4();
        var actor = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var batch = Stage(tenant, "app-1", "Existing source record");
        var target = new DeclaredApplication(tenant, Uuid.CreateVersion4());
        Assert.True(target.Declare("Governed application", "Existing purpose", null, actor,
            "Lead", now).IsSuccess);
        var source = new ApplicationImportLedger(tenant, "manual", "applications");
        var row = Assert.Single(batch.GetRows());
        Assert.Null(source.Correlate(batch, new CorrelateApplicationImportRow(tenant, batch.Id,
            row.RowId, 1, "link_existing", target.Id, 1, "Reviewed existing identity"), target, actor, "Lead", now));
        Assert.True(source.BeginAcceptance(batch, 2,
            new Dictionary<Uuid, DeclaredApplication> { [target.Id] = target }, actor, "Lead", now).IsSuccess);
        var sourceHistory = new AggregateScenario<ApplicationImportLedger>(source).PendingEvents.ToList();
        var accepting = LoadLedger(tenant, sourceHistory);
        Assert.True(target.RecordPendingImportEffect(accepting, batch, row.RowId, now).IsSuccess);
        var targetHistory = new AggregateScenario<DeclaredApplication>(target).PendingEvents.ToList();
        var persistedTarget = LoadApplication(tenant, target.Id, targetHistory, Settled());
        Assert.True(accepting.Commit(batch, accepting.GetRevision(batch),
            new Dictionary<Uuid, DeclaredApplication> { [target.Id] = persistedTarget }, now).IsSuccess);
        sourceHistory.AddRange(new AggregateScenario<ApplicationImportLedger>(accepting).PendingEvents);
        var committed = LoadLedger(tenant, sourceHistory);
        return (tenant, sourceHistory, targetHistory, committed, persistedTarget, batch);
    }

    static ApplicationChangePreview CompleteImpact(Uuid tenant, DeclaredApplication target) =>
        new(tenant, target.Id, target.Revision, "retire", [], [], [], true)
        {
            ImpactDigest = new string('a', 64),
        };

    static ApplicationImportLedger LoadLedger(Uuid tenant, IReadOnlyList<DomainEvent> history)
    {
        var ledger = new ApplicationImportLedger(tenant, "manual", "applications");
        _ = new AggregateScenario<ApplicationImportLedger>(ledger).Given(history.ToArray());
        return ledger;
    }

    static DeclaredApplication LoadApplication(Uuid tenant, Uuid id, IReadOnlyList<DomainEvent> history,
        IReadOnlySet<Uuid> settled)
    {
        var application = new DeclaredApplication(tenant, id, settled);
        _ = new AggregateScenario<DeclaredApplication>(application).Given(history.ToArray());
        return application;
    }

    static HashSet<Uuid> Settled(params Uuid[] batchIds)
    {
        var settled = new HashSet<Uuid>();
        settled.UnionWith(batchIds);
        return settled;
    }

    static ImportBatch Stage(Uuid tenant, string sourceRecordId, string name,
        string coverage = "partial", int rowCount = 1)
    {
        var request = new StageApplicationImport(tenant, Uuid.CreateVersion4(), "manual",
            "applications", coverage, Enumerable.Range(1, rowCount).Select(index => new ApplicationImportInputRow(
                index == 1 ? sourceRecordId : $"{sourceRecordId}-{index}",
                index == 1 ? name : $"{name} {index}", "Observed purpose", null)).ToArray());
        var staged = new ImportBatch(tenant, ImportBatch.BatchIdFor(request));
        Assert.True(staged.Stage(request, Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow).IsSuccess);
        var saved = new ImportBatch(tenant, staged.Id);
        _ = new AggregateScenario<ImportBatch>(saved).Given(
            new AggregateScenario<ImportBatch>(staged).PendingEvents.ToArray());
        return saved;
    }
}
