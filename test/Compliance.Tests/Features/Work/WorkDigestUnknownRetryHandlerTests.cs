using System.Security.Claims;
using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class WorkDigestUnknownRetryHandlerTests
{
    [Fact]
    public async Task ShouldAuthorizeSameWindowRetryGivenPlatformOperatorAndRelayEvidence()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var memberId = Uuid.CreateVersion4();
        var operatorId = Uuid.CreateVersion4();
        var weekOf = new DateOnly(2026, 10, 5);
        var scheduledAt = new DateTimeOffset(2026, 10, 5, 13, 0, 0, TimeSpan.Zero);
        var authorizedAt = scheduledAt.AddHours(2);
        var messageId = Uuid.CreateVersion4();
        await using var provider = BuildProvider(operatorId, authorizedAt);
        await SeedUnknownDispatchAsync(provider, tenantId, memberId, weekOf, scheduledAt,
            messageId);
        var request = new AuthorizeWorkDigestUnknownRetry(tenantId, memberId, weekOf,
            "smtp-record:INC-4201", "Relay logs confirm the message was not accepted.");

        // Act
        var dispatchContext = HttpContext(Actor(operatorId));
        var authorizer = new PlatformOperatorAuthorizer(
            provider.GetRequiredService<IPlatformOperatorAccess>());
        var authorized = await authorizer.AuthorizeAsync(
            new Cntryl.Portia.RequestContext<IPlatformOperatorRequest>(request, dispatchContext),
            CancellationToken.None);
        Assert.True(authorized.IsSuccess);
        await using var scope = provider.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<AuthorizeWorkDigestUnknownRetryHandler>();
        var result = await handler.HandleAsync(new Cntryl.Portia.RequestContext<AuthorizeWorkDigestUnknownRetry>(
            request, dispatchContext), CancellationToken.None);
        var replay = await handler.HandleAsync(
            new Cntryl.Portia.RequestContext<AuthorizeWorkDigestUnknownRetry>(request,
                dispatchContext), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(replay.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, replay.Error.Kind);
        Assert.IsAssignableFrom<IPlatformOperatorRequest>(request);
        Assert.Equal(WorkDigestDispatch.RetryPending, result.Value.Status);
        Assert.Equal(messageId, result.Value.MessageId);
        Assert.Equal(1, result.Value.Attempts);
        Assert.Equal(authorizedAt, result.Value.NextAttemptAt);
        Assert.Equal(operatorId, result.Value.RetryAuthorizedBy);
        Assert.Equal(authorizedAt, result.Value.RetryAuthorizedAt);
        Assert.Equal("smtp-record:INC-4201", result.Value.RetryEvidenceReference);
    }

    [Fact]
    public async Task ShouldDenyUnknownRetryGivenNonOperator()
    {
        // Arrange
        await using var provider = BuildProvider(Uuid.CreateVersion4(),
            new DateTimeOffset(2026, 10, 5, 15, 0, 0, TimeSpan.Zero));
        var request = new AuthorizeWorkDigestUnknownRetry(Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), new DateOnly(2026, 10, 5), "smtp-record:INC-4201",
            "Relay logs confirm the message was not accepted.");

        // Act
        var authorizer = new PlatformOperatorAuthorizer(
            provider.GetRequiredService<IPlatformOperatorAccess>());
        var result = await authorizer.AuthorizeAsync(
            new Cntryl.Portia.RequestContext<IPlatformOperatorRequest>(request,
                HttpContext(Actor(Uuid.CreateVersion4()))),
            CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error.Kind);
    }

    [Fact]
    public async Task ShouldRejectSystemRetryGivenHumanAuthorizationRequirement()
    {
        // Arrange
        await using var provider = BuildProvider(Uuid.CreateVersion4(),
            new DateTimeOffset(2026, 10, 5, 15, 0, 0, TimeSpan.Zero));
        var request = new AuthorizeWorkDigestUnknownRetry(Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), new DateOnly(2026, 10, 5), "smtp-record:INC-4201",
            "Relay logs confirm the message was not accepted.");
        await using var scope = provider.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<AuthorizeWorkDigestUnknownRetryHandler>();
        var context = new Cntryl.Portia.RequestContext<AuthorizeWorkDigestUnknownRetry>(request,
            new RequestDispatchContext(RequestActor.System,
                new HttpInvocation("POST", "/synthetic/digest/retry", "/synthetic/digest/retry",
                    "synthetic")));

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Unauthorized, result.Error.Kind);
    }

    [Fact]
    public async Task ShouldRequireEvidenceGivenRetryAuthorizationRequest()
    {
        // Arrange
        var operatorId = Uuid.CreateVersion4();
        await using var provider = BuildProvider(operatorId,
            new DateTimeOffset(2026, 10, 5, 15, 0, 0, TimeSpan.Zero));
        await using var scope = provider.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<AuthorizeWorkDigestUnknownRetryHandler>();
        var request = new AuthorizeWorkDigestUnknownRetry(Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), new DateOnly(2026, 10, 5), " ",
            "Relay logs confirm the message was not accepted.");

        // Act
        var result = await handler.HandleAsync(
            new Cntryl.Portia.RequestContext<AuthorizeWorkDigestUnknownRetry>(request,
                HttpContext(Actor(operatorId))), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error.Kind);
    }

    [Fact]
    public async Task ShouldReturnConflictGivenConcurrentUnknownRetryAuthorizations()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var memberId = Uuid.CreateVersion4();
        var operatorId = Uuid.CreateVersion4();
        var weekOf = new DateOnly(2026, 10, 5);
        var scheduledAt = new DateTimeOffset(2026, 10, 5, 13, 0, 0, TimeSpan.Zero);
        var authorizedAt = scheduledAt.AddHours(2);
        var messageId = Uuid.CreateVersion4();
        await using var provider = BuildProvider(operatorId, authorizedAt);
        await SeedUnknownDispatchAsync(provider, tenantId, memberId, weekOf, scheduledAt,
            messageId);
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var innerReader = services.GetRequiredService<IAggregateReader>();
        var writer = services.GetRequiredService<IAggregateWriter>();
        var gatedReader = new ConcurrentDispatchReader(innerReader, memberId);
        var executor = new AggregateExecutor(gatedReader, writer);
        var handler = new AuthorizeWorkDigestUnknownRetryHandler(executor,
            services.GetRequiredService<TimeProvider>(),
            services.GetRequiredService<WorkDigestDeliverySettings>());
        var authorizer = new PlatformOperatorAuthorizer(
            services.GetRequiredService<IPlatformOperatorAccess>());
        var request = new AuthorizeWorkDigestUnknownRetry(tenantId, memberId, weekOf,
            "smtp-record:INC-4201", "Relay logs confirm the message was not accepted.");
        var contexts = new[] { HttpContext(Actor(operatorId)), HttpContext(Actor(operatorId)) };
        foreach (var context in contexts)
        {
            var authorization = await authorizer.AuthorizeAsync(
                new Cntryl.Portia.RequestContext<IPlatformOperatorRequest>(request, context),
                CancellationToken.None);
            Assert.True(authorization.IsSuccess);
        }

        // Act
        var results = await Task.WhenAll(contexts.Select(context => handler.HandleAsync(
            new Cntryl.Portia.RequestContext<AuthorizeWorkDigestUnknownRetry>(request, context),
            CancellationToken.None).AsTask()));
        var persisted = await innerReader.HydrateAsync(new WorkDigestDispatch(tenantId, memberId));
        var records = new List<DomainEventRecord>();
        await foreach (var record in services.GetRequiredService<IEventStore>().ReadAsync(
                           new WorkDigestDispatch(tenantId, memberId).Stream, 0,
                           CancellationToken.None))
            records.Add(record);

        // Assert
        Assert.Single(results, static result => result.IsSuccess);
        var conflict = Assert.Single(results, static result => !result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, conflict.Error!.Kind);
        Assert.Equal(WorkDigestDispatch.RetryPending, persisted.Read(weekOf).Status);
        Assert.Equal(messageId, persisted.Read(weekOf).MessageId);
        Assert.Equal(1, records.Count(static record =>
            record.Event is WorkDigestUnknownRetryAuthorized));
        Assert.Equal(2, gatedReader.Arrivals);
    }

    static ServiceProvider BuildProvider(Uuid operatorId, DateTimeOffset now) =>
        ProgramManagementServices.Build(new DigestRetryPermissions(), portia => portia
                .AddRequestHandler<AuthorizeWorkDigestUnknownRetryHandler>()
                .AddRequestAuthorizer<PlatformOperatorAuthorizer>(),
            services =>
            {
                services.AddSingleton<IPlatformOperatorAccess>(
                    new DigestRetryOperatorAccess(operatorId));
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
                services.AddSingleton(new WorkDigestDeliverySettings("https://app.example",
                    "/tenants/{tenant_id}/{tenant_slug}/programs/{program_id}/work/{work_item_id}",
                    3, TimeSpan.FromMinutes(5), TimeSpan.FromHours(167),
                    TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(1)));
            });

    static Task SeedUnknownDispatchAsync(IServiceProvider provider, Uuid tenantId, Uuid memberId,
        DateOnly weekOf, DateTimeOffset scheduledAt, Uuid messageId) =>
        ProgramManagementServices.SeedAsync(provider, new WorkDigestDispatch(tenantId, memberId),
            dispatch => dispatch.Schedule(new WorkDigestScheduleWindow(weekOf,
                        "America/New_York", scheduledAt), messageId,
                    TimeSpan.FromHours(167), scheduledAt) &&
                dispatch.StartAttempt(weekOf, scheduledAt) &&
                dispatch.RecordUnknown(weekOf, scheduledAt.AddMinutes(1), "transport_ambiguous")
                ? Result.Success
                : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The test digest dispatch could not be seeded.")));

    static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "BdgrzSession"));

    static RequestDispatchContext HttpContext(ClaimsPrincipal actor) =>
        new(actor, new HttpInvocation("POST", "/synthetic/digest/retry",
            "/synthetic/digest/retry", "synthetic"));

    sealed class DigestRetryPermissions : IPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            string permission, CancellationToken ct = default) => ValueTask.FromResult(true);
    }

    sealed class DigestRetryOperatorAccess(Uuid operatorId) : IPlatformOperatorAccess
    {
        public ValueTask<bool> IsOperatorAsync(Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(operatorId == userId);
    }

    sealed class ConcurrentDispatchReader(IAggregateReader inner, Uuid memberId)
        : IAggregateReader
    {
        readonly TaskCompletionSource _bothHydrated =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        int _arrivals;

        public int Arrivals => _arrivals;

        public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            var hydrated = await inner.HydrateAsync(aggregate, ct);
            if (hydrated is WorkDigestDispatch && hydrated.Id == memberId)
            {
                if (Interlocked.Increment(ref _arrivals) == 2)
                    _bothHydrated.TrySetResult();
                await _bothHydrated.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
            }
            return hydrated;
        }
    }

    sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
