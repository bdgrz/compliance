using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.AccessReviews;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Cntryl.Fitz.Testing;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationImportCommitTests
{
    [Fact]
    public void ShouldCommitWholePlanGivenDurableExactEffects()
    {
        // Arrange
        var (tenant, batch, planned, ledger, targets, now) = Prepare();

        // Act
        var result = ledger.Commit(batch, 7, targets, now);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("committed", ledger.GetState(batch));
        Assert.Equal(8, ledger.GetRevision(batch));
        Assert.Single(new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents);
        Assert.All(targets.Values, target => Assert.False(target.IsCreated));
        Assert.False(ledger.IsCommitDurable(batch.Id));
        var effect = targets.Values.First().GetPendingImportEffects().Single();
        Assert.False(ledger.IsEffectCommitted(effect));

        // Arrange
        var persisted = new ApplicationImportLedger(tenant, "manual", "applications");
        var history = new AggregateScenario<ApplicationImportLedger>(planned).PendingEvents.Concat(
            new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents).ToArray();

        // Act
        _ = new AggregateScenario<ApplicationImportLedger>(persisted).Given(history);

        // Assert
        Assert.True(persisted.IsCommitDurable(batch.Id));
        Assert.True(persisted.IsEffectCommitted(effect));
        Assert.Null(persisted.Correlate(batch, new CorrelateApplicationImportRow(tenant, batch.Id,
            effect.Row.RowId, 1, "create_new", null, null, "Reviewed"), null,
            Uuid.CreateVersion4(), "Retrying lead", now.AddDays(1)));
        Assert.False(persisted.IsEffectCommitted(effect with { Row = effect.Row with { Name = "Different" } }));
        Assert.True(persisted.Commit(batch, 7, targets, now.AddDays(1)).IsSuccess);
        Assert.True(persisted.Commit(batch, 8, targets, now.AddDays(1)).IsSuccess);
        Assert.Empty(new AggregateScenario<ApplicationImportLedger>(persisted).PendingEvents);
        Assert.Equal(now, persisted.GetCommit(batch.Id)!.CommittedAt);
        Assert.True(targets.Values.First().RecordPendingImportEffect(persisted, batch, effect.Row.RowId, now.AddDays(1)).IsSuccess);
        Assert.Same(effect, targets.Values.First().GetPendingImportEffect(batch.Id, effect.Row.RowId));
        Assert.Equal(now, effect.RecordedAt);
        Assert.Empty(new AggregateScenario<DeclaredApplication>(targets.Values.First()).PendingEvents);
        var missing = new DeclaredApplication(tenant, effect.ApplicationId);
        Assert.False(missing.RecordPendingImportEffect(persisted, batch, effect.Row.RowId, now).IsSuccess);
        Assert.Empty(new AggregateScenario<DeclaredApplication>(missing).PendingEvents);
    }
    [Theory]
    [InlineData("duplicate_event")]
    [InlineData("empty_event")]
    [InlineData("zero_version")]
    [InlineData("missing_row")]
    [InlineData("foreign_target")]
    [InlineData("wrong_digest")]
    public void ShouldRejectReplayGivenMalformedCommitProof(string problem)
    {
        // Arrange
        var (tenant, batch, planned, ledger, targets, now) = Prepare();
        Assert.True(ledger.Commit(batch, 7, targets, now).IsSuccess);
        var marker = ledger.GetCommit(batch.Id)!;
        var proofs = marker.Effects.ToArray();
        proofs[1] = problem switch
        {
            "duplicate_event" => proofs[1] with { EventId = proofs[0].EventId },
            "empty_event" => proofs[1] with { EventId = Uuid.Empty },
            "zero_version" => proofs[1] with { EventVersion = 0 },
            "foreign_target" => proofs[1] with { ApplicationId = Uuid.CreateVersion4() },
            _ => proofs[1],
        };
        marker = marker with
        {
            Effects = problem == "missing_row" ? [proofs[0]] : proofs,
            PlanSha256 = problem == "wrong_digest" ? new string('0', 64) : marker.PlanSha256,
        };
        var replay = new ApplicationImportLedger(tenant, "manual", "applications");
        _ = new AggregateScenario<ApplicationImportLedger>(replay).Given(
            new AggregateScenario<ApplicationImportLedger>(planned).PendingEvents.ToArray());

        // Act
        var act = () => new AggregateScenario<ApplicationImportLedger>(replay).Given(marker);

        // Assert
        Assert.Throws<InvalidOperationException>(act);
        Assert.Null(replay.GetCommit(batch.Id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldExposeNewApplicationGivenDurableMatchingCommit(bool padded)
    {
        // Arrange
        var (tenant, batch, planned, ledger, targets, now) = Prepare(padded: padded);
        var target = targets.Values.First();
        Assert.True(ledger.Commit(batch, 7, targets, now).IsSuccess);
        target.ResolveImportVisibility(ledger);
        Assert.False(target.IsCreated);
        var persisted = new ApplicationImportLedger(tenant, "manual", "applications");
        _ = new AggregateScenario<ApplicationImportLedger>(persisted).Given(
            new AggregateScenario<ApplicationImportLedger>(planned).PendingEvents.Concat(
                new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents).ToArray());

        // Act
        target.ResolveImportVisibility(persisted);

        // Assert
        Assert.True(target.IsCreated);
        Assert.Equal(1, target.Revision);
        Assert.False(target.IsRestricted);
        Assert.Null(target.CheckPendingImportChanges());
        var effect = target.GetPendingImportEffects().Single();
        Assert.True(target.Declare(effect.Row.Name, effect.Row.Purpose, effect.Row.OwnerReference,
            Uuid.CreateVersion4(), "Lead", now).IsSuccess);
        Assert.Empty(new AggregateScenario<DeclaredApplication>(target).PendingEvents);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldPreserveLaterGovernanceGivenImportedCreationReplay(bool retire)
    {
        // Arrange
        var (tenant, batch, planned, ledger, targets, now) = Prepare();
        Assert.True(ledger.Commit(batch, 7, targets, now).IsSuccess);
        var persisted = new ApplicationImportLedger(tenant, "manual", "applications");
        _ = new AggregateScenario<ApplicationImportLedger>(persisted).Given(
            new AggregateScenario<ApplicationImportLedger>(planned).PendingEvents.Concat(
                new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents).ToArray());
        var target = targets.Values.First();
        var effect = target.GetPendingImportEffects().Single();
        target.ResolveImportVisibility(persisted);
        var owner = Uuid.CreateVersion4();
        Assert.Null(target.Revise(1, "Governed name", "Governed purpose", null, owner, "Lead", now,
            accessOwnerPersonId: owner, isRestricted: true));
        if (retire)
            Assert.Null(target.Retire(2, now, "Retired", null, owner, "Lead", now));
        var replay = new DeclaredApplication(tenant, target.Id);
        _ = new AggregateScenario<DeclaredApplication>(replay).Given([effect,
            .. new AggregateScenario<DeclaredApplication>(target).PendingEvents]);
        Assert.False(replay.IsCreated);

        // Act
        replay.ResolveImportVisibility(persisted);

        // Assert
        Assert.True(replay.IsCreated);
        Assert.Equal(retire ? 3 : 2, replay.Revision);
        Assert.Equal(retire, replay.IsRetired);
        Assert.True(replay.IsRestricted);
        Assert.Equal(owner, replay.AccessOwnerPersonId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldProjectWholeBatchGivenCommittedInitialObservations(bool padded)
    {
        // Arrange
        var (tenant, batch, planned, ledger, targets, now) = Prepare(padded: padded);
        Assert.True(ledger.Commit(batch, 7, targets, now).IsSuccess);
        var marker = ledger.GetCommit(batch.Id)!;
        var plan = ledger.GetFrozenPlan(batch.Id)!;
        var persisted = new ApplicationImportLedger(tenant, "manual", "applications");
        _ = new AggregateScenario<ApplicationImportLedger>(persisted).Given(
            new AggregateScenario<ApplicationImportLedger>(planned).PendingEvents.Concat(
                new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents).ToArray());
        var reader = new SourceMapReader([persisted, .. targets.Values]);
        var client = new InMemoryKvClient();
        var directory = new FitzApplicationDirectory(client);
        var query = new FitzApplicationDirectory(client);
        var identity = new CheckpointIdentity("ApplicationDirectoryV2",
            EventStreamPattern.ForPattern(tenant.ToString()));

        // Act
        await using (var projection = await directory.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await new ApplicationDirectoryProjector(directory, reader).HandleAsync(marker,
                new ProjectionContext(identity), CancellationToken.None);
            Assert.Empty((await query.ListAsync(tenant, 200, null)).Items);
            await projection.CommitAsync(ProjectionCheckpoint.Start);
        }
        var page = await directory.ListAsync(tenant, 200, null);

        // Assert
        Assert.Equal(2, page.Items.Count);
        foreach (var row in plan.Rows)
        {
            var view = Assert.Single(page.Items, view => view.ApplicationId == row.ApplicationId);
            Assert.Equal(row.Name.Trim(), view.Name);
            Assert.Equal(row.Purpose.Trim(), view.Purpose);
            Assert.Equal(row.SourceRecordId, view.SourceIdentifier);
            Assert.Equal("import", view.SourceKind);
            Assert.Equal(plan.Start.ApproverMemberId, view.LastChangedByMemberId);
            Assert.Equal(1, view.Revision);
            Assert.NotNull(await directory.GetRevisionAsync(tenant, row.ApplicationId, 1));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRejectContinuationGivenImportVisibilityChanged(bool importedCursor)
    {
        // Arrange
        var (tenant, batch, _, ledger, targets, now) = Prepare();
        Assert.True(ledger.Commit(batch, 7, targets, now).IsSuccess);
        var directory = new FitzApplicationDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity("ApplicationDirectoryV2",
            EventStreamPattern.ForPattern(tenant.ToString()));
        await using (var projection = await directory.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            if (importedCursor)
                await directory.ApplyCommittedImportAsync(ledger.GetCommit(batch.Id)!, ledger.GetFrozenPlan(batch.Id)!);
            else
                foreach (var row in ledger.GetFrozenPlan(batch.Id)!.Rows)
                    await directory.ApplyAsync(new ApplicationDeclared(tenant, row.ApplicationId,
                        row.Name, row.Purpose, null, Uuid.CreateVersion4(), "Lead", now));
            await projection.CommitAsync(ProjectionCheckpoint.Start);
        }
        var page = await directory.ListAsync(tenant, 1, null);
        Assert.NotNull(page.NextCursor);
        var (_, nextBatch, _, nextLedger, nextTargets, nextTime) = Prepare(tenant);
        Assert.True(nextLedger.Commit(nextBatch, 7, nextTargets, nextTime).IsSuccess);
        await using (var projection = await directory.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyCommittedImportAsync(nextLedger.GetCommit(nextBatch.Id)!, nextLedger.GetFrozenPlan(nextBatch.Id)!);
            await projection.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var act = () => directory.ListAsync(tenant, 1, page.NextCursor).AsTask();

        // Assert
        await Assert.ThrowsAsync<ApplicationImportVisibilityChangedException>(act);
    }

    [Fact]
    public async Task ShouldPreserveGovernedInventoryGivenCommittedLinkedObservations()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var actor = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var target = new DeclaredApplication(tenant, Uuid.CreateVersion4());
        Assert.True(target.Declare("Governed", "Manual purpose", "Owner", actor, "Lead", now,
            classification: "confidential", accessOwnerPersonId: actor, isRestricted: true).IsSuccess);
        var declared = Assert.IsType<ApplicationDeclared>(Assert.Single(new AggregateScenario<DeclaredApplication>(target).PendingEvents));
        var request = new StageApplicationImport(tenant, Uuid.CreateVersion4(), "manual", "applications", "partial",
            [new("app-1", "Observed first", "Source first", null), new("app-2", "Observed second", "Source second", null)]);
        var staged = new ImportBatch(tenant, ImportBatch.BatchIdFor(request));
        Assert.True(staged.Stage(request, actor, "Contributor", now).IsSuccess);
        var batch = new ImportBatch(tenant, staged.Id);
        _ = new AggregateScenario<ImportBatch>(batch).Given(new AggregateScenario<ImportBatch>(staged).PendingEvents.ToArray());
        var planned = new ApplicationImportLedger(tenant, "manual", "applications");
        foreach (var row in batch.GetRows())
            Assert.Null(planned.Correlate(batch, new CorrelateApplicationImportRow(tenant, batch.Id, row.RowId,
                planned.GetRevision(batch), "link_existing", target.Id, 1, "Reviewed"), target, actor, "Lead", now));
        Assert.True(planned.BeginAcceptance(batch, 3, new Dictionary<Uuid, DeclaredApplication> { [target.Id] = target }, actor, "Lead", now).IsSuccess);
        var ledger = new ApplicationImportLedger(tenant, "manual", "applications");
        _ = new AggregateScenario<ApplicationImportLedger>(ledger).Given(new AggregateScenario<ApplicationImportLedger>(planned).PendingEvents.ToArray());
        foreach (var row in batch.GetRows())
            Assert.True(target.RecordPendingImportEffect(ledger, batch, row.RowId, now).IsSuccess);
        var saved = new DeclaredApplication(tenant, target.Id);
        _ = new AggregateScenario<DeclaredApplication>(saved).Given(new AggregateScenario<DeclaredApplication>(target).PendingEvents.ToArray());
        Assert.True(ledger.Commit(batch, 7, new Dictionary<Uuid, DeclaredApplication> { [saved.Id] = saved }, now).IsSuccess);
        var directory = new FitzApplicationDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity("ApplicationDirectoryV2", EventStreamPattern.ForPattern(tenant.ToString()));

        // Act
        await using (var projection = await directory.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(declared);
            await directory.ApplyCommittedImportAsync(ledger.GetCommit(batch.Id)!, ledger.GetFrozenPlan(batch.Id)!);
            await projection.CommitAsync(ProjectionCheckpoint.Start);
        }
        var view = await directory.GetAsync(tenant, target.Id);
        var history = await directory.ListRevisionsAsync(tenant, target.Id, 200, null);

        // Assert
        Assert.Equal("Governed", view!.Name);
        Assert.Equal("Manual purpose", view.Purpose);
        Assert.Equal("Owner", view.OwnerReference);
        Assert.Equal("confidential", view.Classification);
        Assert.True(view.IsRestricted);
        Assert.Equal(actor, view.AccessOwnerPersonId);
        Assert.Equal("manual", view.SourceKind);
        Assert.Equal(1, view.Revision);
        Assert.Single(history!.Items);
        Assert.Equal(2, ledger.GetCommit(batch.Id)!.Effects.Count);
    }

    [Fact]
    public async Task ShouldDiscardEveryNewRowGivenProjectionBatchAborts()
    {
        // Arrange
        var (tenant, batch, _, ledger, targets, now) = Prepare();
        Assert.True(ledger.Commit(batch, 7, targets, now).IsSuccess);
        var directory = new FitzApplicationDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity("ApplicationDirectoryV2", EventStreamPattern.ForPattern(tenant.ToString()));

        // Act
        await using (var projection = await directory.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
            await directory.ApplyCommittedImportAsync(ledger.GetCommit(batch.Id)!, ledger.GetFrozenPlan(batch.Id)!);
        var page = await directory.ListAsync(tenant, 200, null);

        // Assert
        Assert.Empty(page.Items);
        foreach (var target in targets.Values)
            Assert.Null(await directory.GetRevisionAsync(tenant, target.Id, 1));
    }

    [Fact]
    public async Task ShouldRejectCommitProjectionGivenForeignTenantTransaction()
    {
        // Arrange
        var (_, batch, _, ledger, targets, now) = Prepare();
        Assert.True(ledger.Commit(batch, 7, targets, now).IsSuccess);
        var directory = new FitzApplicationDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity("ApplicationDirectoryV2",
            EventStreamPattern.ForPattern(Uuid.CreateVersion4().ToString()));
        await using var projection = await directory.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start));

        // Act
        var act = () => directory.ApplyCommittedImportAsync(ledger.GetCommit(batch.Id)!, ledger.GetFrozenPlan(batch.Id)!).AsTask();

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(act);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldProjectCommittedProgressOnlyGivenMatchingTenantWorkload(bool foreign)
    {
        // Arrange
        var (tenant, batch, planned, ledger, targets, now) = Prepare();
        Assert.True(ledger.Commit(batch, 7, targets, now).IsSuccess);
        var workload = foreign ? Uuid.CreateVersion4() : tenant;
        var directory = new FitzApplicationImportDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity("ApplicationImportDirectoryV1", EventStreamPattern.ForPattern(workload.ToString()));
        var staged = new ApplicationImportStaged(tenant, batch.Id, Uuid.CreateVersion4(), batch.SourceKey!,
            batch.SourceNamespace!, batch.Coverage!, batch.ContentDigest!, batch.GetRows(), batch.SubmitterMemberId,
            batch.SubmitterDisplay!, now);

        // Act
        await using (var projection = await directory.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(staged);
            foreach (var ev in new AggregateScenario<ApplicationImportLedger>(planned).PendingEvents)
                await directory.ApplyAsync(ev);
            if (foreign)
                await Assert.ThrowsAsync<InvalidOperationException>(() => directory.ApplyAsync(ledger.GetCommit(batch.Id)!).AsTask());
            else
            {
                await directory.ApplyAsync(ledger.GetCommit(batch.Id)!);
                await projection.CommitAsync(ProjectionCheckpoint.Start);
            }
        }
        var view = await directory.GetAsync(workload, batch.Id);

        // Assert
        if (foreign)
            Assert.Null(view);
        else
        {
            Assert.Equal("committed", view!.State);
            Assert.Equal(8, view.Revision);
            Assert.Equal(2, view.AppliedCount);
            Assert.Equal(0, view.PendingCount);
            Assert.Equal(now, view.LastProgressAt);
        }
    }

    [Theory]
    [InlineData("missing_effect")]
    [InlineData("unsaved_marker")]
    [InlineData("foreign_workload")]
    public async Task ShouldRejectAuthoritativeProjectionGivenIncompleteCommitProof(string problem)
    {
        // Arrange
        var (tenant, batch, planned, ledger, targets, now) = Prepare();
        Assert.True(ledger.Commit(batch, 7, targets, now).IsSuccess);
        var persisted = new ApplicationImportLedger(tenant, "manual", "applications");
        _ = new AggregateScenario<ApplicationImportLedger>(persisted).Given(
            new AggregateScenario<ApplicationImportLedger>(planned).PendingEvents.Concat(
                new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents).ToArray());
        var reader = new SourceMapReader([problem == "unsaved_marker" ? ledger : persisted,
            .. problem == "missing_effect" ? targets.Values.Take(1) : targets.Values]);
        var directory = new FitzApplicationDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity("ApplicationDirectoryV2", EventStreamPattern.ForPattern(
            (problem == "foreign_workload" ? Uuid.CreateVersion4() : tenant).ToString()));
        await using var projection = await directory.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start));
        var projector = new ApplicationDirectoryProjector(directory, reader);

        // Act
        var act = () => projector.HandleAsync(ledger.GetCommit(batch.Id)!, new ProjectionContext(identity), CancellationToken.None).AsTask();

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(act);
    }

    [Fact]
    public async Task ShouldReportRetryableOwnerLookupGivenCommittedApplicationProjectionLag()
    {
        // Arrange
        var (tenant, batch, planned, ledger, targets, now) = Prepare();
        Assert.True(ledger.Commit(batch, 7, targets, now).IsSuccess);
        var persisted = new ApplicationImportLedger(tenant, "manual", "applications");
        _ = new AggregateScenario<ApplicationImportLedger>(persisted).Given(
            new AggregateScenario<ApplicationImportLedger>(planned).PendingEvents.Concat(
                new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents).ToArray());
        var sources = new GovernedAccessReviewSources(null!, null!, null!, null!, null!, null!,
            new FitzApplicationDirectory(new InMemoryKvClient()), new SourceMapReader([persisted, .. targets.Values]));

        // Act
        var result = await sources.AccessOwnerMemberAsync(tenant, targets.Values.First().Id);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(Assert.IsType<RequestError>(result.Error).IsTransient);
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

    static (Uuid Tenant, ImportBatch Batch, ApplicationImportLedger Planned, ApplicationImportLedger Ledger,
        Dictionary<Uuid, DeclaredApplication> Targets, DateTimeOffset Now) Prepare(Uuid? tenantId = null, bool padded = false)
    {
        var tenant = tenantId ?? Uuid.CreateVersion4();
        var actor = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var request = new StageApplicationImport(tenant, Uuid.CreateVersion4(), "manual", "applications", "partial",
            [new("app-1", padded ? " Payroll " : "Payroll", padded ? " Pay staff " : "Pay staff", null),
                new("app-2", "CRM", "Manage customers", null)]);
        var staged = new ImportBatch(tenant, ImportBatch.BatchIdFor(request));
        Assert.True(staged.Stage(request, actor, "Contributor", now).IsSuccess);
        var batch = new ImportBatch(tenant, staged.Id);
        _ = new AggregateScenario<ImportBatch>(batch).Given(new AggregateScenario<ImportBatch>(staged).PendingEvents.ToArray());
        var planned = new ApplicationImportLedger(tenant, "manual", "applications");
        foreach (var row in batch.GetRows())
            Assert.Null(planned.Correlate(batch, new CorrelateApplicationImportRow(tenant, batch.Id, row.RowId,
                planned.GetRevision(batch), "create_new", null, null, "Reviewed"), null, actor, "Lead", now));
        Assert.True(planned.BeginAcceptance(batch, 3, new Dictionary<Uuid, DeclaredApplication>(), actor, "Lead", now).IsSuccess);
        var ledger = new ApplicationImportLedger(tenant, "manual", "applications");
        _ = new AggregateScenario<ApplicationImportLedger>(ledger).Given(new AggregateScenario<ApplicationImportLedger>(planned).PendingEvents.ToArray());
        var targets = new Dictionary<Uuid, DeclaredApplication>();
        foreach (var row in ledger.GetFrozenPlan(batch.Id)!.Rows)
        {
            var target = new DeclaredApplication(tenant, row.ApplicationId);
            Assert.True(target.RecordPendingImportEffect(ledger, batch, row.RowId, now).IsSuccess);
            var saved = new DeclaredApplication(tenant, target.Id);
            _ = new AggregateScenario<DeclaredApplication>(saved).Given(new AggregateScenario<DeclaredApplication>(target).PendingEvents.ToArray());
            targets.Add(saved.Id, saved);
        }

        return (tenant, batch, planned, ledger, targets, now);
    }

}
