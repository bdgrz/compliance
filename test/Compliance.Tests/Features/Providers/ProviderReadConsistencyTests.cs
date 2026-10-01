using System.Security.Claims;
using Bdgrz.Compliance.Features.Providers;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Providers;

public sealed class ProviderReadConsistencyTests
{
    [Theory]
    [InlineData(0, RequestErrorKind.Validation)]
    [InlineData(1, RequestErrorKind.Conflict)]
    [InlineData(2, RequestErrorKind.Conflict)]
    public async Task ShouldDistinguishInvalidRevisionAndLagGivenRecordedUnprojectedProvider(long minimum, RequestErrorKind expected)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var source = Source(tenant, id);
        var consistency = new ProviderReadConsistency(new FitzProviderDirectory(new InMemoryKvClient()),
            new SourceReader(source), new InMemoryEventStore());

        // Act
        var result = await consistency.GetAsync(tenant, id, minimum, CancellationToken.None);
        var historical = await consistency.GetRevisionAsync(tenant, id, minimum, CancellationToken.None);

        // Assert
        Assert.Equal(expected, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Equal(minimum == 2 ? RequestErrorKind.NotFound : expected, Assert.IsType<RequestError>(historical.Error).Kind);
    }

    [Fact]
    public async Task ShouldReturnNotFoundGivenUnknownProviderEvenWithProjectedOtherProvider()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var consistency = new ProviderReadConsistency(new FitzProviderDirectory(new InMemoryKvClient()),
            new SourceReader(new ProviderRegister(tenant)), new InMemoryEventStore());

        // Act
        var result = await consistency.GetAsync(tenant, Uuid.CreateVersion4(), null, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Theory]
    [InlineData(0, RequestErrorKind.Validation)]
    [InlineData(201, RequestErrorKind.Validation)]
    [InlineData(1, RequestErrorKind.Conflict)]
    public async Task ShouldRejectInvalidLimitsOrEmptyStalePageGivenUnprojectedSource(int limit, RequestErrorKind expected)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var ev = new ProviderRecorded(tenant, id, Uuid.CreateVersion4(), new ProviderContent("Provider", "Supplier"),
            ActorReference.ForMember(Uuid.CreateVersion4(), "Recorder"), DateTimeOffset.UtcNow);
        var events = new InMemoryEventStore();
        await events.AppendAsync(new EventStreamAddress(tenant.ToString(), ProviderRegister.Area, tenant.ToString()), 0,
            [DomainEventSeed.Attach(ev, tenant, 1)]);
        var directory = new FitzProviderDirectory(new InMemoryKvClient());
        var consistency = new ProviderReadConsistency(directory, new SourceReader(Source(tenant, id)), events);

        // Act
        var result = await new ListProvidersHandler(directory, consistency).HandleAsync(
            new RequestContext<ListProviders>(new ListProviders(tenant, limit), new ClaimsPrincipal()), CancellationToken.None);

        // Assert
        Assert.Equal(expected, Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Fact]
    public async Task ShouldReadRetainedRevisionGivenNewerCanonicalRevisionAndProjectionLag()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var author = ActorReference.ForMember(Uuid.CreateVersion4(), "Recorder");
        var now = DateTimeOffset.UtcNow;
        var source = new ProviderRegister(tenant);
        Assert.True(source.Record(id, Uuid.CreateVersion4(), new ProviderContent("Original", "Supplier"), author, now).IsSuccess);
        Assert.True(source.Revise(id, Uuid.CreateVersion4(), 1, new ProviderContent("Revised", "Supplier"), author, now.AddMinutes(1)).IsSuccess);
        var directory = new FitzProviderDirectory(new InMemoryKvClient());
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
            new CheckpointIdentity(FitzProviderDirectory.ProjectorName,
                EventStreamPattern.ForPattern(tenant.ToString(), ProviderRegister.Area)), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new ProviderRecorded(tenant, id, Uuid.CreateVersion4(),
                new ProviderContent("Original", "Supplier"), author, now));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var consistency = new ProviderReadConsistency(directory, new SourceReader(source), new InMemoryEventStore());

        // Act
        var historical = await new GetProviderRevisionHandler(consistency).HandleAsync(
            new RequestContext<GetProviderRevision>(new GetProviderRevision(tenant, id, 1), new ClaimsPrincipal()), CancellationToken.None);
        var current = await new GetProviderHandler(consistency).HandleAsync(
            new RequestContext<GetProvider>(new GetProvider(tenant, id), new ClaimsPrincipal()), CancellationToken.None);
        var missing = await consistency.GetRevisionAsync(tenant, id, 3, CancellationToken.None);
        var history = await new ListProviderRevisionsHandler(directory, consistency).HandleAsync(
            new RequestContext<ListProviderRevisions>(new ListProviderRevisions(tenant, id), new ClaimsPrincipal()), CancellationToken.None);

        // Assert
        Assert.True(historical.IsSuccess);
        Assert.Equal("Original", historical.Value.Content.Name);
        Assert.Equal(author, historical.Value.RecordedBy);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(current.Error).Kind);
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(missing.Error).Kind);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(history.Error).Kind);
    }

    [Fact]
    public async Task ShouldTranslateScopedCursorAndPageHistoryGivenCaughtUpProjection()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var directory = new FitzProviderDirectory(new InMemoryKvClient());
        var source = Source(tenant, id);
        var author = ActorReference.ForMember(Uuid.CreateVersion4(), "Recorder");
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
            new CheckpointIdentity(FitzProviderDirectory.ProjectorName,
                EventStreamPattern.ForPattern(tenant.ToString(), ProviderRegister.Area)), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new ProviderRecorded(tenant, id, Uuid.CreateVersion4(), new ProviderContent("Provider", "Supplier"), author, DateTimeOffset.UtcNow));
            await directory.ApplyAsync(new ProviderRecorded(tenant, Uuid.CreateVersion4(), Uuid.CreateVersion4(), new ProviderContent("Second", "Supplier"), author, DateTimeOffset.UtcNow));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var consistency = new ProviderReadConsistency(directory, new SourceReader(source), new InMemoryEventStore());
        var first = await new ListProvidersHandler(directory, consistency).HandleAsync(
            new RequestContext<ListProviders>(new ListProviders(tenant, 1), new ClaimsPrincipal()), CancellationToken.None);

        // Act
        var foreign = await new ListProvidersHandler(directory, consistency).HandleAsync(
            new RequestContext<ListProviders>(new ListProviders(Uuid.CreateVersion4(), 1, first.Value.NextCursor), new ClaimsPrincipal()), CancellationToken.None);
        var history = await new ListProviderRevisionsHandler(directory, consistency).HandleAsync(
            new RequestContext<ListProviderRevisions>(new ListProviderRevisions(tenant, id, 1), new ClaimsPrincipal()), CancellationToken.None);
        var invalidHistory = await new ListProviderRevisionsHandler(directory, consistency).HandleAsync(
            new RequestContext<ListProviderRevisions>(new ListProviderRevisions(tenant, id, 1, first.Value.NextCursor), new ClaimsPrincipal()), CancellationToken.None);

        // Assert
        Assert.True(first.IsSuccess);
        Assert.NotNull(first.Value.NextCursor);
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(foreign.Error).Kind);
        Assert.True(history.IsSuccess);
        Assert.Equal(id, Assert.Single(history.Value.Items).ProviderId);
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(invalidHistory.Error).Kind);
    }

    static ProviderRegister Source(Uuid tenant, Uuid id)
    {
        var source = new ProviderRegister(tenant);
        Assert.True(source.Record(id, Uuid.CreateVersion4(), new ProviderContent("Provider", "Supplier"),
            ActorReference.ForMember(Uuid.CreateVersion4(), "Recorder"), DateTimeOffset.UtcNow).IsSuccess);
        return source;
    }

    sealed class SourceReader(ProviderRegister source) : IAggregateReader
    {
        public ValueTask<T> HydrateAsync<T>(T aggregate, CancellationToken ct = default) where T : Aggregate =>
            ValueTask.FromResult((T)(Aggregate)source);
    }
}
