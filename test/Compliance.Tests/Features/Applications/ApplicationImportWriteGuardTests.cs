using Bdgrz.Compliance.Features.Applications;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using Bdgrz.Compliance.Features.Versioning;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationImportWriteGuardTests
{
    [Theory]
    [InlineData("revise")]
    [InlineData("retire")]
    public async Task ShouldReleaseTargetReservationGivenDurableCommit(string change)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        Assert.True(fixture.Ledger.Commit(fixture.Batch, 5,
            new Dictionary<Uuid, DeclaredApplication> { [fixture.Target.Id] = fixture.Target }, fixture.Now).IsSuccess);
        var blocked = await fixture.ChangeAsync(change);
        Assert.False(blocked.IsSuccess);
        await fixture.Writer.SaveAsync(fixture.Ledger, fixture.Context);

        // Act
        var result = await fixture.ChangeAsync(change);
        var target = await fixture.Reader.HydrateApplicationAsync(fixture.Tenant, fixture.Target.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(target.IsCreated);
        Assert.Equal(2, target.Revision);
        Assert.Equal(change == "retire", target.IsRetired);
        Assert.Null(target.CheckPendingImportChanges());
    }

    [Theory]
    [InlineData("revise")]
    [InlineData("retire")]
    public async Task ShouldRejectStaleEffectGivenManualChangeWinsBeforeReservation(string change)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(persistEffect: false);

        // Act
        var manual = await fixture.ChangeAsync(change);
        var appendConflict = await Record.ExceptionAsync(() => fixture.Writer.SaveAsync(fixture.Target, fixture.Context).AsTask());
        var retry = await new ApplyApplicationImportEffectHandler(
            fixture.Scope.ServiceProvider.GetRequiredService<IAggregateExecutor>(), fixture.Reader, TimeProvider.System)
            .HandleAsync(fixture.Context, CancellationToken.None);
        var target = await fixture.Reader.HydrateAsync(new DeclaredApplication(fixture.Tenant, fixture.Target.Id));
        var proof = fixture.Ledger.VerifyPendingEffects(fixture.Batch, 5,
            new Dictionary<Uuid, DeclaredApplication> { [target.Id] = target });

        // Assert
        Assert.True(manual.IsSuccess);
        Assert.IsType<EventStreamConcurrencyException>(appendConflict);
        Assert.False(retry.IsSuccess);
        Assert.False(proof.IsSuccess);
        Assert.Equal(2, target.Revision);
        Assert.Equal(change == "retire", target.IsRetired);
        Assert.Empty(target.GetPendingImportEffects());
    }

    [Theory]
    [InlineData("revise")]
    [InlineData("retire")]
    public async Task ShouldReleaseTargetReservationGivenDurableCancellation(string change)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var blocked = await fixture.ChangeAsync(change);
        Assert.False(blocked.IsSuccess);
        Assert.True(Assert.IsType<RequestError>(blocked.Error).IsTransient);
        Assert.Null(fixture.Ledger.Cancel(fixture.Batch, fixture.Ledger.GetRevision(fixture.Batch),
            "Canceled", fixture.Member, "Lead", fixture.Now));
        Assert.False(fixture.Ledger.IsCancellationDurable(fixture.Batch.Id));
        await fixture.Writer.SaveAsync(fixture.Ledger, fixture.Context);

        // Act
        var result = await fixture.ChangeAsync(change);
        var target = await fixture.Reader.HydrateAsync(new DeclaredApplication(fixture.Tenant, fixture.Target.Id));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(fixture.Ledger.IsCancellationDurable(fixture.Batch.Id));
        Assert.Equal(2, target.Revision);
        Assert.Equal(change == "retire", target.IsRetired);
        Assert.Single(target.GetPendingImportEffects());
        Assert.Equal(3UL, target.CommittedStreamPosition);
    }

    [Theory]
    [InlineData("revise", 1)]
    [InlineData("retire", 1)]
    [InlineData("revise", 2)]
    [InlineData("retire", 2)]
    public async Task ShouldPreventTargetChangeGivenEffectArrivingAcrossHydrationRace(string change, int phase)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(persistEffect: false);
        var racing = new EffectArrivalReader(fixture.Reader, fixture.Writer, fixture.Target, fixture.Context, phase);
        var executor = new AggregateExecutor(racing, fixture.Writer);
        Result result = default;

        // Act
        var exception = await Record.ExceptionAsync(async () => result = await fixture.ChangeAsync(change, racing, executor));
        var target = await fixture.Reader.HydrateAsync(new DeclaredApplication(fixture.Tenant, fixture.Target.Id));

        // Assert
        if (phase == 1)
        {
            Assert.Null(exception);
            Assert.False(result.IsSuccess);
            Assert.True(Assert.IsType<RequestError>(result.Error).IsTransient);
        }
        else
            Assert.IsType<EventStreamConcurrencyException>(exception);
        Assert.Equal(1, target.Revision);
        Assert.False(target.IsRetired);
        Assert.Single(target.GetPendingImportEffects());
        Assert.Equal(2UL, target.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldKeepReservationGivenUnsavedCancellationSnapshot()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        Assert.Null(fixture.Ledger.Cancel(fixture.Batch, fixture.Ledger.GetRevision(fixture.Batch),
            "Unsaved cancellation", fixture.Member, "Lead", fixture.Now));
        var reader = new UnsavedLedgerReader(fixture.Reader, fixture.Ledger);

        // Act
        var prepared = await ApplicationImportWriteGuard.PrepareAsync(reader, fixture.Tenant, fixture.Target.Id, CancellationToken.None);
        var target = await fixture.Reader.HydrateAsync(prepared);

        // Assert
        Assert.NotNull(target.CheckPendingImportChanges());
        Assert.False(fixture.Ledger.IsCancellationDurable(fixture.Batch.Id));
        Assert.Equal(1, target.Revision);
    }

    [Fact]
    public async Task ShouldReleaseOnlyMatchingReservationGivenTwoSourceBatches()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var request = new StageApplicationImport(fixture.Tenant, Uuid.CreateVersion4(), "another", "applications", "partial",
            [new("app-2", "Other source", "Other observation", null)]);
        var batch = new ImportBatch(fixture.Tenant, ImportBatch.BatchIdFor(request));
        Assert.True(batch.Stage(request, fixture.Member, "Contributor", fixture.Now).IsSuccess);
        await fixture.Writer.SaveAsync(batch, fixture.Context);
        var ledger = new ApplicationImportLedger(fixture.Tenant, "another", "applications");
        var row = batch.GetRows()[0];
        Assert.Null(ledger.Correlate(batch, new CorrelateApplicationImportRow(fixture.Tenant, batch.Id, row.RowId,
            1, "link_existing", fixture.Target.Id, 1, "Reviewed"), fixture.Target, fixture.Member, "Lead", fixture.Now));
        Assert.True(ledger.BeginAcceptance(batch, 2, new Dictionary<Uuid, DeclaredApplication> { [fixture.Target.Id] = fixture.Target },
            fixture.Member, "Lead", fixture.Now).IsSuccess);
        await fixture.Writer.SaveAsync(ledger, fixture.Context);
        Assert.True(fixture.Target.RecordPendingImportEffect(ledger, batch, row.RowId, fixture.Now).IsSuccess);
        await fixture.Writer.SaveAsync(fixture.Target, fixture.Context);
        Assert.Null(fixture.Ledger.Cancel(fixture.Batch, 5, "Canceled first", fixture.Member, "Lead", fixture.Now));
        await fixture.Writer.SaveAsync(fixture.Ledger, fixture.Context);

        // Act
        var stillBlocked = await fixture.ChangeAsync("revise");
        Assert.Null(ledger.Cancel(batch, 5, "Canceled second", fixture.Member, "Lead", fixture.Now));
        await fixture.Writer.SaveAsync(ledger, fixture.Context);
        var released = await fixture.ChangeAsync("revise");

        // Assert
        Assert.False(stillBlocked.IsSuccess);
        Assert.True(Assert.IsType<RequestError>(stillBlocked.Error).IsTransient);
        Assert.True(released.IsSuccess);
    }

    sealed class UnsavedLedgerReader(IAggregateReader inner, ApplicationImportLedger ledger) : IAggregateReader
    {
        public ValueTask<T> HydrateAsync<T>(T aggregate, CancellationToken ct = default) where T : Aggregate =>
            aggregate.Stream == ledger.Stream ? ValueTask.FromResult((T)(Aggregate)ledger) : inner.HydrateAsync(aggregate, ct);
    }

    sealed class EffectArrivalReader(IAggregateReader inner, IAggregateWriter writer, DeclaredApplication target,
        IExecutionContext context, int phase) : IAggregateReader
    {
        int _reads;

        public async ValueTask<T> HydrateAsync<T>(T aggregate, CancellationToken ct = default) where T : Aggregate
        {
            var source = await inner.HydrateAsync(aggregate, ct);
            if (aggregate is DeclaredApplication && Interlocked.Increment(ref _reads) == phase)
                await writer.SaveAsync(target, context, ct);
            return source;
        }
    }

    sealed record Fixture(ServiceProvider Provider, AsyncServiceScope Scope, Uuid Tenant, Uuid Member,
        ClaimsPrincipal Actor, DateTimeOffset Now, ImportBatch Batch, ApplicationImportLedger Ledger,
        DeclaredApplication Target, RequestContext<ApplyApplicationImportEffect> Context) : IAsyncDisposable
    {
        public IAggregateReader Reader => Scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        public IAggregateWriter Writer => Scope.ServiceProvider.GetRequiredService<IAggregateWriter>();

        public async ValueTask<Result> ChangeAsync(string change, IAggregateReader? reader = null, IAggregateExecutor? executor = null)
        {
            reader ??= Reader;
            executor ??= Scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
            return change == "revise"
                ? await new ReviseApplicationHandler(executor, reader, TimeProvider.System).HandleAsync(
                    new RequestContext<ReviseApplication>(new ReviseApplication(Tenant, Target.Id, 1, "Changed", "Governed", null), Actor), CancellationToken.None)
                : await new RetireApplicationHandler(executor, reader, TimeProvider.System).HandleAsync(
                    new RequestContext<RetireApplication>(new RetireApplication(Tenant, Target.Id, 1, Now, "Retired"), Actor), CancellationToken.None);
        }

        public static async Task<Fixture> CreateAsync(bool persistEffect = true)
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(new InMemoryEventStore());
            services.AddSingleton(TimeProvider.System);
            services.AddPortia();
            var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            var scope = provider.CreateAsyncScope();
            var tenant = Uuid.CreateVersion4();
            var member = Uuid.CreateVersion4();
            var actor = new ClaimsPrincipal(new ClaimsIdentity([new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())], "BdgrzSession"));
            var now = DateTimeOffset.UtcNow;
            var target = new DeclaredApplication(tenant, Uuid.CreateVersion4());
            Assert.True(target.Declare("Manual", "Governed", null, member, "Lead", now).IsSuccess);
            var request = new StageApplicationImport(tenant, Uuid.CreateVersion4(), "manual", "applications", "partial",
                [new("app-1", "Source", "Observed", null)]);
            var batch = new ImportBatch(tenant, ImportBatch.BatchIdFor(request));
            Assert.True(batch.Stage(request, member, "Contributor", now).IsSuccess);
            var rowId = batch.GetRows()[0].RowId;
            var context = new RequestContext<ApplyApplicationImportEffect>(new ApplyApplicationImportEffect(
                tenant, batch.Id, rowId, target.Id), RequestActor.System);
            var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            await writer.SaveAsync(target, context);
            await writer.SaveAsync(batch, context);
            var ledger = new ApplicationImportLedger(tenant, "manual", "applications");
            Assert.Null(ledger.Correlate(batch, new CorrelateApplicationImportRow(tenant, batch.Id, rowId, 1,
                "link_existing", target.Id, 1, "Reviewed"), target, member, "Lead", now));
            Assert.True(ledger.BeginAcceptance(batch, 2, new Dictionary<Uuid, DeclaredApplication> { [target.Id] = target }, member, "Lead", now).IsSuccess);
            await writer.SaveAsync(ledger, context);
            Assert.True(target.RecordPendingImportEffect(ledger, batch, rowId, now).IsSuccess);
            if (persistEffect)
                await writer.SaveAsync(target, context);
            return new Fixture(provider, scope, tenant, member, actor, now, batch, ledger, target, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Scope.DisposeAsync();
            await Provider.DisposeAsync();
        }
    }

    [Theory]
    [InlineData("revise")]
    [InlineData("retire")]
    public void ShouldRejectTargetChangeGivenUnsettledPendingImport(string change)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var actor = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var target = new DeclaredApplication(tenant, Uuid.CreateVersion4());
        Assert.True(target.Declare("Manual", "Governed", null, actor, "Lead", now).IsSuccess);
        var request = new StageApplicationImport(tenant, Uuid.CreateVersion4(), "manual", "applications", "partial",
            [new("app-1", "Source", "Observed", null)]);
        var batch = new ImportBatch(tenant, ImportBatch.BatchIdFor(request));
        Assert.True(batch.Stage(request, actor, "Contributor", now).IsSuccess);
        var ledger = new ApplicationImportLedger(tenant, "manual", "applications");
        var rowId = batch.GetRows()[0].RowId;
        Assert.Null(ledger.Correlate(batch, new CorrelateApplicationImportRow(tenant, batch.Id, rowId, 1,
            "link_existing", target.Id, 1, "Reviewed"), target, actor, "Lead", now));
        Assert.True(ledger.BeginAcceptance(batch, 2, new Dictionary<Uuid, DeclaredApplication> { [target.Id] = target }, actor, "Lead", now).IsSuccess);
        Assert.True(target.RecordPendingImportEffect(ledger, batch, rowId, now).IsSuccess);
        var before = new AggregateScenario<DeclaredApplication>(target).PendingEvents.Count;

        // Act
        var failure = change == "revise" ? target.Revise(1, "Changed", "Governed", null, actor, "Lead", now) :
            target.Retire(1, now, "Retired", null, actor, "Lead", now);

        // Assert
        Assert.NotNull(failure);
        Assert.Equal(1, target.Revision);
        Assert.False(target.IsRetired);
        Assert.Equal(before, new AggregateScenario<DeclaredApplication>(target).PendingEvents.Count);
    }
}
