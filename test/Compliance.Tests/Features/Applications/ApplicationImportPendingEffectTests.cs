using Bdgrz.Compliance.Features.Applications;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using Cntryl.Fitz.Testing;
using System.Text.Json;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationImportPendingEffectTests
{
    [Fact]
    public void ShouldRejectOversizedCombinedEffectGivenIndividuallyBoundedFrozenRecords()
    {
        // Arrange
        var fixture = Prepare(nearPayloadLimit: true);

        // Act
        var result = fixture.Target.RecordPendingImportEffect(fixture.Ledger, fixture.Batch, fixture.RowId, fixture.Now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(new AggregateScenario<DeclaredApplication>(fixture.Target).PendingEvents);
        Assert.False(fixture.Target.IsCreated);
        Assert.Equal(0, fixture.Target.Revision);
    }

    [Fact]
    public void ShouldKeepLateEffectInvisibleGivenCancellationAfterAuthoritySnapshot()
    {
        // Arrange
        var fixture = Prepare();
        var captured = new ApplicationImportLedger(fixture.Tenant, "manual", "applications");
        _ = new AggregateScenario<ApplicationImportLedger>(captured).Given(
            new AggregateScenario<ApplicationImportLedger>(fixture.Ledger).PendingEvents.ToArray());
        Assert.Null(fixture.Ledger.Cancel(fixture.Batch, fixture.Ledger.GetRevision(fixture.Batch),
            "Canceled after authorization", fixture.Approver, "Lead", fixture.Now));

        // Act
        var late = fixture.Target.RecordPendingImportEffect(captured, fixture.Batch, fixture.RowId, fixture.Now);
        var retry = fixture.Target.RecordPendingImportEffect(fixture.Ledger, fixture.Batch, fixture.RowId, fixture.Now);

        // Assert
        Assert.True(late.IsSuccess);
        Assert.False(retry.IsSuccess);
        Assert.Equal("canceled", fixture.Ledger.GetState(fixture.Batch));
        Assert.False(fixture.Target.IsCreated);
        Assert.Equal(0, fixture.Target.Revision);
        Assert.Single(new AggregateScenario<DeclaredApplication>(fixture.Target).PendingEvents);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldExcludePendingEffectGivenInventoryAndHistoryProjection(bool link)
    {
        // Arrange
        var fixture = Prepare(link: link);
        Assert.True(fixture.Target.RecordPendingImportEffect(fixture.Ledger, fixture.Batch, fixture.RowId, fixture.Now).IsSuccess);
        var directory = new FitzApplicationDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity("ApplicationDirectoryV2", EventStreamPattern.ForPattern(fixture.Tenant.ToString()));
        await using (var projection = await directory.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            foreach (var ev in new AggregateScenario<DeclaredApplication>(fixture.Target).PendingEvents)
                await directory.ApplyAsync(ev);
            await projection.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var view = await directory.GetAsync(fixture.Tenant, fixture.Target.Id);
        var list = await directory.ListAsync(fixture.Tenant, 50, null);
        var history = await directory.ListRevisionsAsync(fixture.Tenant, fixture.Target.Id, 50, null);
        var revision = await directory.GetRevisionAsync(fixture.Tenant, fixture.Target.Id, 1);

        // Assert
        Assert.Equal(link, view is not null);
        Assert.Equal(link ? 1 : 0, list.Items.Count);
        Assert.Equal(link, revision is not null);
        if (link)
        {
            Assert.NotNull(history);
            Assert.Single(history.Items);
            Assert.Equal("Manual name", view!.Name);
            Assert.Equal("Governed purpose", view.Purpose);
            Assert.Equal("Manual owner", view.OwnerReference);
            Assert.Equal(1, view.Revision);
        }
        else
            Assert.Null(history);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("target")]
    [InlineData("row_target")]
    [InlineData("decision")]
    [InlineData("revision")]
    [InlineData("nonconsecutive_revision")]
    [InlineData("hash")]
    [InlineData("source")]
    [InlineData("approver")]
    [InlineData("submitter")]
    [InlineData("duplicate")]
    public void ShouldRejectCorruptEffectGivenInvalidReplay(string corruption)
    {
        // Arrange
        var fixture = Prepare();
        Assert.True(fixture.Target.RecordPendingImportEffect(fixture.Ledger, fixture.Batch, fixture.RowId, fixture.Now).IsSuccess);
        var effect = Assert.IsType<ApplicationImportEffectPending>(Assert.Single(
            new AggregateScenario<DeclaredApplication>(fixture.Target).PendingEvents));
        var altered = corruption switch
        {
            "tenant" => effect with { TenantId = Uuid.CreateVersion4() },
            "target" => effect with { ApplicationId = Uuid.CreateVersion4() },
            "row_target" => effect with { Row = effect.Row with { ApplicationId = Uuid.CreateVersion4() } },
            "decision" => effect with { Row = effect.Row with { Decision = "overwrite" } },
            "revision" => effect with { PlanRevision = effect.Plan.Revision },
            "nonconsecutive_revision" => effect with { PlanRevision = effect.PlanRevision + 1 },
            "hash" => effect with { PlanSha256 = new string('z', 64) },
            "source" => effect with { Plan = effect.Plan with { SourceKey = " " } },
            "approver" => effect with { Plan = effect.Plan with { ApproverMemberId = Uuid.Empty } },
            "submitter" => effect with { Plan = effect.Plan with { SubmitterMemberId = Uuid.Empty } },
            _ => effect,
        };
        var replay = new DeclaredApplication(fixture.Tenant, fixture.Target.Id);

        // Act
        var exception = Record.Exception(() => new AggregateScenario<DeclaredApplication>(replay)
            .Given(corruption == "duplicate" ? [effect, altered] : [altered]));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
        Assert.False(replay.IsCreated);
        Assert.Equal(0, replay.Revision);
    }

    [Fact]
    public void ShouldPreserveFrozenContentGivenJsonRoundTrip()
    {
        // Arrange
        var fixture = Prepare();
        Assert.True(fixture.Target.RecordPendingImportEffect(fixture.Ledger, fixture.Batch, fixture.RowId, fixture.Now).IsSuccess);
        var effect = Assert.IsType<ApplicationImportEffectPending>(Assert.Single(
            new AggregateScenario<DeclaredApplication>(fixture.Target).PendingEvents));

        // Act
        var bytes = JsonSerializer.SerializeToUtf8Bytes(effect, ComplianceCoreJsonContext.Default.ApplicationImportEffectPending);
        var roundTrip = JsonSerializer.Deserialize(bytes, ComplianceCoreJsonContext.Default.ApplicationImportEffectPending);
        var replay = new DeclaredApplication(fixture.Tenant, fixture.Target.Id);
        _ = new AggregateScenario<DeclaredApplication>(replay).Given(roundTrip!);

        // Assert
        Assert.True(bytes.Length <= ImportBatch.MaximumStagedPayloadBytes);
        Assert.Equal(effect, roundTrip);
        Assert.Equal(effect, replay.GetPendingImportEffect(fixture.Batch.Id, fixture.RowId));
        Assert.False(replay.IsCreated);
        Assert.Equal(0, replay.Revision);
    }

    [Theory]
    [InlineData("system", true)]
    [InlineData("user", false)]
    [InlineData("anonymous", false)]
    [InlineData("mixed", false)]
    [InlineData("canceled", false)]
    [InlineData("unfrozen", false)]
    [InlineData("wrong_tenant", false)]
    [InlineData("wrong_target", false)]
    [InlineData("wrong_row", false)]
    public async Task ShouldRequireTrustedSystemAndExactPlanGivenEffectDispatch(string condition, bool success)
    {
        // Arrange
        var fixture = Prepare(freeze: condition != "unfrozen");
        if (condition == "canceled")
            Assert.Null(fixture.Ledger.Cancel(fixture.Batch, fixture.Ledger.GetRevision(fixture.Batch),
                "Canceled", fixture.Approver, "Lead", fixture.Now));
        var request = new ApplyApplicationImportEffect(condition == "wrong_tenant" ? Uuid.CreateVersion4() : fixture.Tenant,
            fixture.Batch.Id, condition == "wrong_row" ? Uuid.CreateVersion4() : fixture.RowId,
            condition == "wrong_target" ? Uuid.CreateVersion4() : fixture.Target.Id);
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", fixture.Approver.ToString())], "BdgrzSession"));
        var actor = condition == "user" ? user : condition == "anonymous" ? RequestActor.Anonymous :
            condition == "mixed" ? new ClaimsPrincipal(RequestActor.System.Identities.Concat(user.Identities)) : RequestActor.System;
        var context = new RequestContext<ApplyApplicationImportEffect>(request, actor);
        var authorizer = new ApplyApplicationImportEffectAuthorizer(new SourceReader(fixture.Batch, fixture.Ledger));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(success, result.IsSuccess);
        Assert.False(typeof(ICallable).IsAssignableFrom(typeof(ApplyApplicationImportEffect)));
        Assert.False(typeof(IApplicationInventoryRequest).IsAssignableFrom(typeof(ApplyApplicationImportEffect)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldPersistOneInvisibleEffectGivenDispatchAndRetry(bool link)
    {
        // Arrange
        var fixture = Prepare(link: link);
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddSingleton(TimeProvider.System);
        services.AddPortia().AddRequestHandler<ApplyApplicationImportEffectHandler>()
            .AddRequestAuthorizer<ApplyApplicationImportEffectAuthorizer>();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var context = new RequestContext<ApplyApplicationImportEffect>(new ApplyApplicationImportEffect(
            fixture.Tenant, fixture.Batch.Id, fixture.RowId, fixture.Target.Id), RequestActor.System);
        await writer.SaveAsync(fixture.Batch, context);
        await writer.SaveAsync(fixture.Ledger, context);
        if (link)
            await writer.SaveAsync(fixture.Target, context);
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();

        // Act
        await RequestScenario.For(provider).GivenActor(RequestActor.System).When(context.Request).ExpectSuccess();
        await RequestScenario.For(provider).GivenActor(RequestActor.System).When(context.Request).ExpectSuccess();
        var persisted = await reader.HydrateAsync(new DeclaredApplication(fixture.Tenant, fixture.Target.Id));

        // Assert
        Assert.NotNull(persisted.GetPendingImportEffect(fixture.Batch.Id, fixture.RowId));
        Assert.Equal(link, persisted.IsCreated);
        Assert.Equal(link ? 1 : 0, persisted.Revision);
        Assert.False(persisted.IsRetired);
        var store = scope.ServiceProvider.GetRequiredService<IEventStore>();
        var effects = new List<ApplicationImportEffectPending>();
        await foreach (var record in store.ReadAsync(persisted.Stream, 0, CancellationToken.None))
            if (record.Event is ApplicationImportEffectPending effect)
                effects.Add(effect);
        Assert.Single(effects);
    }

    sealed class SourceReader(params Aggregate[] sources) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate => ValueTask.FromResult(
            (TAggregate)(sources.SingleOrDefault(source => source.Stream == aggregate.Stream) ?? aggregate));
    }

    [Theory]
    [InlineData("canceled", RequestErrorKind.Conflict)]
    [InlineData("unfrozen", RequestErrorKind.Conflict)]
    [InlineData("wrong_row", RequestErrorKind.NotFound)]
    [InlineData("wrong_target", RequestErrorKind.NotFound)]
    [InlineData("wrong_tenant", RequestErrorKind.NotFound)]
    [InlineData("wrong_source", RequestErrorKind.NotFound)]
    public void ShouldRejectEffectGivenNoMatchingActiveAuthority(string problem, RequestErrorKind expected)
    {
        // Arrange
        var fixture = Prepare(freeze: problem != "unfrozen");
        if (problem == "canceled")
            Assert.Null(fixture.Ledger.Cancel(fixture.Batch, fixture.Ledger.GetRevision(fixture.Batch),
                "Canceled", fixture.Approver, "Lead", fixture.Now));
        var target = problem == "wrong_target" ? new DeclaredApplication(fixture.Tenant, Uuid.CreateVersion4()) :
            problem == "wrong_tenant" ? new DeclaredApplication(Uuid.CreateVersion4(), fixture.Target.Id) : fixture.Target;
        var ledger = problem == "wrong_source" ? new ApplicationImportLedger(fixture.Tenant, "another", "applications") : fixture.Ledger;

        // Act
        var result = target.RecordPendingImportEffect(ledger, fixture.Batch,
            problem == "wrong_row" ? Uuid.CreateVersion4() : fixture.RowId, fixture.Now);

        // Assert
        Assert.Equal(expected, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(new AggregateScenario<DeclaredApplication>(target).PendingEvents);
    }

    [Fact]
    public void ShouldPreserveAttributionAndReserveIdentityGivenRetryAndReplay()
    {
        // Arrange
        var fixture = Prepare();
        Assert.True(fixture.Target.RecordPendingImportEffect(fixture.Ledger, fixture.Batch, fixture.RowId, fixture.Now).IsSuccess);
        var events = new AggregateScenario<DeclaredApplication>(fixture.Target).PendingEvents.ToArray();
        var replay = new DeclaredApplication(fixture.Tenant, fixture.Target.Id);
        _ = new AggregateScenario<DeclaredApplication>(replay).Given(events);

        // Act
        var retry = replay.RecordPendingImportEffect(fixture.Ledger, fixture.Batch, fixture.RowId, fixture.Now.AddDays(1));
        var declaration = replay.Declare("Hijacked", "Manual declaration", null, fixture.Approver, "Lead", fixture.Now);
        var effect = replay.GetPendingImportEffect(fixture.Batch.Id, fixture.RowId);

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(declaration.Error).Kind);
        Assert.Empty(new AggregateScenario<DeclaredApplication>(replay).PendingEvents);
        Assert.NotNull(effect);
        Assert.Equal(fixture.Submitter, effect.Plan.SubmitterMemberId);
        Assert.Equal(fixture.Approver, effect.Plan.ApproverMemberId);
        Assert.Equal("system_process", effect.StoredActor.Kind);
        Assert.Equal("reactor:application-import", effect.StoredActor.Id);
        Assert.Equal(fixture.Now, effect.RecordedAt);
        Assert.False(replay.IsCreated);
        Assert.Equal(0, replay.Revision);
    }

    [Theory]
    [InlineData("unchanged", true)]
    [InlineData("revised", false)]
    [InlineData("retired", false)]
    [InlineData("missing", false)]
    public void ShouldRecheckLinkedTargetGivenChangeAfterPlanFreeze(string state, bool expectedSuccess)
    {
        // Arrange
        var fixture = Prepare(link: true);
        if (state == "revised")
            Assert.Null(fixture.Target.Revise(1, "Manual revision", "Governed purpose", null, fixture.Approver, "Lead", fixture.Now));
        if (state == "retired")
            Assert.Null(fixture.Target.Retire(1, fixture.Now, "Manual retirement", null, fixture.Approver, "Lead", fixture.Now));
        var target = state == "missing" ? new DeclaredApplication(fixture.Tenant, fixture.Target.Id) : fixture.Target;
        var before = new AggregateScenario<DeclaredApplication>(target).PendingEvents.Count;
        var revision = target.Revision;

        // Act
        var result = target.RecordPendingImportEffect(fixture.Ledger, fixture.Batch, fixture.RowId, fixture.Now);

        // Assert
        Assert.Equal(expectedSuccess, result.IsSuccess);
        Assert.Equal(before + (expectedSuccess ? 1 : 0), new AggregateScenario<DeclaredApplication>(target).PendingEvents.Count);
        Assert.Equal(revision, target.Revision);
        Assert.Equal(state == "retired", target.IsRetired);
    }

    [Fact]
    public void ShouldKeepCanceledEffectInvisibleGivenPreviouslyDurablePendingRecord()
    {
        // Arrange
        var fixture = Prepare();
        Assert.True(fixture.Target.RecordPendingImportEffect(fixture.Ledger, fixture.Batch, fixture.RowId, fixture.Now).IsSuccess);
        Assert.Null(fixture.Ledger.Cancel(fixture.Batch, fixture.Ledger.GetRevision(fixture.Batch),
            "Canceled", fixture.Approver, "Lead", fixture.Now));

        // Act
        var retry = fixture.Target.RecordPendingImportEffect(fixture.Ledger, fixture.Batch, fixture.RowId, fixture.Now);

        // Assert
        Assert.False(retry.IsSuccess);
        Assert.NotNull(fixture.Target.GetPendingImportEffect(fixture.Batch.Id, fixture.RowId));
        Assert.False(fixture.Target.IsCreated);
        Assert.Equal(0, fixture.Target.Revision);
        Assert.Single(new AggregateScenario<DeclaredApplication>(fixture.Target).PendingEvents);
    }

    static Fixture Prepare(bool link = false, bool freeze = true, bool nearPayloadLimit = false)
    {
        var tenant = Uuid.CreateVersion4();
        var submitter = Uuid.CreateVersion4();
        var approver = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var request = new StageApplicationImport(tenant, Uuid.CreateVersion4(), "manual", "applications", "partial",
            [new("app-1", "Source name", nearPayloadLimit ? new string('a', 2000) : "Source purpose",
                nearPayloadLimit ? new string('b', 2000) : "Source owner")]);
        var batch = new ImportBatch(tenant, ImportBatch.BatchIdFor(request));
        Assert.True(batch.Stage(request, submitter, nearPayloadLimit ? new string('c', 22500) : "Contributor", now).IsSuccess);
        var rowId = batch.GetRows()[0].RowId;
        var target = new DeclaredApplication(tenant, link ? Uuid.CreateVersion4() :
            Uuid.CreateVersion5(batch.Id, $"application_import_target:{rowId}"));
        if (link)
            Assert.True(target.Declare("Manual name", "Governed purpose", "Manual owner", approver, "Lead", now).IsSuccess);
        var ledger = new ApplicationImportLedger(tenant, "manual", "applications");
        Assert.Null(ledger.Correlate(batch, new CorrelateApplicationImportRow(tenant, batch.Id, rowId, 1,
            link ? "link_existing" : "create_new", link ? target.Id : null, link ? 1 : null, "Reviewed"),
            link ? target : null, approver, "Lead", now));
        if (freeze)
            Assert.True(ledger.BeginAcceptance(batch, 2, link ? new Dictionary<Uuid, DeclaredApplication> { [target.Id] = target } :
                [], approver, nearPayloadLimit ? new string('d', 22500) : "Lead", now).IsSuccess);
        return new Fixture(tenant, submitter, approver, now, batch, ledger, rowId, target);
    }

    sealed record Fixture(Uuid Tenant, Uuid Submitter, Uuid Approver, DateTimeOffset Now,
        ImportBatch Batch, ApplicationImportLedger Ledger, Uuid RowId, DeclaredApplication Target);

    [Fact]
    public void ShouldPersistInvisibleEffectGivenFrozenNewTarget()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var actor = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var request = new StageApplicationImport(tenant, Uuid.CreateVersion4(), "manual", "applications", "partial",
            [new("app-1", "Payroll", "Pay staff", null)]);
        var batch = new ImportBatch(tenant, ImportBatch.BatchIdFor(request));
        Assert.True(batch.Stage(request, actor, "Contributor", now).IsSuccess);
        var ledger = new ApplicationImportLedger(tenant, "manual", "applications");
        var rowId = batch.GetRows()[0].RowId;
        Assert.Null(ledger.Correlate(batch, new CorrelateApplicationImportRow(tenant, batch.Id,
            rowId, 1, "create_new", null, null, "Reviewed"), null, actor, "Lead", now));
        Assert.True(ledger.BeginAcceptance(batch, 2, new Dictionary<Uuid, DeclaredApplication>(), actor, "Lead", now).IsSuccess);
        var target = new DeclaredApplication(tenant, ledger.GetFrozenPlan(batch.Id)!.Rows[0].ApplicationId);

        // Act
        var result = target.RecordPendingImportEffect(ledger, batch, rowId, now);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(new AggregateScenario<DeclaredApplication>(target).PendingEvents);
        Assert.False(target.IsCreated);
        Assert.False(target.IsRetired);
        Assert.Equal(0, target.Revision);
    }
}
