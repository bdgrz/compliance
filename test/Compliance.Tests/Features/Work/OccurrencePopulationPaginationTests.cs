using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class OccurrencePopulationPaginationTests
{
    [Fact]
    public async Task ShouldPreserveOrderedPopulationGivenMaterializationBetweenLiveOffsetPages()
    {
        // Arrange
        var source = await OperationsFixture.CreateAsync();
        await using var sourceProvider = source.Provider;
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var firstStart = today.AddDays(-28);
        await source.PlanAsync(cadence: new ControlCadence("recurring", "weekly", firstStart, 0),
            effectiveFrom: firstStart);
        await using var provider = Compose(source, now);
        var before = await ListAsync(provider, source, 200);
        Assert.Null(before.NextCursor);
        Assert.Equal(17, before.Items.Count);
        Assert.Equal(Enumerable.Range(0, 17).Select(index => firstStart.AddDays(index * 7)),
            before.Items.Select(item => item.PeriodStart!.Value));
        Assert.All(before.Items, item => Assert.Equal("expected", item.Kind));
        var first = await ListAsync(provider, source, 2);
        Assert.Equal("2", first.NextCursor);
        var target = first.Items[0];
        Assert.Equal(0, target.Revision);
        var during = new List<ControlOccurrenceView>(first.Items);

        // Act
        var submitted = await PersonalOccurrenceProofTransportTests.HttpAsync(provider, source.OwnerUserId,
            source.Attest(target, "not_applicable", [], "No changes in the review population."));
        var cursor = first.NextCursor;
        var cursors = new HashSet<string>(StringComparer.Ordinal);
        do
        {
            Assert.NotNull(cursor);
            Assert.True(cursors.Add(cursor), "The public cursor must advance.");
            var next = await ListAsync(provider, source, 2, cursor);
            Assert.InRange(next.Items.Count, 1, 2);
            during.AddRange(next.Items);
            cursor = next.NextCursor;
        } while (cursor is not null);
        var fresh = await TraverseAsync(provider, source);
        await ProjectAsync(provider, source.TenantId);
        var review = await ReviewWorkAsync(provider, source, source.ReviewerUserId);
        await using var replayProvider = Compose(source, now);
        await ProjectAsync(replayProvider, source.TenantId);
        await ProjectAsync(replayProvider, source.TenantId);
        var replayedPopulation = await TraverseAsync(replayProvider, source);
        var replayedReview = await ReviewWorkAsync(replayProvider, source, source.ReviewerUserId);

        // Assert
        Assert.Null(cursor);
        Assert.Equal(before.Items.Select(item => item.OccurrenceId), during.Select(item => item.OccurrenceId));
        Assert.Equal(17, during.Select(item => item.OccurrenceId).Distinct().Count());
        Assert.Equal(during.Select(item => item.OccurrenceId), fresh.Select(item => item.OccurrenceId));
        Assert.Equal(fresh.Select(item => item.OccurrenceId), replayedPopulation.Select(item => item.OccurrenceId));
        var recorded = Assert.Single(fresh, item => item.OccurrenceId == target.OccurrenceId);
        Assert.Equal("submitted", recorded.State);
        Assert.Equal(target.OccurrenceId, submitted.Value.OccurrenceId);
        Assert.Equal(target.Kind, recorded.Kind);
        Assert.Equal(target.DueOn, recorded.DueOn);
        Assert.Equal(target.PeriodStart, recorded.PeriodStart);
        Assert.Equal(target.PeriodEnd, recorded.PeriodEnd);
        Assert.Equal(target.PlanVersionId, recorded.PlanVersionId);
        Assert.Equal(target.ControlVersionId, recorded.ControlVersionId);
        Assert.Equal(submitted.Value.Attestations[0], Assert.Single(recorded.Attestations));
        Assert.Single(fresh, item => item.State == "submitted");
        Assert.Equal(fresh.Select(item => (item.OccurrenceId, item.State, item.Revision)),
            replayedPopulation.Select(item => (item.OccurrenceId, item.State, item.Revision)));
        var work = Assert.Single(review.Value.Items);
        Assert.Equal(target.OccurrenceId, work.SourceId);
        Assert.Equal(1, review.Value.Counts.Total);
        Assert.Equal(review.Value.Counts, replayedReview.Value.Counts);
        Assert.Equal(work.WorkItemId, Assert.Single(replayedReview.Value.Items).WorkItemId);
    }

    static async Task<IReadOnlyList<ControlOccurrenceView>> TraverseAsync(IServiceProvider provider,
        OperationsFixture source)
    {
        var items = new List<ControlOccurrenceView>();
        string? cursor = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        do
        {
            var page = await ListAsync(provider, source, 2, cursor);
            items.AddRange(page.Items);
            cursor = page.NextCursor;
            if (cursor is not null)
                Assert.True(seen.Add(cursor), "The public cursor must advance.");
        } while (cursor is not null);
        return items;
    }

    static async Task<Page<ControlOccurrenceView>> ListAsync(IServiceProvider provider, OperationsFixture source,
        int limit, string? cursor = null)
    {
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
            new ListControlOccurrences(source.TenantId, source.ProgramId, source.ControlId, Limit: limit, Cursor: cursor),
            new RequestDispatchContext(ProgramManagementServices.Actor(source.ReviewerUserId), new DirectInvocation()),
            CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value;
    }

    sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    static ServiceProvider Compose(OperationsFixture source, DateTimeOffset now)
    {
        var services = new ServiceCollection();
        services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
            ["Fitz:ApplicationName"] = "compliance"
        }).Build(), developerAuthentication: true);
        var events = source.Provider.GetRequiredService<IEventStore>();
        services.AddSingleton(events);
        services.AddSingleton<IDomainEventReader>((IDomainEventReader)events);
        services.AddSingleton<IKvClient>(new InMemoryKvClient());
        services.AddSingleton<TimeProvider>(new FixedClock(now));
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
