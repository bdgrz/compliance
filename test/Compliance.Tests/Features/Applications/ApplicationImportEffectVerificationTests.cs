using Bdgrz.Compliance.Features.Applications;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationImportEffectVerificationTests
{
    [Fact]
    public async Task ShouldVerifyAuthoritativeDurabilityGivenFreshPersistenceHydration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddSingleton(TimeProvider.System);
        services.AddPortia();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var tenant = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var actor = new ClaimsPrincipal(new ClaimsIdentity([new Claim("iss", "bdgrz"), new Claim("sub", user.ToString())], "BdgrzSession"));
        var stage = new StageApplicationImport(tenant, Uuid.CreateVersion4(), "manual", "applications", "partial",
            [new("app-1", "Payroll", "Pay staff", null)]);
        var staged = await new StageApplicationImportHandler(executor, reader, TimeProvider.System)
            .HandleAsync(new RequestContext<StageApplicationImport>(stage, actor), CancellationToken.None);
        Assert.True(staged.IsSuccess);
        var batch = await reader.HydrateAsync(new ImportBatch(tenant, staged.Value.BatchId));
        var rowId = batch.GetRows()[0].RowId;
        var correlation = new RequestContext<CorrelateApplicationImportRow>(new CorrelateApplicationImportRow(
            tenant, batch.Id, rowId, 1, "create_new", null, null, "Reviewed"), actor);
        Assert.True((await new CorrelateApplicationImportRowHandler(executor, reader, TimeProvider.System)
            .HandleAsync(correlation, CancellationToken.None)).IsSuccess);
        Assert.True((await executor.ExecuteAsync(new ApplicationImportLedger(tenant, "manual", "applications"),
            ledger => AggregateOutcome.CommitOnSuccess(ledger.BeginAcceptance(batch, 2,
                new Dictionary<Uuid, DeclaredApplication>(), RbacIds.Member(tenant, user), "Lead", DateTimeOffset.UtcNow)), correlation)).IsSuccess);
        var ledger = await reader.HydrateAsync(new ApplicationImportLedger(tenant, "manual", "applications"));
        var applicationId = ledger.GetFrozenPlan(batch.Id)!.Rows[0].ApplicationId;
        var missing = ledger.VerifyPendingEffects(batch, 5, new Dictionary<Uuid, DeclaredApplication>());

        // Act
        var applied = await new ApplyApplicationImportEffectHandler(executor, reader, TimeProvider.System)
            .HandleAsync(new RequestContext<ApplyApplicationImportEffect>(
                new ApplyApplicationImportEffect(tenant, batch.Id, rowId, applicationId), RequestActor.System), CancellationToken.None);
        var target = await reader.HydrateAsync(new DeclaredApplication(tenant, applicationId));
        var verified = ledger.VerifyPendingEffects(batch, 5, new Dictionary<Uuid, DeclaredApplication> { [target.Id] = target });

        // Assert
        Assert.False(missing.IsSuccess);
        Assert.True(Assert.IsType<RequestError>(missing.Error).IsTransient);
        Assert.True(applied.IsSuccess);
        Assert.True(verified.IsSuccess);
        Assert.Single(verified.Value);
        Assert.Equal(1UL, target.CommittedStreamPosition);
        Assert.Equal(4UL, ledger.CommittedStreamPosition);
        Assert.False(target.IsCreated);
        Assert.Equal(0, target.Revision);
        Assert.Empty(new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldUsePhysicalSealVersionGivenPriorBatchHistory(bool savedPlan)
    {
        // Arrange
        var fixture = Durable();
        Assert.Null(fixture.Ledger.Cancel(fixture.Batch, fixture.Ledger.GetRevision(fixture.Batch), "Canceled",
            fixture.Actor, "Lead", fixture.Now));
        var history = fixture.History.Concat(new AggregateScenario<ApplicationImportLedger>(fixture.Ledger).PendingEvents).ToArray();
        var ledger = new ApplicationImportLedger(fixture.Tenant, "manual", "applications");
        _ = new AggregateScenario<ApplicationImportLedger>(ledger).Given(history);
        var request = new StageApplicationImport(fixture.Tenant, Uuid.CreateVersion4(), "manual", "applications", "partial",
            [new("app-3", "Another", "Another purpose", null)]);
        var staged = new ImportBatch(fixture.Tenant, ImportBatch.BatchIdFor(request));
        Assert.True(staged.Stage(request, fixture.Actor, "Contributor", fixture.Now).IsSuccess);
        var batch = Saved(staged, new ImportBatch(fixture.Tenant, staged.Id));
        var rowId = batch.GetRows()[0].RowId;
        Assert.Null(ledger.Correlate(batch, new CorrelateApplicationImportRow(fixture.Tenant, batch.Id,
            rowId, 1, "create_new", null, null, "Reviewed"), null, fixture.Actor, "Lead", fixture.Now));
        Assert.True(ledger.BeginAcceptance(batch, 2, new Dictionary<Uuid, DeclaredApplication>(), fixture.Actor, "Lead", fixture.Now).IsSuccess);
        if (savedPlan)
        {
            var saved = new ApplicationImportLedger(fixture.Tenant, "manual", "applications");
            _ = new AggregateScenario<ApplicationImportLedger>(saved).Given(history.Concat(
                new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents).ToArray());
            ledger = saved;
        }
        var target = new DeclaredApplication(fixture.Tenant, ledger.GetFrozenPlan(batch.Id)!.Rows[0].ApplicationId);
        Assert.True(target.RecordPendingImportEffect(ledger, batch, rowId, fixture.Now).IsSuccess);
        target = Saved(target, new DeclaredApplication(fixture.Tenant, target.Id));

        // Act
        var result = ledger.VerifyPendingEffects(batch, ledger.GetRevision(batch),
            new Dictionary<Uuid, DeclaredApplication> { [target.Id] = target });

        // Assert
        Assert.Equal(savedPlan, result.IsSuccess);
        Assert.Equal(5, ledger.GetRevision(batch));
        Assert.True(ledger.CommittedStreamPosition > (ulong)ledger.GetRevision(batch));
        if (!savedPlan)
            Assert.True(Assert.IsType<RequestError>(result.Error).IsTransient);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldVerifyEveryFrozenRowGivenDurableExactEffects(bool sharedTarget)
    {
        // Arrange
        var fixture = Durable(sharedTarget);
        var before = fixture.Ledger.CommittedStreamPosition;

        // Act
        var result = fixture.Ledger.VerifyPendingEffects(fixture.Batch, fixture.Ledger.GetRevision(fixture.Batch), fixture.Targets);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.Equal(fixture.Batch.GetRows().Select(row => row.RowId), result.Value.Select(ev => ev.Row.RowId));
        Assert.All(result.Value, ev => Assert.Equal(fixture.Ledger.GetFrozenPlan(fixture.Batch.Id)!.PlanSha256, ev.PlanSha256));
        Assert.Equal(before, fixture.Ledger.CommittedStreamPosition);
        Assert.Empty(new AggregateScenario<ApplicationImportLedger>(fixture.Ledger).PendingEvents);
        Assert.All(fixture.Targets.Values, target => Assert.Equal(target.IsCreated ? 1 : 0, target.Revision));
        var list = Assert.IsAssignableFrom<IList<ApplicationImportEffectPending>>(result.Value);
        Assert.Throws<NotSupportedException>(() => list.Clear());
    }

    [Theory]
    [InlineData("canceled", RequestErrorKind.Conflict)]
    [InlineData("stale", RequestErrorKind.Conflict)]
    [InlineData("revised", RequestErrorKind.Conflict)]
    [InlineData("retired", RequestErrorKind.Conflict)]
    [InlineData("tenant", RequestErrorKind.NotFound)]
    [InlineData("source", RequestErrorKind.NotFound)]
    [InlineData("foreign_target", RequestErrorKind.NotFound)]
    [InlineData("wrong_target", RequestErrorKind.NotFound)]
    public void ShouldRejectCommitVerificationGivenChangedAuthorityOrTarget(string problem, RequestErrorKind kind)
    {
        // Arrange
        var fixture = Durable();
        var target = fixture.Targets.Values.Single(target => target.IsCreated);
        if (problem == "canceled")
            Assert.Null(fixture.Ledger.Cancel(fixture.Batch, fixture.Ledger.GetRevision(fixture.Batch), "Canceled",
                fixture.Actor, "Lead", fixture.Now));
        if (problem == "revised")
            Assert.Null(target.Revise(1, "Manual revision", "Governed", null, fixture.Actor, "Lead", fixture.Now));
        if (problem == "retired")
            Assert.Null(target.Retire(1, fixture.Now, "Manual retirement", null, fixture.Actor, "Lead", fixture.Now));
        if (problem == "foreign_target")
            fixture.Targets[target.Id] = new DeclaredApplication(Uuid.CreateVersion4(), target.Id);
        if (problem == "wrong_target")
            fixture.Targets[target.Id] = new DeclaredApplication(fixture.Tenant, Uuid.CreateVersion4());
        var ledger = problem == "tenant" ? new ApplicationImportLedger(Uuid.CreateVersion4(), "manual", "applications") :
            problem == "source" ? new ApplicationImportLedger(fixture.Tenant, "other", "applications") : fixture.Ledger;
        var revision = problem == "stale" ? 1 : fixture.Ledger.GetRevision(fixture.Batch);
        var before = new AggregateScenario<ApplicationImportLedger>(fixture.Ledger).PendingEvents.Count;

        // Act
        var result = ledger.VerifyPendingEffects(fixture.Batch, revision, fixture.Targets);

        // Assert
        Assert.False(result.IsSuccess);
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(kind, error.Kind);
        Assert.False(error.IsTransient);
        Assert.Equal(before, new AggregateScenario<ApplicationImportLedger>(fixture.Ledger).PendingEvents.Count);
    }

    [Theory]
    [InlineData("source_content")]
    [InlineData("approver")]
    [InlineData("source")]
    [InlineData("hash")]
    public void ShouldRejectEffectProofGivenDurablePayloadDifferentFromPlan(string difference)
    {
        // Arrange
        var fixture = Durable();
        var row = fixture.Ledger.GetFrozenPlan(fixture.Batch.Id)!.Rows.Single(row => row.Decision == "create_new");
        var original = fixture.Targets[row.ApplicationId].GetPendingImportEffect(fixture.Batch.Id, row.RowId)!;
        var altered = difference switch
        {
            "source_content" => original with { Row = original.Row with { Name = "Altered observation" } },
            "approver" => original with { Plan = original.Plan with { ApproverMemberId = Uuid.CreateVersion4() } },
            "source" => original with { Plan = original.Plan with { SourceKey = "other" } },
            _ => original with { PlanSha256 = new string('a', 64) },
        };
        var target = new DeclaredApplication(fixture.Tenant, row.ApplicationId);
        _ = new AggregateScenario<DeclaredApplication>(target).Given(altered);
        fixture.Targets[target.Id] = target;

        // Act
        var result = fixture.Ledger.VerifyPendingEffects(fixture.Batch, fixture.Ledger.GetRevision(fixture.Batch), fixture.Targets);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.False(Assert.IsType<RequestError>(result.Error).IsTransient);
        Assert.Empty(new AggregateScenario<ApplicationImportLedger>(fixture.Ledger).PendingEvents);
    }

    [Fact]
    public void ShouldRejectWholeProofGivenDurableFirstEffectAndMissingLastEffect()
    {
        // Arrange
        var fixture = Durable();
        var last = fixture.Ledger.GetFrozenPlan(fixture.Batch.Id)!.Rows[^1];
        fixture.Targets.Remove(last.ApplicationId);

        // Act
        var result = fixture.Ledger.VerifyPendingEffects(fixture.Batch, fixture.Ledger.GetRevision(fixture.Batch), fixture.Targets);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(Assert.IsType<RequestError>(result.Error).IsTransient);
        Assert.Empty(new AggregateScenario<ApplicationImportLedger>(fixture.Ledger).PendingEvents);
    }

    static Fixture Durable(bool sharedTarget = false)
    {
        var tenant = Uuid.CreateVersion4();
        var actor = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var request = new StageApplicationImport(tenant, Uuid.CreateVersion4(), "manual", "applications", "partial",
            [new("app-1", "Payroll", "Pay staff", null), new("app-2", "CRM", "Manage customers", null)]);
        var staged = new ImportBatch(tenant, ImportBatch.BatchIdFor(request));
        Assert.True(staged.Stage(request, actor, "Contributor", now).IsSuccess);
        var batch = Saved(staged, new ImportBatch(tenant, staged.Id));
        var ledger = new ApplicationImportLedger(tenant, "manual", "applications");
        var manual = new DeclaredApplication(tenant, Uuid.CreateVersion4());
        Assert.True(manual.Declare("Manual", "Governed", null, actor, "Lead", now).IsSuccess);
        manual = Saved(manual, new DeclaredApplication(tenant, manual.Id));
        foreach (var row in batch.GetRows())
        {
            var link = sharedTarget || row.RowNumber == 2;
            Assert.Null(ledger.Correlate(batch, new CorrelateApplicationImportRow(tenant, batch.Id, row.RowId,
                ledger.GetRevision(batch), link ? "link_existing" : "create_new", link ? manual.Id : null,
                link ? 1 : null, "Reviewed"), link ? manual : null, actor, "Lead", now));
        }
        Assert.True(ledger.BeginAcceptance(batch, 3, new Dictionary<Uuid, DeclaredApplication> { [manual.Id] = manual }, actor, "Lead", now).IsSuccess);
        var history = new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents.ToArray();
        ledger = Saved(ledger, new ApplicationImportLedger(tenant, "manual", "applications"));
        var targets = new Dictionary<Uuid, DeclaredApplication> { [manual.Id] = manual };
        foreach (var row in ledger.GetFrozenPlan(batch.Id)!.Rows)
        {
            if (!targets.TryGetValue(row.ApplicationId, out var target))
            {
                target = new DeclaredApplication(tenant, row.ApplicationId);
                targets.Add(row.ApplicationId, target);
            }
            Assert.True(target.RecordPendingImportEffect(ledger, batch, row.RowId, now).IsSuccess);
        }
        foreach (var target in targets.Values.ToArray())
        {
            var fresh = new DeclaredApplication(tenant, target.Id);
            var pending = new AggregateScenario<DeclaredApplication>(target).PendingEvents.ToArray();
            if (target.IsCreated)
            {
                Assert.True(fresh.Declare("Manual", "Governed", null, actor, "Lead", now).IsSuccess);
                fresh = Saved(fresh, new DeclaredApplication(tenant, target.Id));
            }
            _ = new AggregateScenario<DeclaredApplication>(fresh).Given(pending);
            targets[target.Id] = fresh;
        }
        return new Fixture(tenant, actor, now, batch, ledger, targets, history);
    }

    static T Saved<T>(T source, T fresh) where T : Aggregate
    {
        _ = new AggregateScenario<T>(fresh).Given(new AggregateScenario<T>(source).PendingEvents.ToArray());
        return fresh;
    }

    sealed record Fixture(Uuid Tenant, Uuid Actor, DateTimeOffset Now, ImportBatch Batch,
        ApplicationImportLedger Ledger, Dictionary<Uuid, DeclaredApplication> Targets, IReadOnlyList<DomainEvent> History);

    [Theory]
    [InlineData("missing_target")]
    [InlineData("missing_effect")]
    [InlineData("unsaved_effect")]
    [InlineData("unsaved_plan")]
    [InlineData("unsaved_batch")]
    public void ShouldRejectVerificationGivenNonDurablePlanOrEffect(string problem)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var actor = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var request = new StageApplicationImport(tenant, Uuid.CreateVersion4(), "manual", "applications", "partial",
            [new("app-1", "Payroll", "Pay staff", null)]);
        var batch = new ImportBatch(tenant, ImportBatch.BatchIdFor(request));
        Assert.True(batch.Stage(request, actor, "Contributor", now).IsSuccess);
        if (problem != "unsaved_batch")
        {
            var saved = new ImportBatch(tenant, batch.Id);
            _ = new AggregateScenario<ImportBatch>(saved).Given(new AggregateScenario<ImportBatch>(batch).PendingEvents.ToArray());
            batch = saved;
        }
        var ledger = new ApplicationImportLedger(tenant, "manual", "applications");
        var rowId = batch.GetRows()[0].RowId;
        Assert.Null(ledger.Correlate(batch, new CorrelateApplicationImportRow(tenant, batch.Id,
            rowId, 1, "create_new", null, null, "Reviewed"), null, actor, "Lead", now));
        Assert.True(ledger.BeginAcceptance(batch, 2, new Dictionary<Uuid, DeclaredApplication>(), actor, "Lead", now).IsSuccess);
        if (problem != "unsaved_plan")
        {
            var saved = new ApplicationImportLedger(tenant, "manual", "applications");
            _ = new AggregateScenario<ApplicationImportLedger>(saved).Given(
                new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents.ToArray());
            ledger = saved;
        }
        var target = new DeclaredApplication(tenant, ledger.GetFrozenPlan(batch.Id)!.Rows[0].ApplicationId);
        if (problem != "missing_effect")
            Assert.True(target.RecordPendingImportEffect(ledger, batch, rowId, now).IsSuccess);
        if (problem == "unsaved_plan")
        {
            var saved = new DeclaredApplication(tenant, target.Id);
            _ = new AggregateScenario<DeclaredApplication>(saved).Given(
                new AggregateScenario<DeclaredApplication>(target).PendingEvents.ToArray());
            target = saved;
        }
        IReadOnlyDictionary<Uuid, DeclaredApplication> targets = problem == "missing_target" ?
            [] : new Dictionary<Uuid, DeclaredApplication> { [target.Id] = target };

        // Act
        var result = ledger.VerifyPendingEffects(batch, ledger.GetRevision(batch), targets);

        // Assert
        Assert.False(result.IsSuccess);
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
        Assert.DoesNotContain(new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents, ev => ev is not
            (ApplicationImportRowCorrelated or ApplicationImportPlanStarted or ApplicationImportPlanRowFrozen or ApplicationImportPlanSealed));
    }
}
