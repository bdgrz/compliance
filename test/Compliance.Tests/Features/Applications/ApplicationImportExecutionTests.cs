using System.Security.Claims;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationImportExecutionTests
{
    [Fact]
    public async Task ShouldFreezeWholeBatchGivenPersonalAcceptanceOfResolvedRows()
    {
        // Arrange
        await using var fixture = new Fixture();
        var batchId = await fixture.StageAsync();
        var handler = new AcceptApplicationImportHandler(fixture.Executor, fixture.Reader, TimeProvider.System);

        // Act
        var result = await handler.HandleAsync(new RequestContext<AcceptApplicationImport>(
            new(fixture.Tenant, batchId, 2), fixture.Actor), CancellationToken.None);
        var batch = await fixture.Reader.HydrateAsync(new ImportBatch(fixture.Tenant, batchId));
        var ledger = await fixture.LedgerAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("accepting", ledger.GetState(batch));
        Assert.Single(ledger.GetFrozenPlan(batchId)!.Rows);
        var target = await fixture.Reader.HydrateAsync(new DeclaredApplication(fixture.Tenant,
            ledger.GetFrozenPlan(batchId)!.Rows[0].ApplicationId));
        Assert.False(target.IsCreated);
        Assert.Empty(target.GetPendingImportEffects());
    }

    [Fact]
    public async Task ShouldCommitAllEffectsGivenTrustedExecutionAndReplay()
    {
        // Arrange
        await using var fixture = new Fixture();
        var batchId = await fixture.StageAsync();
        Assert.True((await new AcceptApplicationImportHandler(fixture.Executor, fixture.Reader, TimeProvider.System)
            .HandleAsync(new RequestContext<AcceptApplicationImport>(new(fixture.Tenant, batchId, 2), fixture.Actor),
                CancellationToken.None)).IsSuccess);
        var handler = new ExecuteApplicationImportHandler(fixture.Executor, fixture.Reader, new ActiveTenant(), TimeProvider.System, fixture.EffectBus(fixture.Executor));
        var context = new RequestContext<ExecuteApplicationImport>(new(fixture.Tenant, batchId), RequestActor.System);

        // Act
        var first = await handler.HandleAsync(context, CancellationToken.None);
        var replay = await handler.HandleAsync(context, CancellationToken.None);
        var batch = await fixture.Reader.HydrateAsync(new ImportBatch(fixture.Tenant, batchId));
        var ledger = await fixture.LedgerAsync();
        var row = Assert.Single(ledger.GetFrozenPlan(batchId)!.Rows);
        var target = await fixture.Reader.HydrateApplicationAsync(fixture.Tenant, row.ApplicationId);

        // Assert
        Assert.True(first.IsSuccess);
        Assert.True(replay.IsSuccess);
        Assert.Equal("committed", ledger.GetState(batch));
        Assert.True(ledger.IsCommitDurable(batchId));
        Assert.True(target.IsCreated);
        Assert.Equal(1, target.Revision);
        Assert.Single(target.GetPendingImportEffects());
        Assert.Single(ledger.GetCommit(batchId)!.Effects);
    }

    [Theory]
    [InlineData("unresolved")]
    [InlineData("stale")]
    [InlineData("wrong_tenant")]
    [InlineData("system")]
    public async Task ShouldRejectAcceptanceGivenInvalidAuthorityOrUnresolvedBatch(string condition)
    {
        // Arrange
        await using var fixture = new Fixture();
        var batchId = await fixture.StageAsync(correlate: condition != "unresolved");
        var handler = new AcceptApplicationImportHandler(fixture.Executor, fixture.Reader, TimeProvider.System);
        var request = new AcceptApplicationImport(condition == "wrong_tenant" ? Uuid.CreateVersion4() : fixture.Tenant,
            batchId, condition == "stale" ? 1 : 2);

        // Act
        var result = await handler.HandleAsync(new RequestContext<AcceptApplicationImport>(request,
            condition == "system" ? RequestActor.System : fixture.Actor), CancellationToken.None);
        var ledger = await fixture.LedgerAsync();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Null(ledger.GetFrozenPlan(batchId));
    }

    [Theory]
    [InlineData("user")]
    [InlineData("anonymous")]
    [InlineData("mixed")]
    [InlineData("inactive")]
    [InlineData("wrong_tenant")]
    [InlineData("unaccepted")]
    public async Task ShouldRejectExecutionGivenInvalidWorkerAuthority(string condition)
    {
        // Arrange
        await using var fixture = new Fixture();
        var batchId = await fixture.StageAsync();
        var actor = condition == "user" ? fixture.Actor : condition == "anonymous" ? RequestActor.Anonymous :
            condition == "mixed" ? new ClaimsPrincipal(RequestActor.System.Identities.Concat(fixture.Actor.Identities)) : RequestActor.System;
        var handler = new ExecuteApplicationImportHandler(fixture.Executor, fixture.Reader,
            new ActiveTenant(condition != "inactive"), TimeProvider.System, fixture.EffectBus(fixture.Executor));

        // Act
        var result = await handler.HandleAsync(new RequestContext<ExecuteApplicationImport>(
            new(condition == "wrong_tenant" ? Uuid.CreateVersion4() : fixture.Tenant, batchId), actor), CancellationToken.None);
        var ledger = await fixture.LedgerAsync();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Null(ledger.GetCommit(batchId));
        Assert.False(typeof(ICallable).IsAssignableFrom(typeof(ExecuteApplicationImport)));
        Assert.False(typeof(IApplicationInventoryRequest).IsAssignableFrom(typeof(ExecuteApplicationImport)));
    }

    [Fact]
    public async Task ShouldResumeWithoutDuplicatesGivenCrashAfterFirstDurableEffect()
    {
        // Arrange
        await using var fixture = new Fixture();
        var batchId = await fixture.StageAsync(rowCount: 2);
        Assert.True((await new AcceptApplicationImportHandler(fixture.Executor, fixture.Reader, TimeProvider.System)
            .HandleAsync(new RequestContext<AcceptApplicationImport>(new(fixture.Tenant, batchId, 3), fixture.Actor),
                CancellationToken.None)).IsSuccess);
        var interrupted = new ExecuteApplicationImportHandler(new InterruptedExecutor(fixture.Executor), fixture.Reader,
            new ActiveTenant(), TimeProvider.System, fixture.EffectBus(new InterruptedExecutor(fixture.Executor)));
        var context = new RequestContext<ExecuteApplicationImport>(new(fixture.Tenant, batchId), RequestActor.System);

        // Act
        await Assert.ThrowsAsync<IOException>(async () => await interrupted.HandleAsync(context, CancellationToken.None));
        var before = await fixture.LedgerAsync();
        var firstTarget = await fixture.Reader.HydrateApplicationAsync(fixture.Tenant, before.GetFrozenPlan(batchId)!.Rows[0].ApplicationId);
        var resumed = await new ExecuteApplicationImportHandler(fixture.Executor, fixture.Reader, new ActiveTenant(), TimeProvider.System, fixture.EffectBus(fixture.Executor))
            .HandleAsync(context, CancellationToken.None);
        var after = await fixture.LedgerAsync();

        // Assert
        Assert.Null(before.GetCommit(batchId));
        Assert.False(firstTarget.IsCreated);
        Assert.Single(firstTarget.GetPendingImportEffects());
        Assert.True(resumed.IsSuccess);
        Assert.Equal(2, after.GetCommit(batchId)!.Effects.Count);
        foreach (var row in after.GetFrozenPlan(batchId)!.Rows)
        {
            var target = await fixture.Reader.HydrateApplicationAsync(fixture.Tenant, row.ApplicationId);
            Assert.True(target.IsCreated);
            Assert.Single(target.GetPendingImportEffects());
        }
    }

    [Fact]
    public async Task ShouldLeaveEveryEffectInvisibleGivenCancellationDuringRecovery()
    {
        // Arrange
        await using var fixture = new Fixture();
        var batchId = await fixture.StageAsync(rowCount: 2);
        Assert.True((await new AcceptApplicationImportHandler(fixture.Executor, fixture.Reader, TimeProvider.System)
            .HandleAsync(new RequestContext<AcceptApplicationImport>(new(fixture.Tenant, batchId, 3), fixture.Actor),
                CancellationToken.None)).IsSuccess);
        var context = new RequestContext<ExecuteApplicationImport>(new(fixture.Tenant, batchId), RequestActor.System);
        await Assert.ThrowsAsync<IOException>(async () => await new ExecuteApplicationImportHandler(
            new InterruptedExecutor(fixture.Executor), fixture.Reader, new ActiveTenant(), TimeProvider.System, fixture.EffectBus(
            new InterruptedExecutor(fixture.Executor)))
            .HandleAsync(context, CancellationToken.None));
        var accepting = await fixture.LedgerAsync();
        var batch = await fixture.Reader.HydrateAsync(new ImportBatch(fixture.Tenant, batchId));
        Assert.True((await new CancelApplicationImportHandler(fixture.Executor, fixture.Reader, TimeProvider.System)
            .HandleAsync(new RequestContext<CancelApplicationImport>(new(fixture.Tenant, batchId,
                accepting.GetRevision(batch), "Withdrawn after interruption"), fixture.Actor), CancellationToken.None)).IsSuccess);

        // Act
        var replay = await new ExecuteApplicationImportHandler(fixture.Executor, fixture.Reader, new ActiveTenant(), TimeProvider.System, fixture.EffectBus(fixture.Executor))
            .HandleAsync(context, CancellationToken.None);
        var ledger = await fixture.LedgerAsync();

        // Assert
        Assert.True(replay.IsSuccess);
        Assert.Null(ledger.GetCommit(batchId));
        Assert.Equal("canceled", ledger.GetState(batch));
        foreach (var row in ledger.GetFrozenPlan(batchId)!.Rows)
            Assert.False((await fixture.Reader.HydrateApplicationAsync(fixture.Tenant, row.ApplicationId)).IsCreated);
    }

    [Fact]
    public async Task ShouldRollBackEveryEffectGivenPermanentRejectionAfterFirstDurableEffect()
    {
        // Arrange
        await using var fixture = new Fixture();
        var batchId = await fixture.StageAsync(rowCount: 2);
        Assert.True((await new AcceptApplicationImportHandler(fixture.Executor, fixture.Reader, TimeProvider.System)
            .HandleAsync(new RequestContext<AcceptApplicationImport>(new(fixture.Tenant, batchId, 3), fixture.Actor),
                CancellationToken.None)).IsSuccess);
        var context = new RequestContext<ExecuteApplicationImport>(new(fixture.Tenant, batchId), RequestActor.System);
        var handler = new ExecuteApplicationImportHandler(new InterruptedExecutor(fixture.Executor, reject: true), fixture.Reader,
            new ActiveTenant(), TimeProvider.System, fixture.EffectBus(new InterruptedExecutor(fixture.Executor, reject: true)));

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);
        var replay = await new ExecuteApplicationImportHandler(fixture.Executor, fixture.Reader, new ActiveTenant(), TimeProvider.System, fixture.EffectBus(fixture.Executor))
            .HandleAsync(context, CancellationToken.None);
        var ledger = await fixture.LedgerAsync();
        var batch = await fixture.Reader.HydrateAsync(new ImportBatch(fixture.Tenant, batchId));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(replay.IsSuccess);
        Assert.Equal("failed", ledger.GetState(batch));
        Assert.Null(ledger.GetCommit(batchId));
        foreach (var row in ledger.GetFrozenPlan(batchId)!.Rows)
            Assert.False((await fixture.Reader.HydrateApplicationAsync(fixture.Tenant, row.ApplicationId)).IsCreated);
    }

    [Fact]
    public async Task ShouldReportDurableProgressGivenInterruptedWholeBatch()
    {
        // Arrange
        await using var fixture = new Fixture();
        var batchId = await fixture.StageAsync(rowCount: 2);
        Assert.True((await new AcceptApplicationImportHandler(fixture.Executor, fixture.Reader, TimeProvider.System)
            .HandleAsync(new RequestContext<AcceptApplicationImport>(new(fixture.Tenant, batchId, 3), fixture.Actor),
                CancellationToken.None)).IsSuccess);
        await Assert.ThrowsAsync<IOException>(async () => await new ExecuteApplicationImportHandler(
            new InterruptedExecutor(fixture.Executor), fixture.Reader, new ActiveTenant(), TimeProvider.System, fixture.EffectBus(
            new InterruptedExecutor(fixture.Executor)))
            .HandleAsync(new RequestContext<ExecuteApplicationImport>(new(fixture.Tenant, batchId), RequestActor.System), CancellationToken.None));

        // Act
        var result = await new GetApplicationImportProgressHandler(fixture.Reader).HandleAsync(
            new RequestContext<GetApplicationImportProgress>(new(fixture.Tenant, batchId), fixture.Actor), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("accepting", result.Value.State);
        Assert.Equal(2, result.Value.PlannedCount);
        Assert.Equal(1, result.Value.DurableEffectCount);
        Assert.Equal(0, result.Value.AppliedCount);
    }

    [Fact]
    public async Task ShouldReportValidationFindingsGivenRejectedStagedRow()
    {
        // Arrange
        await using var fixture = new Fixture();
        var batchId = await fixture.StageAsync(correlate: false, invalid: true);

        // Act
        var result = await new GetApplicationImportRejectedReportHandler(fixture.Reader).HandleAsync(
            new RequestContext<GetApplicationImportRejectedReport>(new(fixture.Tenant, batchId), fixture.Actor), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var row = Assert.Single(result.Value.Rows);
        Assert.Equal("row-1", row.SourceRecordId);
        Assert.Contains("name_required", row.Findings);
        Assert.Equal("preview_ready", result.Value.State);
    }

    [Fact]
    public async Task ShouldReportTargetConflictGivenLinkedTargetChangedAfterCorrelation()
    {
        // Arrange
        await using var fixture = new Fixture();
        var batchId = await fixture.StageAsync(correlate: false);
        var target = new DeclaredApplication(fixture.Tenant, Uuid.CreateVersion4());
        Assert.True(target.Declare("Manual", "Governed purpose", null, Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow).IsSuccess);
        var context = new RequestContext<AcceptApplicationImport>(new(fixture.Tenant, batchId, 2), fixture.Actor);
        await fixture.Writer.SaveAsync(target, context);
        Assert.True((await new CorrelateApplicationImportRowHandler(fixture.Executor, fixture.Reader, TimeProvider.System)
            .HandleAsync(new RequestContext<CorrelateApplicationImportRow>(new(fixture.Tenant, batchId,
                Uuid.CreateVersion5(batchId, "1"), 1, "link_existing", target.Id, 1, "Reviewed identity"), fixture.Actor),
                CancellationToken.None)).IsSuccess);
        Assert.Null(target.Revise(1, "Revised", "Changed purpose", null, Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow));
        await fixture.Writer.SaveAsync(target, context);

        // Act
        var result = await new GetApplicationImportRejectedReportHandler(fixture.Reader).HandleAsync(
            new RequestContext<GetApplicationImportRejectedReport>(new(fixture.Tenant, batchId), fixture.Actor), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains("target_revision_conflict", Assert.Single(result.Value.Rows).Findings);
    }

    [Theory]
    [InlineData("wrong_tenant")]
    [InlineData("minimum_future")]
    [InlineData("minimum_invalid")]
    public async Task ShouldRejectLifecycleReadsGivenWrongTenantOrUnsatisfiedRevision(string condition)
    {
        // Arrange
        await using var fixture = new Fixture();
        var batchId = await fixture.StageAsync();
        var tenant = condition == "wrong_tenant" ? Uuid.CreateVersion4() : fixture.Tenant;
        var minimum = condition == "minimum_future" ? 100 : condition == "minimum_invalid" ? 0 : (long?)null;

        // Act
        var progress = await new GetApplicationImportProgressHandler(fixture.Reader).HandleAsync(
            new RequestContext<GetApplicationImportProgress>(new(tenant, batchId, minimum), fixture.Actor), CancellationToken.None);
        var report = await new GetApplicationImportRejectedReportHandler(fixture.Reader).HandleAsync(
            new RequestContext<GetApplicationImportRejectedReport>(new(tenant, batchId, minimum), fixture.Actor), CancellationToken.None);

        // Assert
        Assert.False(progress.IsSuccess);
        Assert.False(report.IsSuccess);
        Assert.Equal(progress.Error.Kind, report.Error.Kind);
        Assert.Equal(condition == "minimum_future", progress.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldRetryAfterReactivationGivenSuspendedExecution()
    {
        // Arrange
        await using var fixture = new Fixture();
        var batchId = await fixture.StageAsync();
        Assert.True((await new AcceptApplicationImportHandler(fixture.Executor, fixture.Reader, TimeProvider.System)
            .HandleAsync(new RequestContext<AcceptApplicationImport>(new(fixture.Tenant, batchId, 2), fixture.Actor),
                CancellationToken.None)).IsSuccess);
        var context = new RequestContext<ExecuteApplicationImport>(new(fixture.Tenant, batchId), RequestActor.System);

        // Act
        var suspended = await new ExecuteApplicationImportHandler(fixture.Executor, fixture.Reader, new ActiveTenant(false), TimeProvider.System, fixture.EffectBus(fixture.Executor))
            .HandleAsync(context, CancellationToken.None);
        var resumed = await new ExecuteApplicationImportHandler(fixture.Executor, fixture.Reader, new ActiveTenant(), TimeProvider.System, fixture.EffectBus(fixture.Executor))
            .HandleAsync(context, CancellationToken.None);
        var ledger = await fixture.LedgerAsync();

        // Assert
        Assert.False(suspended.IsSuccess);
        Assert.True(suspended.Error.IsTransient);
        Assert.True(resumed.IsSuccess);
        Assert.Null(ledger.GetFailure(batchId));
        Assert.True(ledger.IsCommitDurable(batchId));
    }

    [Fact]
    public async Task ShouldDispatchFromDurableSealGivenTenantWorkerReplay()
    {
        // Arrange
        await using var fixture = new Fixture();
        var batchId = await fixture.StageAsync();
        Assert.True((await new AcceptApplicationImportHandler(fixture.Executor, fixture.Reader, TimeProvider.System)
            .HandleAsync(new RequestContext<AcceptApplicationImport>(new(fixture.Tenant, batchId, 2), fixture.Actor),
                CancellationToken.None)).IsSuccess);
        var scenario = new ReactorScenario(new TenantId(fixture.Tenant.ToString()));
        var checkpoints = new InMemoryProjectionCheckpointStore();
        var reactor = new ApplicationImportExecutionReactor(checkpoints, scenario.Requests, fixture.Reader);
        await scenario.RunAsync(reactor);
        var runner = new ReactorRunner(fixture.Events);

        // Act
        var completed = await runner.RunAsync(reactor, ProjectionCheckpoint.Start);
        await runner.RunAsync(reactor, completed);

        // Assert
        Assert.Equal(fixture.Tenant.ToString(), reactor.Pattern.Realm);
        Assert.Equal(new ExecuteApplicationImport(fixture.Tenant, batchId), Assert.Single(scenario.SentRequests));
    }

    [Fact]
    public async Task ShouldCommitNewTargetsGivenLaterBatchAfterPermanentFailure()
    {
        // Arrange
        await using var fixture = new Fixture();
        var firstBatch = await fixture.StageAsync(rowCount: 2);
        Assert.True((await new AcceptApplicationImportHandler(fixture.Executor, fixture.Reader, TimeProvider.System)
            .HandleAsync(new RequestContext<AcceptApplicationImport>(new(fixture.Tenant, firstBatch, 3), fixture.Actor),
                CancellationToken.None)).IsSuccess);
        Assert.True((await new ExecuteApplicationImportHandler(new InterruptedExecutor(fixture.Executor, reject: true), fixture.Reader,
            new ActiveTenant(), TimeProvider.System, fixture.EffectBus(new InterruptedExecutor(fixture.Executor, reject: true))).HandleAsync(new RequestContext<ExecuteApplicationImport>(
                new(fixture.Tenant, firstBatch), RequestActor.System), CancellationToken.None)).IsSuccess);
        var failed = await fixture.LedgerAsync();
        var failedTarget = failed.GetFrozenPlan(firstBatch)!.Rows[0].ApplicationId;
        var nextBatch = await fixture.StageAsync();

        // Act
        var accepted = await new AcceptApplicationImportHandler(fixture.Executor, fixture.Reader, TimeProvider.System)
            .HandleAsync(new RequestContext<AcceptApplicationImport>(new(fixture.Tenant, nextBatch, 2), fixture.Actor), CancellationToken.None);
        var executed = await new ExecuteApplicationImportHandler(fixture.Executor, fixture.Reader, new ActiveTenant(), TimeProvider.System, fixture.EffectBus(fixture.Executor))
            .HandleAsync(new RequestContext<ExecuteApplicationImport>(new(fixture.Tenant, nextBatch), RequestActor.System), CancellationToken.None);
        var ledger = await fixture.LedgerAsync();
        var nextTarget = ledger.GetFrozenPlan(nextBatch)!.Rows[0].ApplicationId;

        // Assert
        Assert.True(accepted.IsSuccess);
        Assert.True(executed.IsSuccess);
        Assert.NotEqual(failedTarget, nextTarget);
        Assert.False((await fixture.Reader.HydrateApplicationAsync(fixture.Tenant, failedTarget)).IsCreated);
        Assert.True((await fixture.Reader.HydrateApplicationAsync(fixture.Tenant, nextTarget)).IsCreated);
        Assert.Single(ledger.GetSourceClaims());
    }

    [Fact]
    public async Task ShouldRetainSealCheckpointGivenInactiveTenantThenResumeAfterReactivation()
    {
        // Arrange
        await using var fixture = new Fixture();
        var batchId = await fixture.StageAsync();
        Assert.True((await new AcceptApplicationImportHandler(fixture.Executor, fixture.Reader, TimeProvider.System)
            .HandleAsync(new RequestContext<AcceptApplicationImport>(new(fixture.Tenant, batchId, 2), fixture.Actor),
                CancellationToken.None)).IsSuccess);
        var checkpoints = new InMemoryProjectionCheckpointStore();
        var reactor = new ApplicationImportExecutionReactor(checkpoints, fixture.Bus, fixture.Reader);
        await new ReactorScenario(new TenantId(fixture.Tenant.ToString())).RunAsync(reactor);
        var starting = ProjectionCheckpoint.Start;
        await foreach (var record in fixture.Events.ReadAsync(reactor.Pattern, EventCursor.Start, CancellationToken.None))
        {
            if (record.Event is ApplicationImportPlanSealed)
                break;
            starting = new ProjectionCheckpoint(record.NextCursor);
        }
        var identity = new CheckpointIdentity(reactor.Name, reactor.Pattern);
        await checkpoints.SaveAsync(identity, starting);
        fixture.Activity.Active = false;
        var runner = new ReactorRunner(fixture.Events);

        // Act
        var failure = await Assert.ThrowsAsync<ReactionCommandFailedException>(async () =>
            await runner.RunAsync(reactor, starting));
        var interrupted = await checkpoints.LoadAsync(identity);
        fixture.Activity.Active = true;
        var resumed = await runner.RunAsync(reactor, interrupted);
        var ledger = await fixture.LedgerAsync();

        // Assert
        Assert.True(failure.Error.IsTransient);
        Assert.Equal(starting, interrupted);
        Assert.NotEqual(interrupted, resumed);
        Assert.True(ledger.IsCommitDurable(batchId));
        Assert.Null(ledger.GetFailure(batchId));
    }

    [Fact]
    public async Task ShouldReleaseLinkedTargetReservationGivenDurablePermanentRollback()
    {
        // Arrange
        await using var fixture = new Fixture();
        var batchId = await fixture.StageAsync(rowCount: 2);
        var target = new DeclaredApplication(fixture.Tenant, Uuid.CreateVersion4());
        Assert.True(target.Declare("Manual", "Governed", null, Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow).IsSuccess);
        var context = new RequestContext<AcceptApplicationImport>(new(fixture.Tenant, batchId, 4), fixture.Actor);
        await fixture.Writer.SaveAsync(target, context);
        Assert.True((await new CorrelateApplicationImportRowHandler(fixture.Executor, fixture.Reader, TimeProvider.System)
            .HandleAsync(new RequestContext<CorrelateApplicationImportRow>(new(fixture.Tenant, batchId,
                Uuid.CreateVersion5(batchId, "1"), 3, "link_existing", target.Id, 1, "Reviewed exact existing target"), fixture.Actor),
                CancellationToken.None)).IsSuccess);
        Assert.True((await new AcceptApplicationImportHandler(fixture.Executor, fixture.Reader, TimeProvider.System)
            .HandleAsync(context, CancellationToken.None)).IsSuccess);
        Assert.True((await new ExecuteApplicationImportHandler(new InterruptedExecutor(fixture.Executor, reject: true), fixture.Reader,
            new ActiveTenant(), TimeProvider.System, fixture.EffectBus(new InterruptedExecutor(fixture.Executor, reject: true))).HandleAsync(new RequestContext<ExecuteApplicationImport>(
                new(fixture.Tenant, batchId), RequestActor.System), CancellationToken.None)).IsSuccess);

        // Act
        var settled = await fixture.Reader.HydrateApplicationAsync(fixture.Tenant, target.Id);
        var revised = settled.Revise(1, "Updated manually", "Governed revision", null, Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow);
        var report = await new GetApplicationImportRejectedReportHandler(fixture.Reader).HandleAsync(
            new RequestContext<GetApplicationImportRejectedReport>(new(fixture.Tenant, batchId), fixture.Actor), CancellationToken.None);
        var progress = await new GetApplicationImportProgressHandler(fixture.Reader).HandleAsync(
            new RequestContext<GetApplicationImportProgress>(new(fixture.Tenant, batchId), fixture.Actor), CancellationToken.None);

        // Assert
        Assert.Null(revised);
        Assert.True(report.IsSuccess);
        Assert.Equal("target_effect_rejected", report.Value.FailureCode);
        Assert.Equal(2, report.Value.Rows.Count);
        Assert.All(report.Value.Rows, row => Assert.Contains("batch_execution_failed", row.Findings));
        Assert.True(progress.IsSuccess);
        Assert.Equal(1, progress.Value.DurableEffectCount);
        Assert.Equal(0, progress.Value.AppliedCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRejectSealGivenForgedTenantOrSourceStream(bool wrongTenant)
    {
        // Arrange
        await using var fixture = new Fixture();
        var batchId = await fixture.StageAsync();
        Assert.True((await new AcceptApplicationImportHandler(fixture.Executor, fixture.Reader, TimeProvider.System)
            .HandleAsync(new RequestContext<AcceptApplicationImport>(new(fixture.Tenant, batchId, 2), fixture.Actor),
                CancellationToken.None)).IsSuccess);
        var plan = (await fixture.LedgerAsync()).GetFrozenPlan(batchId)!;
        var forgedStreamId = Uuid.CreateVersion4();
        var forged = new ApplicationImportPlanSealed(wrongTenant ? Uuid.CreateVersion4() : fixture.Tenant,
            batchId, plan.Revision, plan.PlanSha256);
        forged.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), forgedStreamId, 1, DateTimeOffset.UtcNow));
        await fixture.Events.AppendAsync(new EventStreamAddress(fixture.Tenant.ToString(), "application_imports",
            forgedStreamId.ToString()), 0, [forged]);
        var scenario = new ReactorScenario(new TenantId(fixture.Tenant.ToString()));
        var reactor = new ApplicationImportExecutionReactor(new InMemoryProjectionCheckpointStore(), scenario.Requests, fixture.Reader);
        await scenario.RunAsync(reactor);

        // Act
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await new ReactorRunner(fixture.Events).RunAsync(reactor, ProjectionCheckpoint.Start));

        // Assert
        Assert.Contains(wrongTenant ? "tenant workload" : "source ledger", failure.Message);
        Assert.Single(scenario.SentRequests);
    }

    [Fact]
    public async Task ShouldRejectWholeBatchGivenEffectCommandDeniedByPlanAuthorizer()
    {
        // Arrange
        await using var fixture = new Fixture();
        var batchId = await fixture.StageAsync();
        Assert.True((await new AcceptApplicationImportHandler(fixture.Executor, fixture.Reader, TimeProvider.System)
            .HandleAsync(new RequestContext<AcceptApplicationImport>(new(fixture.Tenant, batchId, 2), fixture.Actor),
                CancellationToken.None)).IsSuccess);
        var bus = fixture.EffectBus(fixture.Executor, rejectTarget: true);
        var context = new RequestContext<ExecuteApplicationImport>(new(fixture.Tenant, batchId), RequestActor.System);

        // Act
        var result = await new ExecuteApplicationImportHandler(fixture.Executor, fixture.Reader, new ActiveTenant(), TimeProvider.System, bus)
            .HandleAsync(context, CancellationToken.None);
        var ledger = await fixture.LedgerAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(bus.Effects);
        Assert.Equal("failed", ledger.GetState(await fixture.Reader.HydrateAsync(new ImportBatch(fixture.Tenant, batchId))));
        Assert.Null(ledger.GetCommit(batchId));
        var planned = Assert.Single(ledger.GetFrozenPlan(batchId)!.Rows);
        Assert.Empty((await fixture.Reader.HydrateAsync(new DeclaredApplication(fixture.Tenant, planned.ApplicationId))).GetPendingImportEffects());
    }

    [Fact]
    public async Task ShouldPreserveTrustedParentMetadataGivenPerEffectBusDispatch()
    {
        // Arrange
        await using var fixture = new Fixture();
        var batchId = await fixture.StageAsync();
        Assert.True((await new AcceptApplicationImportHandler(fixture.Executor, fixture.Reader, TimeProvider.System)
            .HandleAsync(new RequestContext<AcceptApplicationImport>(new(fixture.Tenant, batchId, 2), fixture.Actor),
                CancellationToken.None)).IsSuccess);
        var bus = fixture.EffectBus(fixture.Executor, dispatchActual: true);
        var context = new RequestContext<ExecuteApplicationImport>(new(fixture.Tenant, batchId),
            RequestActor.CreateSystem("reactor:ApplicationImportExecutionV1", "bdgrz.system"));

        // Act
        var result = await new ExecuteApplicationImportHandler(fixture.Executor, fixture.Reader, new ActiveTenant(), TimeProvider.System, bus)
            .HandleAsync(context, CancellationToken.None);
        var ledger = await fixture.LedgerAsync();
        var row = Assert.Single(ledger.GetFrozenPlan(batchId)!.Rows);
        var target = await fixture.Reader.HydrateAsync(new DeclaredApplication(fixture.Tenant, row.ApplicationId));

        // Assert
        Assert.True(result.IsSuccess);
        var child = Assert.Single(bus.Contexts);
        Assert.True(RequestActor.IsSystem(child.Actor));
        Assert.Equal(context.CorrelationId, child.CorrelationId);
        Assert.Equal(context.RequestId, child.CausationId);
        Assert.Equal(context.CorrelationId, target.GetPendingImportEffect(batchId, row.RowId)!.Metadata.CorrelationId);
        Assert.Equal(child.RequestId, target.GetPendingImportEffect(batchId, row.RowId)!.Metadata.CausationId);
    }

    sealed class EffectCommandBus(IRequestBus inner, IAggregateExecutor executor, IAggregateReader reader, bool rejectTarget, bool dispatchActual)
        : IRequestBus
    {
        public List<ApplyApplicationImportEffect> Effects { get; } = [];
        public List<RequestDispatchContext> Contexts { get; } = [];
        public RequestDispatchContext CreateContext(ClaimsPrincipal actor, RequestMetadata? metadata = null) => inner.CreateContext(actor, metadata);
        public ValueTask<Result> AuthorizeAsync(IRequestBase request, RequestDispatchContext context, CancellationToken ct = default) =>
            inner.AuthorizeAsync(request, context, ct);
        public async ValueTask<Result> DispatchAsync(IRequest request, RequestDispatchContext context, CancellationToken ct = default)
        {
            if (request is not ApplyApplicationImportEffect effect)
                return await inner.DispatchAsync(request, context, ct);
            Effects.Add(effect);
            Contexts.Add(context);
            var authorized = await inner.AuthorizeAsync(rejectTarget ? effect with { ApplicationId = Uuid.CreateVersion4() } : effect, context, ct);
            if (!authorized.IsSuccess)
                return authorized;
            if (dispatchActual)
                return await inner.DispatchAsync(effect, context, ct);
            return await new ApplyApplicationImportEffectHandler(executor, reader, TimeProvider.System).HandleAsync(
                new RequestContext<ApplyApplicationImportEffect>(effect, context.Actor), ct);
        }
        public ValueTask<Result<TOut>> DispatchAsync<TOut>(IRequest<TOut> request, RequestDispatchContext context, CancellationToken ct = default) =>
            inner.DispatchAsync(request, context, ct);
        public IAsyncEnumerable<TOut> DispatchStreamAsync<TOut>(IStreamRequest<TOut> request, RequestDispatchContext context,
            CancellationToken ct = default) => inner.DispatchStreamAsync(request, context, ct);
    }

    sealed class MutableTenantActivity : ITenantActivity
    {
        public bool Active { get; set; } = true;
        public ValueTask<bool> IsActiveAsync(Uuid tenantId, CancellationToken ct = default) => ValueTask.FromResult(Active);
    }

    sealed class InterruptedExecutor(IAggregateExecutor inner, bool reject = false) : IAggregateExecutor
    {
        int _writes;
        public ValueTask<Result> ExecuteAsync<TAggregate>(TAggregate aggregate, Func<TAggregate, AggregateOutcome> operation,
            IExecutionContext context, CancellationToken ct = default) where TAggregate : Aggregate
        {
            if (aggregate is DeclaredApplication && ++_writes == 2)
            {
                if (reject)
                    return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation, "Permanent rejection.")));
                throw new IOException("Simulated process interruption after one durable effect.");
            }
            return inner.ExecuteAsync(aggregate, operation, context, ct);
        }
        public ValueTask<Result<TOut>> ExecuteAsync<TAggregate, TOut>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome<TOut>> operation, IExecutionContext context, CancellationToken ct = default)
            where TAggregate : Aggregate => inner.ExecuteAsync(aggregate, operation, context, ct);
    }

    sealed class ActiveTenant(bool active = true) : ITenantActivity
    {
        public ValueTask<bool> IsActiveAsync(Uuid tenantId, CancellationToken ct = default) => ValueTask.FromResult(active);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public ClaimsPrincipal Actor { get; } = new(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())], "BdgrzSession"));
        public IAggregateExecutor Executor { get; }
        public IAggregateReader Reader { get; }
        public IAggregateWriter Writer { get; }
        public IEventStore Events { get; }
        public IRequestBus Bus { get; }
        public MutableTenantActivity Activity { get; } = new();

        public Fixture()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(new InMemoryEventStore());
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<ITenantActivity>(Activity);
            services.AddPortia().AddRequestHandler<ExecuteApplicationImportHandler>()
                .AddRequestAuthorizer<ExecuteApplicationImportAuthorizer>()
                .AddRequestHandler<ApplyApplicationImportEffectHandler>()
                .AddRequestAuthorizer<ApplyApplicationImportEffectAuthorizer>();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
            Executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            Events = _scope.ServiceProvider.GetRequiredService<IEventStore>();
            Bus = _scope.ServiceProvider.GetRequiredService<IRequestBus>();
        }

        public EffectCommandBus EffectBus(IAggregateExecutor executor, bool rejectTarget = false, bool dispatchActual = false) =>
            new(Bus, executor, Reader, rejectTarget, dispatchActual);

        public ValueTask<ApplicationImportLedger> LedgerAsync() => Reader.HydrateAsync(
            new ApplicationImportLedger(Tenant, "manual", "applications"));

        public async Task<Uuid> StageAsync(bool correlate = true, int rowCount = 1, bool invalid = false)
        {
            var request = new StageApplicationImport(Tenant, Uuid.CreateVersion4(), "manual", "applications",
                "partial", Enumerable.Range(1, rowCount).Select(i => new ApplicationImportInputRow($"row-{i}", invalid ? "" : "Payroll", "Pay staff", null)).ToArray());
            var staged = await new StageApplicationImportHandler(Executor, Reader, TimeProvider.System)
                .HandleAsync(new RequestContext<StageApplicationImport>(request, Actor), CancellationToken.None);
            Assert.True(staged.IsSuccess);
            if (correlate)
            {
                for (var i = 1; i <= rowCount; i++)
                {
                    var correlation = new CorrelateApplicationImportRow(Tenant, staged.Value.BatchId,
                        Uuid.CreateVersion5(staged.Value.BatchId, i.ToString(System.Globalization.CultureInfo.InvariantCulture)), i, "create_new", null, null, "Reviewed identity");
                    Assert.True((await new CorrelateApplicationImportRowHandler(Executor, Reader, TimeProvider.System)
                        .HandleAsync(new RequestContext<CorrelateApplicationImportRow>(correlation, Actor), CancellationToken.None)).IsSuccess);
                }
            }
            return staged.Value.BatchId;
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }
}
