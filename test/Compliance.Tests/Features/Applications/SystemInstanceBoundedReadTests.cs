using System.Runtime.CompilerServices;
using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Applications;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class SystemInstanceBoundedReadTests
{
    const int Backlog = 1_000;

    [Fact]
    public async Task ShouldStopFenceScanAndReportTransientLagGivenBacklogBeyondScanLimit()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync();
        await fixture.AppendBacklogAsync(Backlog);
        var executor = new RejectingExecutor();
        var handler = new DeclareSystemInstanceHandler(executor,
            new SourceReader(fixture.Source), fixture.Directory, fixture.Reader,
            TimeProvider.System);
        var context = new RequestContext<DeclareSystemInstance>(new DeclareSystemInstance(
            fixture.TenantId, fixture.ApplicationId, 1, "Production", "production"),
            Actor());

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
        Assert.False(executor.Called);
        Assert.True(fixture.Reader.PatternRecords < Backlog,
            $"The fence read {fixture.Reader.PatternRecords} pending records.");
    }

    [Fact]
    public async Task ShouldStopListScanAndReportTransientLagGivenUnrelatedBacklogBeyondScanLimit()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync();
        await fixture.ProjectToHeadAsync();
        await fixture.AppendUnrelatedAsync(Backlog);
        var handler = new ListSystemInstancesHandler(fixture.Directory, fixture.Consistency());
        var request = new RequestContext<ListSystemInstances>(new ListSystemInstances(
            fixture.TenantId, fixture.ApplicationId), new ClaimsPrincipal());

        // Act
        var result = await handler.HandleAsync(request, CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
        Assert.True(fixture.Reader.PatternRecords < Backlog,
            $"The list check read {fixture.Reader.PatternRecords} pending records.");
    }

    [Fact]
    public async Task ShouldReadLegacyInstanceWithoutStreamScanGivenCaughtUpProjection()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync();
        var legacyId = Uuid.CreateVersion4();
        await fixture.AppendLegacyAsync(legacyId);
        await fixture.ProjectToHeadAsync();
        fixture.Reader.ForbidStreamReads = true;
        var handler = new GetSystemInstanceHandler(fixture.Directory, fixture.Consistency());

        // Act
        var legacy = await handler.HandleAsync(new RequestContext<GetSystemInstance>(
            new GetSystemInstance(fixture.TenantId, fixture.ApplicationId, legacyId,
                MinimumInstanceRevision: 1), new ClaimsPrincipal()), CancellationToken.None);
        var unknown = await handler.HandleAsync(new RequestContext<GetSystemInstance>(
            new GetSystemInstance(fixture.TenantId, fixture.ApplicationId,
                Uuid.CreateVersion4()), new ClaimsPrincipal()), CancellationToken.None);

        // Assert
        Assert.Equal(2, legacy.Value.LegacyApplicationRevision);
        Assert.Equal(RequestErrorKind.NotFound,
            Assert.IsType<RequestError>(unknown.Error).Kind);
        Assert.Equal(0, fixture.Reader.StreamReads);
    }

    static ClaimsPrincipal Actor() => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())],
        "test"));

    sealed class Fixture
    {
        readonly InMemoryEventStore _events;
        readonly DateTimeOffset _now = DateTimeOffset.UtcNow;
        readonly Uuid _actorId = Uuid.CreateVersion4();
        ulong _applicationVersion;

        Fixture(InMemoryEventStore events, Uuid tenantId, Uuid applicationId,
            DeclaredApplication source)
        {
            _events = events;
            TenantId = tenantId;
            ApplicationId = applicationId;
            Source = source;
            Reader = new GuardedEventReader(events);
            Directory = new FitzApplicationDirectory(new InMemoryKvClient());
        }

        public Uuid TenantId { get; }
        public Uuid ApplicationId { get; }
        public DeclaredApplication Source { get; }
        public GuardedEventReader Reader { get; }
        public FitzApplicationDirectory Directory { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var tenantId = Uuid.CreateVersion4();
            var applicationId = Uuid.CreateVersion4();
            var source = new DeclaredApplication(tenantId, applicationId);
            var fixture = new Fixture(new InMemoryEventStore(), tenantId, applicationId, source);
            Assert.True(source.Declare("Payroll", "Run payroll", null, fixture._actorId,
                "Manager", fixture._now).IsSuccess);
            await fixture.AppendApplicationAsync(new ApplicationDeclared(tenantId,
                applicationId, "Payroll", "Run payroll", null, fixture._actorId, "Manager",
                fixture._now));
            return fixture;
        }

        public SystemInstanceReadConsistency Consistency() =>
            new(Directory, new SourceReader(Source), new LegacySystemInstanceSource(Directory, Reader),
                Reader);

        public Task AppendLegacyAsync(Uuid instanceId) =>
            AppendApplicationAsync(new SystemInstanceDeclared(TenantId, ApplicationId,
                instanceId, 2, "Legacy", "production", null, "legacy-source", _actorId,
                "Legacy actor", _now));

        public async Task AppendBacklogAsync(int count)
        {
            for (var index = 0; index < count; index++)
            {
                var otherId = Uuid.CreateVersion4();
                await _events.AppendAsync(new EventStreamAddress(TenantId.ToString(),
                    "applications", otherId.ToString()), 0,
                [
                    DomainEventSeed.Attach(new ApplicationDeclared(TenantId, otherId,
                        "Other", "Other", null, _actorId, "Manager", _now), otherId, 1),
                ]);
            }
        }

        public async Task AppendUnrelatedAsync(int count)
        {
            var stream = new EventStreamAddress(TenantId.ToString(), "unrelated",
                Uuid.CreateVersion4().ToString());
            for (var index = 0; index < count; index++)
                await _events.AppendAsync(stream, (ulong)index,
                [
                    DomainEventSeed.Attach(new TeamDefined(TenantId, Uuid.CreateVersion4(),
                        "Team"), Uuid.CreateVersion4(), (ulong)index + 1),
                ]);
        }

        public async Task ProjectToHeadAsync()
        {
            var identity = new CheckpointIdentity("ApplicationDirectoryV2",
                EventStreamPattern.ForPattern(TenantId.ToString()));
            var checkpoint = await Directory.LoadCheckpointAsync(TenantId);
            await using var batch = await Directory.BeginAsync(new ProjectionBatchContext(
                identity, checkpoint));
            var cursor = checkpoint.Cursor;
            await foreach (var record in _events.ReadAsync(EventStreamPattern.ForPattern(
                               TenantId.ToString()), cursor, CancellationToken.None))
            {
                if (record.Event is ApplicationDeclared or ApplicationRevised or
                    SystemInstanceDeclared or SystemInstanceRegistered)
                    await Directory.ApplyAsync(record.Event);
                cursor = record.NextCursor;
            }
            await batch.CommitAsync(new ProjectionCheckpoint(cursor));
        }

        async Task AppendApplicationAsync(DomainEvent domainEvent)
        {
            await _events.AppendAsync(new EventStreamAddress(TenantId.ToString(),
                "applications", ApplicationId.ToString()), _applicationVersion,
            [
                DomainEventSeed.Attach(domainEvent, ApplicationId,
                    _applicationVersion + 1),
            ]);
            _applicationVersion++;
        }
    }

    sealed class GuardedEventReader(IDomainEventReader inner) : IDomainEventReader
    {
        public int PatternRecords { get; private set; }
        public int StreamReads { get; private set; }
        public bool ForbidStreamReads { get; set; }

        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream,
            ulong fromVersion, CancellationToken ct = default)
        {
            StreamReads++;
            return ForbidStreamReads
                ? throw new InvalidOperationException("The legacy stream must not be scanned.")
                : inner.ReadAsync(stream, fromVersion, ct);
        }

        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern,
            EventCursor after, [EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var record in inner.ReadAsync(pattern, after, ct)
                               .ConfigureAwait(false))
            {
                PatternRecords++;
                yield return record;
            }
        }
    }

    sealed class SourceReader(DeclaredApplication source) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult(aggregate is DeclaredApplication && aggregate.Id == source.Id
                ? (TAggregate)(Aggregate)source : aggregate);
    }

    sealed class RejectingExecutor : IAggregateExecutor
    {
        public bool Called { get; private set; }

        public ValueTask<Result> ExecuteAsync<TAggregate>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome> operation, IExecutionContext context,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            Called = true;
            throw new InvalidOperationException("The instance stream must not be written.");
        }

        public ValueTask<Result<TOut>> ExecuteAsync<TAggregate, TOut>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome<TOut>> operation, IExecutionContext context,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            Called = true;
            throw new InvalidOperationException("The instance stream must not be written.");
        }
    }
}
