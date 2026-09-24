using System.Security.Claims;
using Bdgrz.Compliance.Features.Applications;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class DeclareSystemInstanceHandlerTests
{
    [Fact]
    public async Task ShouldReturnTransientConflictGivenExpectedApplicationRevisionAboveSource()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var handler = scenario.Handler(scenario.Executor);
        var context = scenario.Context(expectedApplicationRevision: 2);

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
        Assert.Empty(await scenario.InstanceEventsAsync(context.RequestId));
    }

    [Fact]
    public async Task ShouldWriteOneRegistrationGivenConcurrentDeclarationsWithSameRequestId()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var gate = new HydrationGate(scenario.Reader, participants: 2);
        var racingHandler = scenario.Handler(new AggregateExecutor(gate, scenario.Writer));
        var retryHandler = scenario.Handler(scenario.Executor);
        var context = scenario.Context(expectedApplicationRevision: 1);

        // Act
        var racing = await Task.WhenAll(RaceAsync(racingHandler, context),
            RaceAsync(racingHandler, context));
        var retry = await retryHandler.HandleAsync(context, CancellationToken.None);
        var events = await scenario.InstanceEventsAsync(context.RequestId);

        // Assert
        // The dispatcher maps the losing append's concurrency exception to a transient 409.
        Assert.Single(racing, static outcome => outcome is Result<SystemInstanceRegistration>
        {
            IsSuccess: true,
        });
        Assert.Single(racing, static outcome => outcome is EventStreamConcurrencyException);
        Assert.True(retry.IsSuccess);
        Assert.Equal(context.RequestId, retry.Value.SystemInstanceId);
        var registered = Assert.IsType<SystemInstanceRegistered>(Assert.Single(events).Event);
        Assert.Equal(scenario.ApplicationId, registered.ApplicationId);
    }

    static async Task<object> RaceAsync(DeclareSystemInstanceHandler handler,
        FixedRequestContext context)
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

    sealed class Scenario : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        readonly Uuid _requestId = Uuid.CreateVersion4();

        Scenario(ServiceProvider provider, Uuid tenantId, Uuid applicationId)
        {
            _provider = provider;
            _scope = provider.CreateAsyncScope();
            TenantId = tenantId;
            ApplicationId = applicationId;
        }

        public Uuid TenantId { get; }
        public Uuid ApplicationId { get; }
        public IAggregateReader Reader => _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        public IAggregateWriter Writer => _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        public IAggregateExecutor Executor =>
            _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        IEventStore Events => _scope.ServiceProvider.GetRequiredService<IEventStore>();

        public static async Task<Scenario> CreateAsync()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(new InMemoryEventStore());
            services.AddSingleton(TimeProvider.System);
            services.AddPortia();
            var scenario = new Scenario(services.BuildServiceProvider(
                    new ServiceProviderOptions { ValidateScopes = true }),
                Uuid.CreateVersion4(), Uuid.CreateVersion4());
            var application = new DeclaredApplication(scenario.TenantId, scenario.ApplicationId);
            Assert.True(application.Declare("Payroll", "Run payroll", null,
                Uuid.CreateVersion4(), "Manager", DateTimeOffset.UtcNow).IsSuccess);
            await scenario.Writer.SaveAsync(application,
                new RequestDispatchContext(RequestActor.System), CancellationToken.None);
            return scenario;
        }

        public DeclareSystemInstanceHandler Handler(IAggregateExecutor executor) =>
            new(executor, Reader, new FitzApplicationDirectory(new InMemoryKvClient()),
                Events, TimeProvider.System);

        public FixedRequestContext Context(long expectedApplicationRevision) => new(
            new DeclareSystemInstance(TenantId, ApplicationId, expectedApplicationRevision,
                "Production", "production", null, "payroll-prod"), _requestId);

        public async Task<List<DomainEventRecord>> InstanceEventsAsync(Uuid instanceId)
        {
            var records = new List<DomainEventRecord>();
            await foreach (var record in Events.ReadAsync(new EventStreamAddress(
                               TenantId.ToString(), "system-instances", instanceId.ToString()),
                               0, CancellationToken.None))
                records.Add(record);
            return records;
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    /// <summary>Holds instance hydrations until every racer has loaded the empty stream.</summary>
    sealed class HydrationGate(IAggregateReader inner, int participants) : IAggregateReader
    {
        readonly TaskCompletionSource _released =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        int _arrived;

        public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            var hydrated = await inner.HydrateAsync(aggregate, ct);
            if (hydrated is not DeclaredSystemInstance)
                return hydrated;
            if (Interlocked.Increment(ref _arrived) >= participants)
                _released.TrySetResult();
            await _released.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
            return hydrated;
        }
    }

    sealed class FixedRequestContext(DeclareSystemInstance request, Uuid requestId)
        : IRequestContext<DeclareSystemInstance>
    {
        public DeclareSystemInstance Request => request;
        public ClaimsPrincipal Actor { get; } = new(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())],
            "BdgrzSession"));
        public Uuid ExecutionId { get; } = Uuid.CreateVersion4();
        public Uuid RequestId => requestId;
        public Uuid CorrelationId => requestId;
        public Uuid? CausationId => null;
        public Uuid CauseId => requestId;
        public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;
        public RequestInvocation Invocation { get; } = new DirectInvocation();
    }
}
