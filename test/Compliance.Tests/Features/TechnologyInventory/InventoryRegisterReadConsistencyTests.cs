using System.Security.Claims;
using Bdgrz.Compliance.Features.TechnologyInventory;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.TechnologyInventory;

public sealed class InventoryRegisterReadConsistencyTests
{
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Recorder");
    static readonly Uuid Owner = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, RequestErrorKind.Validation)]
    [InlineData(1, RequestErrorKind.Conflict)]
    [InlineData(2, RequestErrorKind.Conflict)]
    public async Task ShouldDistinguishInvalidRevisionAndLagGivenRecordedUnprojectedLocation(
        long minimum, RequestErrorKind expected)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var consistency = Consistency(new FitzInventoryRegisterDirectory(new InMemoryKvClient()),
            Locations(tenant, id), new InMemoryEventStore());

        // Act
        var result = await consistency.GetLocationAsync(tenant, id, minimum, CancellationToken.None);

        // Assert
        Assert.Equal(expected, Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Fact]
    public async Task ShouldReturnNotFoundGivenUnknownOrForeignRecord()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var directory = new FitzInventoryRegisterDirectory(new InMemoryKvClient());
        await Project(directory, tenant, new LocationRevisionRecorded(tenant, id, 1, Loc("HQ"), Author, Now));
        var otherTenant = Uuid.CreateVersion4();
        var foreign = Consistency(directory, new LocationRegister(otherTenant), new InMemoryEventStore());
        var unknown = Consistency(directory, Locations(tenant, id), new InMemoryEventStore());

        // Act
        var leak = await foreign.GetLocationAsync(otherTenant, id, null, CancellationToken.None);
        var missing = await unknown.GetLocationAsync(tenant, Uuid.CreateVersion4(), null, CancellationToken.None);
        var processLeak = await Consistency(directory, new LocationRegister(otherTenant),
            new InMemoryEventStore(), new OperationalProcessRegister(otherTenant))
            .GetOperationalProcessAsync(otherTenant, id, null, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(leak.Error).Kind);
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(missing.Error).Kind);
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(processLeak.Error).Kind);
    }

    [Fact]
    public async Task ShouldServeCurrentRevisionOnlyOnceProjectionReachesSourceGivenLaggingProjection()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var source = Locations(tenant, id);
        Assert.Null(source.Revise(id, 1, c => c with { Name = "Main" }, Author, Now.AddMinutes(1)));
        var directory = new FitzInventoryRegisterDirectory(new InMemoryKvClient());
        await Project(directory, tenant, new LocationRevisionRecorded(tenant, id, 1, Loc("HQ"), Author, Now));
        var consistency = Consistency(directory, source, new InMemoryEventStore());

        // Act
        var lagging = await new GetLocationHandler(consistency).HandleAsync(
            new RequestContext<GetLocation>(new GetLocation(tenant, id), new ClaimsPrincipal()), CancellationToken.None);
        var history = await new ListLocationRevisionsHandler(directory, consistency).HandleAsync(
            new RequestContext<ListLocationRevisions>(new ListLocationRevisions(tenant, id), new ClaimsPrincipal()),
            CancellationToken.None);
        await Project(directory, tenant, new LocationRevisionRecorded(tenant, id, 2, Loc("Main"), Author, Now.AddMinutes(1)));
        var caughtUp = await new GetLocationHandler(consistency).HandleAsync(
            new RequestContext<GetLocation>(new GetLocation(tenant, id, 2), new ClaimsPrincipal()), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(lagging.Error).Kind);
        Assert.True(Assert.IsType<RequestError>(lagging.Error).IsTransient);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(history.Error).Kind);
        Assert.Equal("Main", caughtUp.Value.Content.Name);
    }

    [Theory]
    [InlineData(0, RequestErrorKind.Validation)]
    [InlineData(201, RequestErrorKind.Validation)]
    [InlineData(1, RequestErrorKind.Conflict)]
    public async Task ShouldRejectInvalidLimitsOrEmptyStalePageGivenUnprojectedSource(int limit,
        RequestErrorKind expected)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var events = new InMemoryEventStore();
        await events.AppendAsync(new EventStreamAddress(tenant.ToString(), "inventory-registers", "locations"), 0,
            [DomainEventSeed.Attach(new LocationRevisionRecorded(tenant, id, 1, Loc("HQ"), Author, Now), tenant, 1)]);
        var directory = new FitzInventoryRegisterDirectory(new InMemoryKvClient());
        var consistency = Consistency(directory, Locations(tenant, id), events);

        // Act
        var locations = await new ListLocationsHandler(directory, consistency).HandleAsync(
            new RequestContext<ListLocations>(new ListLocations(tenant, limit), new ClaimsPrincipal()), CancellationToken.None);
        var processes = await new ListOperationalProcessesHandler(directory, consistency).HandleAsync(
            new RequestContext<ListOperationalProcesses>(new ListOperationalProcesses(tenant, limit), new ClaimsPrincipal()),
            CancellationToken.None);

        // Assert
        Assert.Equal(expected, Assert.IsType<RequestError>(locations.Error).Kind);
        Assert.Equal(expected, Assert.IsType<RequestError>(processes.Error).Kind);
    }

    [Fact]
    public async Task ShouldTranslateForeignCursorsAndIsolateTenantsGivenCaughtUpProjection()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var other = Uuid.CreateVersion4();
        var directory = new FitzInventoryRegisterDirectory(new InMemoryKvClient());
        await Project(directory, tenant,
            new OperationalProcessRevisionRecorded(tenant, Uuid.CreateVersion4(), 1, Proc("One"), Author, Now),
            new OperationalProcessRevisionRecorded(tenant, Uuid.CreateVersion4(), 1, Proc("Two"), Author, Now));
        var consistency = Consistency(directory, new LocationRegister(tenant), new InMemoryEventStore());
        var handler = new ListOperationalProcessesHandler(directory, consistency);
        var first = await handler.HandleAsync(new RequestContext<ListOperationalProcesses>(
            new ListOperationalProcesses(tenant, 1), new ClaimsPrincipal()), CancellationToken.None);

        // Act
        var foreign = await handler.HandleAsync(new RequestContext<ListOperationalProcesses>(
            new ListOperationalProcesses(other, 1, first.Value.NextCursor), new ClaimsPrincipal()), CancellationToken.None);
        var otherTenant = await handler.HandleAsync(new RequestContext<ListOperationalProcesses>(
            new ListOperationalProcesses(other), new ClaimsPrincipal()), CancellationToken.None);

        // Assert
        Assert.True(first.IsSuccess);
        Assert.Single(first.Value.Items);
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(foreign.Error).Kind);
        Assert.True(otherTenant.IsSuccess);
        Assert.Empty(otherTenant.Value.Items);
    }

    [Fact]
    public async Task ShouldReportActiveOnlyGivenRetiredOrForeignRegisterRecordsAsBoundarySubjects()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var locationId = Uuid.CreateVersion4();
        var processId = Uuid.CreateVersion4();
        var locations = Locations(tenant, locationId);
        var processes = new OperationalProcessRegister(tenant);
        Assert.True(processes.Record(processId, Proc("Backups"), Author, Now).IsSuccess);
        var references = new TechnologyInventoryReferences(new SourceReader(locations, processes), null!, null!);

        // Act
        var activeLocation = await references.IsActiveAsync(tenant, "location", locationId);
        var activeProcess = await references.IsActiveAsync(tenant, "process", processId);
        var wrongKind = await references.IsActiveAsync(tenant, "process", locationId);
        Assert.Null(locations.Revise(locationId, 1, c => c with { Lifecycle = "retired" }, Author, Now));
        var retired = await references.IsActiveAsync(tenant, "location", locationId);

        // Assert
        Assert.True(activeLocation);
        Assert.True(activeProcess);
        Assert.False(wrongKind);
        Assert.False(retired);
    }

    static async Task Project(FitzInventoryRegisterDirectory directory, Uuid tenant, params DomainEvent[] events)
    {
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(
            new CheckpointIdentity(FitzInventoryRegisterDirectory.ProjectorName,
                EventStreamPattern.ForPattern(tenant.ToString(), "inventory-registers")), ProjectionCheckpoint.Start));
        foreach (var ev in events)
            await directory.ApplyAsync(ev);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }

    static InventoryRegisterReadConsistency Consistency(FitzInventoryRegisterDirectory directory,
        LocationRegister locations, IDomainEventReader events, OperationalProcessRegister? processes = null) =>
        new(directory, new SourceReader(locations, processes), events);

    static LocationRegister Locations(Uuid tenant, Uuid id)
    {
        var source = new LocationRegister(tenant);
        Assert.True(source.Record(id, Loc("HQ"), Author, Now).IsSuccess);
        return source;
    }

    static LocationContent Loc(string name) => new("physical_site", name, null, Owner, "active");

    static OperationalProcessContent Proc(string name) =>
        new(name, "Purpose", Owner, null, null, "active");

    sealed class SourceReader(LocationRegister locations, OperationalProcessRegister? processes)
        : IAggregateReader
    {
        public ValueTask<T> HydrateAsync<T>(T aggregate, CancellationToken ct = default)
            where T : Aggregate =>
            ValueTask.FromResult(aggregate switch
            {
                LocationRegister => (T)(Aggregate)locations,
                OperationalProcessRegister => (T)(Aggregate)(processes ?? new OperationalProcessRegister(Uuid.CreateVersion4())),
                _ => aggregate,
            });
    }
}
