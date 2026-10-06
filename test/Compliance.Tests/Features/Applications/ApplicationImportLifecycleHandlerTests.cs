using System.Security.Claims;
using Bdgrz.Compliance.Features.Applications;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationImportLifecycleHandlerTests
{
    [Fact]
    public async Task ShouldWriteOneCancellationGivenConcurrentRequestsAndRetry()
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
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var events = scope.ServiceProvider.GetRequiredService<IEventStore>();
        var tenantId = Uuid.CreateVersion4();
        var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())], "BdgrzSession"));
        var request = new StageApplicationImport(tenantId, Uuid.CreateVersion4(), "manual",
            "applications", "partial", [new ApplicationImportInputRow("app-1", "Payroll", "Pay staff", null)]);
        var staged = await new StageApplicationImportHandler(executor, reader, TimeProvider.System)
            .HandleAsync(new RequestContext<StageApplicationImport>(request, actor), CancellationToken.None);
        Assert.True(staged.IsSuccess);
        var gate = new LedgerHydrationGate(reader);
        var racing = new CancelApplicationImportHandler(new AggregateExecutor(gate, writer), reader, TimeProvider.System);
        var retryHandler = new CancelApplicationImportHandler(executor, reader, TimeProvider.System);
        var cancel = new CancelApplicationImport(tenantId, staged.Value.BatchId, 1, "Superseded");

        // Act
        var outcomes = await Task.WhenAll(RaceAsync(racing, new RequestContext<CancelApplicationImport>(cancel, actor)),
            RaceAsync(racing, new RequestContext<CancelApplicationImport>(cancel, actor)));
        var retry = await retryHandler.HandleAsync(new RequestContext<CancelApplicationImport>(cancel, actor), CancellationToken.None);
        var ledger = new ApplicationImportLedger(tenantId, request.SourceKey, request.SourceNamespace);
        var records = new List<DomainEventRecord>();
        await foreach (var record in events.ReadAsync(ledger.Stream, 0, CancellationToken.None))
            records.Add(record);

        // Assert
        Assert.Single(outcomes, outcome => outcome is Result { IsSuccess: true });
        Assert.Single(outcomes, outcome => outcome is EventStreamConcurrencyException);
        Assert.True(retry.IsSuccess);
        Assert.Equal(staged.Value.BatchId, Assert.IsType<ApplicationImportCanceled>(Assert.Single(records).Event).BatchId);
    }

    static async Task<object> RaceAsync(CancelApplicationImportHandler handler,
        RequestContext<CancelApplicationImport> context)
    {
        try
        {
            return await handler.HandleAsync(context, CancellationToken.None);
        }
        catch (EventStreamConcurrencyException exception)
        {
            return exception;
        }
    }

    sealed class LedgerHydrationGate(IAggregateReader inner) : IAggregateReader
    {
        readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int _arrived;

        public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            var hydrated = await inner.HydrateAsync(aggregate, ct);
            if (hydrated is not ApplicationImportLedger)
                return hydrated;
            if (Interlocked.Increment(ref _arrived) == 2)
                _released.TrySetResult();
            await _released.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
            return hydrated;
        }
    }

    [Fact]
    public async Task ShouldReturnLedgerRevisionGivenStagingReplayAfterCancellation()
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
        var tenantId = Uuid.CreateVersion4();
        var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())], "BdgrzSession"));
        var request = new StageApplicationImport(tenantId, Uuid.CreateVersion4(), "manual",
            "applications", "partial", [new ApplicationImportInputRow("app-1", "Payroll", "Pay staff", null)]);
        var context = new RequestContext<StageApplicationImport>(request, actor);
        var stageHandler = new StageApplicationImportHandler(executor, reader, TimeProvider.System);
        var first = await stageHandler.HandleAsync(context, CancellationToken.None);
        Assert.True(first.IsSuccess);
        var cancelHandler = new CancelApplicationImportHandler(executor, reader, TimeProvider.System);
        var cancel = await cancelHandler.HandleAsync(new RequestContext<CancelApplicationImport>(
            new CancelApplicationImport(tenantId, first.Value.BatchId, 1, "Superseded"), actor), CancellationToken.None);
        Assert.True(cancel.IsSuccess);

        // Act
        var replay = await stageHandler.HandleAsync(context, CancellationToken.None);
        var raw = await reader.HydrateAsync(new ImportBatch(tenantId, first.Value.BatchId));
        var ledger = await reader.HydrateAsync(new ApplicationImportLedger(tenantId,
            request.SourceKey, request.SourceNamespace));

        // Assert
        Assert.True(replay.IsSuccess);
        Assert.Equal(2, replay.Value.Revision);
        Assert.Equal(first.Value.ContentSha256, replay.Value.ContentSha256);
        Assert.Equal(1, raw.Revision);
        Assert.False(raw.IsCanceled);
        Assert.Equal(2, ledger.GetCanceledRevision(raw.Id));
    }
}
