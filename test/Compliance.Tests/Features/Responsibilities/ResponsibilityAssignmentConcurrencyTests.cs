using System.Security.Claims;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Responsibilities;

public sealed class ResponsibilityAssignmentConcurrencyTests
{
    [Fact]
    public async Task ShouldPersistOnlyOneConflictingAssignmentGivenConcurrentCommands()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var versionId = Uuid.CreateVersion4();
        var memberId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddPortia();
        await using var provider = services.BuildServiceProvider();
        await using var scopeServices = provider.CreateAsyncScope();
        var reader = scopeServices.ServiceProvider.GetRequiredService<IAggregateReader>();
        var writer = scopeServices.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var boundary = new SystemBoundary(tenantId, boundaryId);
        Assert.True(boundary.Create(Uuid.CreateVersion4(), versionId,
            new BoundaryContent("Statement", "readiness", ["security"], []), Uuid.CreateVersion4(),
            "Author", now).IsSuccess);
        await writer.SaveAsync(boundary, new RequestDispatchContext(RequestActor.System));
        var gate = new HydrationGate(reader, participants: 2);
        var executor = new AggregateExecutor(gate, writer);
        var scope = new ResponsibilityScope("boundary", boundaryId, versionId, 1);

        // Act
        var outcomes = await Task.WhenAll(
            RaceAsync(executor, tenantId, boundaryId, scope, memberId,
                ResponsibilityType.ControlOwner, now),
            RaceAsync(executor, tenantId, boundaryId, scope, memberId,
                ResponsibilityType.AssignedReviewer, now));
        var resultingBoundary = await reader.HydrateAsync(new SystemBoundary(tenantId, boundaryId));
        var assignments = resultingBoundary.GetResponsibilitySet(scope).ReadAssignments();

        // Assert
        Assert.Single(outcomes, static outcome => outcome is Result { IsSuccess: true });
        Assert.Single(outcomes, static outcome => outcome is EventStreamConcurrencyException);
        Assert.Single(assignments);
    }

    static async Task<object> RaceAsync(AggregateExecutor executor, Uuid tenantId,
        Uuid boundaryId, ResponsibilityScope scope, Uuid memberId,
        ResponsibilityType type, DateTimeOffset now)
    {
        try
        {
            return await executor.ExecuteAsync(new SystemBoundary(tenantId, boundaryId), boundary =>
            {
                var failure = boundary.AssignResponsibility(scope, Uuid.CreateVersion4(), memberId,
                    type, Uuid.CreateVersion4(), "Administrator", now, now, null, []);
                return CommandFailureRequestAdapter.ToOutcome(failure);
            }, new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        }
        catch (EventStreamConcurrencyException exception)
        {
            return exception;
        }
    }

    sealed class HydrationGate(IAggregateReader inner, int participants) : IAggregateReader
    {
        readonly TaskCompletionSource _released =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        int _arrived;

        public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            var hydrated = await inner.HydrateAsync(aggregate, ct);
            if (hydrated is SystemBoundary)
            {
                if (Interlocked.Increment(ref _arrived) == participants)
                    _released.TrySetResult();
                await _released.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
            }
            return hydrated;
        }
    }
}
