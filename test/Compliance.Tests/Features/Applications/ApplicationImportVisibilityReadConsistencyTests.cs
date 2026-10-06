using Bdgrz.Compliance.Features.Applications;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationImportVisibilityReadConsistencyTests
{
    [Fact]
    public async Task ShouldReturnRetryableConflictGivenChangedVisibilityCursor()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var consistency = new ApplicationImportVisibilityReadConsistency(
            new FitzApplicationDirectory(new InMemoryKvClient()), new PendingEvents(tenant, []));
        var behavior = new ApplicationImportVisibilityBehavior(consistency);

        // Act
        var result = await behavior.HandleAsync(new RequestContext<ListApplications>(new ListApplications(tenant), RequestActor.System),
            _ => ValueTask.FromException<Result<Page<ApplicationView>>>(new ApplicationImportVisibilityChangedException()), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(Assert.IsType<RequestError>(result.Error).IsTransient);
    }

    [Fact]
    public async Task ShouldBlockHandlerGivenRegisteredReadBehaviorAndPendingCommit()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var services = new ServiceCollection();
        services.AddSingleton<IApplicationDirectoryReader>(new FitzApplicationDirectory(new InMemoryKvClient()));
        services.AddSingleton<IDomainEventReader>(new PendingEvents(tenant, [Marker(tenant)]));
        services.AddScoped<ApplicationImportVisibilityReadConsistency>();
        services.AddPortia().AddRequestHandler<EmptyListHandler>()
            .AddRequestPipelineBehavior<ApplicationImportVisibilityBehavior>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>()
            .SendAsync(new ListApplications(tenant), RequestActor.System);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(Assert.IsType<RequestError>(result.Error).IsTransient);
        Assert.False(scope.ServiceProvider.GetRequiredService<EmptyListHandler>().WasCalled);
    }

    [Fact]
    public async Task ShouldRejectProjectionReadGivenPendingCommitMarker()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var directory = new FitzApplicationDirectory(new InMemoryKvClient());
        var events = new PendingEvents(tenant, [Marker(tenant)]);
        var consistency = new ApplicationImportVisibilityReadConsistency(directory, events);

        // Act
        var result = await consistency.CaptureAsync(tenant, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(Assert.IsType<RequestError>(result.Error).IsTransient);
    }

    [Fact]
    public async Task ShouldRejectReadResultGivenCommitArrivesAfterCapture()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var directory = new FitzApplicationDirectory(new InMemoryKvClient());
        var events = new PendingEvents(tenant, []);
        var consistency = new ApplicationImportVisibilityReadConsistency(directory, events);
        var captured = await consistency.CaptureAsync(tenant, CancellationToken.None);
        Assert.True(captured.IsSuccess);
        events.Items = [Marker(tenant)];

        // Act
        var result = await consistency.ConfirmAsync(tenant, captured.Value, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(Assert.IsType<RequestError>(result.Error).IsTransient);
    }

    static ApplicationImportCommitted Marker(Uuid tenant) => new(tenant, "manual", "applications",
        Uuid.CreateVersion4(), 6, new string('0', 64), [], DateTimeOffset.UtcNow);

    internal sealed class EmptyListHandler : IRequestHandler<ListApplications, Page<ApplicationView>>
    {
        public bool WasCalled { get; private set; }

        public ValueTask<Result<Page<ApplicationView>>> HandleAsync(IRequestContext<ListApplications> context,
            CancellationToken ct)
        {
            WasCalled = true;
            return ValueTask.FromResult(Result<Page<ApplicationView>>.Success(new Page<ApplicationView>([], null)));
        }
    }

    sealed class PendingEvents(Uuid tenant, DomainEvent[] items) : IDomainEventReader
    {
        public DomainEvent[] Items { get; set; } = items;

        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream,
            ulong offset = 0, CancellationToken ct = default) =>
            ReadAsync(EventStreamPattern.ForPattern(tenant.ToString()), default, ct);

        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern,
            EventCursor cursor = default, [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            foreach (var ev in Items)
            {
                ct.ThrowIfCancellationRequested();
                yield return new DomainEventRecord(new EventStreamAddress(tenant.ToString(), "application_imports",
                    "source"), ev, 0, default);
            }
        }
    }
}
