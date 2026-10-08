using System.Collections.Concurrent;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Bdgrz.Compliance.Tests.Features.Readiness;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class ConcurrentOccurrenceSubmissionTests
{
    [Fact]
    public async Task ShouldRetainOneAttributionAndReviewItemGivenCompetingPersonalSubmissions()
    {
        // Arrange
        var source = await OperationsFixture.CreateAsync();
        await using var sourceProvider = source.Provider;
        await source.PlanAsync(backup: new OperatingHolder("member", source.BackupMemberId));
        var occurrence = (await source.OccurrencesAsync("missed"))[0];
        Assert.Equal(0, occurrence.Revision);
        var events = source.Provider.GetRequiredService<IEventStore>();
        var ledger = new ControlOperationsLedger(source.TenantId, source.ProgramId);
        var before = await ProgramManagementServices.HydrateAsync(source.Provider, ledger);
        var barrier = new CompetingAppendStore(events, ledger.Stream, before.CommittedStreamPosition);
        await using var provider = Compose(source, barrier);
        var owner = source.Attest(occurrence) with { Notes = "Owner submission" };
        var backup = source.Attest(occurrence) with { Notes = "Backup submission" };

        // Act
        var results = await Task.WhenAll(
            SubmitAsync(provider, source.OwnerUserId, owner),
            SubmitAsync(provider, source.BackupUserId, backup));
        var after = await ProgramManagementServices.HydrateAsync(provider,
            new ControlOperationsLedger(source.TenantId, source.ProgramId));
        var records = new List<DomainEventRecord>();
        await foreach (var record in events.ReadAsync(ledger.Stream, before.CommittedStreamPosition, CancellationToken.None))
            records.Add(record);
        await ProjectAsync(provider, source.TenantId);
        var queue = await ReviewWorkAsync(provider, source, source.ReviewerUserId);
        await using var replayProvider = Compose(source, events);
        await ProjectAsync(replayProvider, source.TenantId);
        await ProjectAsync(replayProvider, source.TenantId);
        var replayed = await ReviewWorkAsync(replayProvider, source, source.ReviewerUserId);
        var population = await PersonalOccurrenceProofTransportTests.HttpAsync(replayProvider, source.ReviewerUserId,
            new ListControlOccurrences(source.TenantId, source.ProgramId, source.ControlId));

        // Assert
        var winner = Assert.Single(results, result => result.Result is { IsSuccess: true }).Result!.Value;
        var loser = Assert.Single(results, result => result.Conflict is not null);
        Assert.Null(loser.Result);
        Assert.IsType<EventStreamConcurrencyException>(loser.Conflict);
        Assert.Equal(2, barrier.Arrivals);
        Assert.All(barrier.ExpectedPositions, position => Assert.Equal(before.CommittedStreamPosition, position));
        Assert.All(barrier.BatchSizes, count => Assert.Equal(2, count));
        Assert.Equal(before.CommittedStreamPosition + 2, after.CommittedStreamPosition);
        Assert.Equal(2, records.Count);
        var opened = Assert.IsType<ControlOccurrenceOpened>(records[0].Event);
        var retained = Assert.IsType<ControlOccurrenceAttested>(records[1].Event);
        var attestation = Assert.Single(winner.Value.Attestations);
        Assert.Equal(retained.Attestation, attestation);
        var winningMember = results[0].Result is { IsSuccess: true } ? source.OwnerMemberId : source.BackupMemberId;
        Assert.Equal(winningMember.ToString(), opened.OpenedBy.Id);
        var winningUser = results[0].Result is { IsSuccess: true } ? source.OwnerUserId : source.BackupUserId;
        Assert.All(records, record => Assert.Equal(winningUser.ToString(), record.Event.Metadata.Actor?.Subject));
        Assert.Equal(winningMember, attestation.RecorderMemberId);
        Assert.Equal(new OperatingHolder("member", winningMember), attestation.PerformedBy);
        Assert.Equal(results[0].Result is { IsSuccess: true } ? owner.Notes : backup.Notes, attestation.Notes);
        Assert.Equal(occurrence.OccurrenceId, retained.OccurrenceId);
        Assert.Equal(1, attestation.Version);
        var current = Assert.Single(population.Value.Items, item => item.OccurrenceId == occurrence.OccurrenceId);
        Assert.Equal("submitted", current.State);
        Assert.Equal(attestation, Assert.Single(current.Attestations));
        var item = Assert.Single(queue.Value.Items);
        Assert.Equal(occurrence.OccurrenceId, item.SourceId);
        Assert.Equal(1, queue.Value.Counts.Total);
        Assert.Equal(queue.Value.Counts, replayed.Value.Counts);
        Assert.Equal(item.WorkItemId, Assert.Single(replayed.Value.Items).WorkItemId);
    }

    static async Task<(Result<ControlOccurrenceView>? Result, EventStreamConcurrencyException? Conflict)> SubmitAsync(IServiceProvider provider, Uuid actor,
        AttestControlOccurrence request)
    {
        await using var scope = provider.CreateAsyncScope();
        try
        {
            return (await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request,
                PersonalReadinessClosureTransportTests.Context(actor, "http"), CancellationToken.None), null);
        }
        catch (EventStreamConcurrencyException exception)
        {
            // The bus propagates OCC; the generated HTTP binding owns its conflict response.
            return (null, exception);
        }
    }

    sealed class CompetingAppendStore(IEventStore inner, EventStreamAddress target, ulong expectedPosition) : IEventStore
    {
        readonly TaskCompletionSource _bothPrepared = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int _arrivals;
        public int Arrivals => _arrivals;
        public ConcurrentQueue<ulong> ExpectedPositions { get; } = new();
        public ConcurrentQueue<int> BatchSizes { get; } = new();

        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream, ulong fromOffset,
            CancellationToken ct) => inner.ReadAsync(stream, fromOffset, ct);

        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
            CancellationToken ct) => inner.ReadAsync(pattern, cursor, ct);

        public async ValueTask AppendAsync(EventStreamAddress stream, ulong expectedStreamPosition,
            IReadOnlyList<DomainEvent> events, CancellationToken ct = default)
        {
            if (stream == target && events.Any(value => value is ControlOccurrenceAttested))
            {
                Assert.Equal(expectedPosition, expectedStreamPosition);
                ExpectedPositions.Enqueue(expectedStreamPosition);
                BatchSizes.Enqueue(events.Count);
                if (Interlocked.Increment(ref _arrivals) == 2)
                    _bothPrepared.SetResult();
                // Both production commands have prepared their batches before either reaches the real store.
                await _bothPrepared.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
            }
            await inner.AppendAsync(stream, expectedStreamPosition, events, ct);
        }
    }

    static ServiceProvider Compose(OperationsFixture source, IEventStore events)
    {
        var services = new ServiceCollection();
        services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
            ["Fitz:ApplicationName"] = "compliance"
        }).Build(), developerAuthentication: true);
        services.AddSingleton(events);
        services.AddSingleton<IDomainEventReader>((IDomainEventReader)events);
        services.AddSingleton<IKvClient>(new InMemoryKvClient());
        services.AddSingleton<IAccessGrantPermissionAuthorizer>(new PermissionBackedAccessGrantPermissionAuthorizer(source.Permissions));
        services.AddSingleton<IProgramResourceScopeResolver, TestProgramResourceScopeResolver>();
        services.AddSingleton<ITenantActivity, ActiveTenant>();
        services.AddSingleton<ITenantMembershipDirectoryReader, AlwaysMemberDirectory>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    static async Task<Result<WorkQueueView>> ReviewWorkAsync(IServiceProvider provider, OperationsFixture source, Uuid actor)
    {
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
            new ListWork(source.TenantId, source.ProgramId, "mine", Search: "occurrence_review"),
            new RequestDispatchContext(ProgramManagementServices.Actor(actor), new DirectInvocation()), CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result;
    }

    static async Task ProjectAsync(IServiceProvider provider, Uuid tenantId)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        foreach (var directory in services.GetServices<IAccountableWorkItemDirectoryReader>()
                     .DistinctBy(reader => reader.ProjectorName))
        {
            var registration = Assert.Single(services.GetServices<WorkloadRegistration>(), value => value.Name == directory.ProjectorName);
            var projector = (Projector)services.GetRequiredService(registration.ComponentType);
            await new ProjectorScenario(new TenantId(tenantId.ToString())).RunAsync(projector);
            var checkpoint = await directory.LoadCheckpointAsync(tenantId);
            var runner = new ProjectorRunner(services.GetRequiredService<IDomainEventReader>());
            while (true)
            {
                var next = await runner.RunAsync(projector, checkpoint);
                if (next == checkpoint)
                    break;
                checkpoint = next;
            }
        }
    }
}
