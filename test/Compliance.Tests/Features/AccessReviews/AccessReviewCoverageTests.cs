using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessReviews;

public sealed class AccessReviewCoverageTests
{
    [Fact]
    public async Task ShouldReconcileEverySystemInstanceGivenMoreThanOneThousandInstances()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var instances = Enumerable.Range(0, 1001)
            .Select(index => new SystemInstanceView(tenantId, applicationId,
                Uuid.CreateVersion4(), $"Instance {index}", "aws_account", null, "manual",
                null, [], Uuid.CreateVersion4(), "Manager", DateTimeOffset.UtcNow)
            {
                Revision = 1,
            }).ToArray();
        var directory = new ApplicationDirectory(tenantId, applicationId, instances);
        var reader = new SourceReader(tenantId, applicationId);
        var events = new InMemoryEventStore();
        var handler = CreateHandler(directory,
            new ScopeDirectory(), new PopulationDirectory(), reader, events);
        var request = new GetAccessReviewCoverage(tenantId, applicationId);

        // Act
        var result = await handler.HandleAsync(new RequestContext<GetAccessReviewCoverage>(
            request, new ClaimsPrincipal()), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(instances.Length, result.Value.Instances.Count);
        Assert.Equal(instances.Select(instance => instance.SystemInstanceId),
            result.Value.Instances.Select(instance => instance.SystemInstanceId));
    }

    [Fact]
    public async Task ShouldReconcileAcceptedExceptedMissingExcludedAndUnresolvedSystemsGivenAsOf()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var asOf = DateTimeOffset.UtcNow;
        var instances = CreateInstances(tenantId, applicationId, 5);
        var acceptedInstance = instances[0];
        var exceptedInstance = instances[1];
        var missingInstance = instances[2];
        var excludedInstance = instances[3];
        var unresolvedInstance = instances[4];
        var scopeActor = ActorReference.ForMember(Uuid.CreateVersion4(), "Compliance Lead");
        var scopes = new ScopeDirectory(
        [
            Record(acceptedInstance, "included", asOf.AddDays(-2), scopeActor),
            Record(exceptedInstance, "included", asOf.AddDays(-2), scopeActor),
            Record(missingInstance, "included", asOf.AddDays(-2), scopeActor),
            Record(excludedInstance, "excluded", asOf.AddDays(-2), scopeActor),
        ]);
        var acceptedPopulationId = Uuid.CreateVersion4();
        var acceptedSnapshotId = Uuid.CreateVersion4();
        var exceptionPopulationId = Uuid.CreateVersion4();
        var exceptionSnapshotId = Uuid.CreateVersion4();
        var populations = new PopulationDirectory(
        [
            new AccessPopulationSummaryView(tenantId, acceptedPopulationId, applicationId,
                acceptedInstance.SystemInstanceId, 1, asOf.AddDays(-1), 3,
                AccessPopulation.Accepted, acceptedSnapshotId, new string('a', 64), asOf.AddHours(-1)),
            new AccessPopulationSummaryView(tenantId, exceptionPopulationId, applicationId,
                exceptedInstance.SystemInstanceId, 1, asOf.AddDays(-1), 3,
                AccessPopulation.Accepted, exceptionSnapshotId, new string('b', 64), asOf.AddMinutes(1)),
            new AccessPopulationSummaryView(tenantId, Uuid.CreateVersion4(), applicationId,
                missingInstance.SystemInstanceId, 1, asOf.AddDays(-1), 2,
                AccessPopulation.Draft, null, null, null),
        ]);
        var ledger = new AccessReviewSystemLedger(tenantId, exceptedInstance.SystemInstanceId);
        var exceptionResult = ledger.RecordPopulationException(Uuid.CreateVersion4(), 0,
            "Source export is temporarily unavailable.", asOf.AddDays(1), scopeActor,
            asOf.AddDays(-1));
        Assert.True(exceptionResult.IsSuccess);
        var reader = new SourceReader(tenantId, applicationId,
            new Dictionary<Uuid, AccessReviewSystemLedger>
            {
                [exceptedInstance.SystemInstanceId] = ledger,
            });
        var directory = new ApplicationDirectory(tenantId, applicationId, instances);
        var events = new InMemoryEventStore();
        var handler = CreateHandler(directory, scopes, populations,
            reader, events);

        // Act
        var result = await handler.HandleAsync(new RequestContext<GetAccessReviewCoverage>(
            new GetAccessReviewCoverage(tenantId, applicationId, asOf), new ClaimsPrincipal()),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(asOf, result.Value.AsOf);
        var covered = Assert.Single(result.Value.Instances, item =>
            item.SystemInstanceId == acceptedInstance.SystemInstanceId);
        Assert.Equal("covered", covered.Coverage);
        Assert.Equal(acceptedPopulationId, covered.PopulationId);
        Assert.Equal(acceptedSnapshotId, covered.SnapshotId);
        var excepted = Assert.Single(result.Value.Instances, item =>
            item.SystemInstanceId == exceptedInstance.SystemInstanceId);
        Assert.Equal("excepted", excepted.Coverage);
        Assert.Equal("Source export is temporarily unavailable.", excepted.Exception?.Reason);
        var missing = Assert.Single(result.Value.Instances, item =>
            item.SystemInstanceId == missingInstance.SystemInstanceId);
        Assert.Equal("missing_population", missing.Coverage);
        Assert.Null(missing.PopulationId);
        Assert.Null(missing.SnapshotId);
        Assert.Equal("not_in_scope", Assert.Single(result.Value.Instances, item =>
            item.SystemInstanceId == excludedInstance.SystemInstanceId).Coverage);
        Assert.Equal("scope_unresolved", Assert.Single(result.Value.Instances, item =>
            item.SystemInstanceId == unresolvedInstance.SystemInstanceId).Coverage);
    }

    [Fact]
    public async Task ShouldHideForeignPopulationDirectoryItemGivenCoverageRead()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var instance = Assert.Single(CreateInstances(tenantId, applicationId, 1));
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Compliance Lead");
        var asOf = DateTimeOffset.UtcNow;
        var populations = new PopulationDirectory(
        [
            new AccessPopulationSummaryView(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
                applicationId, instance.SystemInstanceId, 1, asOf.AddDays(-1), 3,
                AccessPopulation.Accepted, Uuid.CreateVersion4(), new string('c', 64),
                asOf.AddHours(-1)),
        ]);
        var reader = new SourceReader(tenantId, applicationId);
        var directory = new ApplicationDirectory(tenantId, applicationId, [instance]);
        var events = new InMemoryEventStore();
        var handler = CreateHandler(directory,
            new ScopeDirectory([Record(instance, "included", asOf.AddDays(-2), actor)]),
            populations, reader, events);

        // Act
        var result = await handler.HandleAsync(new RequestContext<GetAccessReviewCoverage>(
            new GetAccessReviewCoverage(tenantId, applicationId, asOf), new ClaimsPrincipal()),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Fact]
    public async Task ShouldNotReportCoverageWithoutSnapshotIdentityGivenAcceptedStatus()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var instance = Assert.Single(CreateInstances(tenantId, applicationId, 1));
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Compliance Lead");
        var asOf = DateTimeOffset.UtcNow;
        var populations = new PopulationDirectory(
        [
            new AccessPopulationSummaryView(tenantId, Uuid.CreateVersion4(), applicationId,
                instance.SystemInstanceId, 1, asOf.AddDays(-1), 3,
                AccessPopulation.Accepted, null, null, asOf.AddHours(-1)),
        ]);
        var reader = new SourceReader(tenantId, applicationId);
        var directory = new ApplicationDirectory(tenantId, applicationId, [instance]);
        var events = new InMemoryEventStore();
        var handler = CreateHandler(directory,
            new ScopeDirectory([Record(instance, "included", asOf.AddDays(-2), actor)]),
            populations, reader, events);

        // Act
        var result = await handler.HandleAsync(new RequestContext<GetAccessReviewCoverage>(
            new GetAccessReviewCoverage(tenantId, applicationId, asOf), new ClaimsPrincipal()),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var coverage = Assert.Single(result.Value.Instances);
        Assert.Equal("missing_population", coverage.Coverage);
        Assert.Null(coverage.PopulationId);
        Assert.Null(coverage.SnapshotId);
    }

    [Fact]
    public async Task ShouldHideForeignScopeDirectoryItemGivenCoverageRead()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var instance = Assert.Single(CreateInstances(tenantId, applicationId, 1));
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Compliance Lead");
        var asOf = DateTimeOffset.UtcNow;
        var foreignScope = Record(instance, "included", asOf.AddDays(-1), actor) with
        {
            TenantId = Uuid.CreateVersion4(),
        };
        var reader = new SourceReader(tenantId, applicationId);
        var directory = new ApplicationDirectory(tenantId, applicationId, [instance]);
        var events = new InMemoryEventStore();
        var handler = CreateHandler(directory,
            new ScopeDirectory([foreignScope]), new PopulationDirectory(), reader, events);

        // Act
        var result = await handler.HandleAsync(new RequestContext<GetAccessReviewCoverage>(
            new GetAccessReviewCoverage(tenantId, applicationId, asOf), new ClaimsPrincipal()),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Fact]
    public async Task ShouldHideForeignScopeDecisionGivenCoverageRead()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var instance = Assert.Single(CreateInstances(tenantId, applicationId, 1));
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Compliance Lead");
        var asOf = DateTimeOffset.UtcNow;
        var scope = Record(instance, "included", asOf.AddDays(-1), actor);
        var decision = Assert.Single(scope.Decisions) with
        {
            SystemInstanceId = Uuid.CreateVersion4(),
        };
        var reader = new SourceReader(tenantId, applicationId);
        var directory = new ApplicationDirectory(tenantId, applicationId, [instance]);
        var events = new InMemoryEventStore();
        var handler = CreateHandler(directory,
            new ScopeDirectory([scope with { Decisions = [decision] }]),
            new PopulationDirectory(), reader, events);

        // Act
        var result = await handler.HandleAsync(new RequestContext<GetAccessReviewCoverage>(
            new GetAccessReviewCoverage(tenantId, applicationId, asOf), new ClaimsPrincipal()),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(result.Error).Kind);
    }

    static GetAccessReviewCoverageHandler CreateHandler(ApplicationDirectory directory,
        ScopeDirectory scopes, PopulationDirectory populations,
        IAggregateReader reader, IDomainEventReader events)
    {
        var consistency = new SystemInstanceReadConsistency(directory, reader,
            new LegacySystemInstanceSource(directory, events), events);
        return new GetAccessReviewCoverageHandler(directory, consistency, scopes, populations,
            reader, events, TimeProvider.System,
            RestrictedApplicationVisibilityFixture.Create(reader));
    }

    static SystemInstanceView[] CreateInstances(Uuid tenantId, Uuid applicationId, int count) =>
        Enumerable.Range(0, count)
            .Select(index => new SystemInstanceView(tenantId, applicationId,
                Uuid.CreateVersion4(), $"Instance {index}", "aws_account", null, "manual",
                null, [], Uuid.CreateVersion4(), "Manager", DateTimeOffset.UtcNow)
            {
                Revision = 1,
            }).ToArray();

    static AccessReviewScopeRecord Record(SystemInstanceView instance, string decision,
        DateTimeOffset effectiveFrom, ActorReference actor) =>
        new(instance.TenantId, instance.ApplicationId, instance.SystemInstanceId,
        [new AccessReviewScopeDecisionView(instance.TenantId, instance.ApplicationId,
            instance.SystemInstanceId, instance.Revision, Uuid.CreateVersion4(), 1, decision,
            "Reviewed scope.", effectiveFrom, null, actor, effectiveFrom, null)]);

    sealed class SourceReader : IAggregateReader
    {
        readonly Uuid _tenantId;
        readonly Uuid _applicationId;
        readonly IReadOnlyDictionary<Uuid, AccessReviewSystemLedger> _ledgers;

        public SourceReader(Uuid tenantId, Uuid applicationId,
            IReadOnlyDictionary<Uuid, AccessReviewSystemLedger>? ledgers = null)
        {
            _tenantId = tenantId;
            _applicationId = applicationId;
            _ledgers = ledgers ?? new Dictionary<Uuid, AccessReviewSystemLedger>();
        }

        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            if (aggregate is DeclaredApplication)
            {
                var application = new DeclaredApplication(_tenantId, _applicationId);
                Assert.True(application.Declare("Payroll", "Run payroll", null,
                Uuid.CreateVersion4(), "Manager", DateTimeOffset.UtcNow).IsSuccess);
                return ValueTask.FromResult((TAggregate)(Aggregate)application);
            }
            return ValueTask.FromResult(aggregate is AccessReviewSystemLedger ledger &&
                _ledgers.TryGetValue(ledger.Id, out var recorded)
                ? (TAggregate)(Aggregate)recorded
                : aggregate);
        }
    }

    sealed class ApplicationDirectory : IApplicationDirectoryReader
    {
        readonly Uuid _tenantId;
        readonly Uuid _applicationId;
        readonly IReadOnlyList<SystemInstanceView> _instances;

        public ApplicationDirectory(Uuid tenantId, Uuid applicationId,
            IReadOnlyList<SystemInstanceView> instances)
        {
            _tenantId = tenantId;
            _applicationId = applicationId;
            _instances = instances;
        }

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);

        public ValueTask<ApplicationView?> GetAsync(Uuid tenantId, Uuid applicationId,
            CancellationToken ct = default) => ValueTask.FromResult<ApplicationView?>(
            tenantId == _tenantId && applicationId == _applicationId
                ? new ApplicationView(tenantId, applicationId, 1, "Payroll", "Run payroll",
                    null, "manual", "payroll", true, [], Uuid.CreateVersion4(), "Manager",
                    DateTimeOffset.UtcNow)
                : null);

        public ValueTask<Page<ApplicationView>> ListAsync(Uuid tenantId, int limit,
            string? cursor, CancellationToken ct = default) => throw new NotSupportedException();

        public ValueTask<ApplicationRevisionView?> GetRevisionAsync(Uuid tenantId,
            Uuid applicationId, long revision, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public ValueTask<Page<ApplicationRevisionView>?> ListRevisionsAsync(Uuid tenantId,
            Uuid applicationId, int limit, string? cursor, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public ValueTask<SystemInstanceView?> GetInstanceAsync(Uuid tenantId, Uuid instanceId,
            CancellationToken ct = default) => ValueTask.FromResult(_instances.FirstOrDefault(
            instance => instance.TenantId == tenantId && instance.SystemInstanceId == instanceId));

        public ValueTask<Page<SystemInstanceView>> ListInstancesAsync(Uuid tenantId,
            Uuid applicationId, int limit, string? cursor, CancellationToken ct = default)
        {
            var offset = cursor is null ? 0 : int.Parse(cursor,
                System.Globalization.CultureInfo.InvariantCulture);
            var page = _instances.Skip(offset).Take(limit).ToArray();
            var next = offset + page.Length < _instances.Count
                ? (offset + page.Length).ToString(System.Globalization.CultureInfo.InvariantCulture)
                : null;
            return ValueTask.FromResult(new Page<SystemInstanceView>(page, next));
        }
    }

    sealed class ScopeDirectory(IReadOnlyDictionary<Uuid, AccessReviewScopeRecord> records)
        : IAccessReviewScopeDirectoryReader
    {
        public ScopeDirectory(IEnumerable<AccessReviewScopeRecord>? records = null)
            : this((records ?? []).ToDictionary(record => record.SystemInstanceId))
        {
        }

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);

        public ValueTask<AccessReviewScopeRecord?> GetAsync(Uuid tenantId,
            Uuid systemInstanceId, CancellationToken ct = default) =>
            ValueTask.FromResult(records.GetValueOrDefault(systemInstanceId));
    }

    sealed class PopulationDirectory : IAccessPopulationDirectoryReader
    {
        readonly IReadOnlyDictionary<Uuid, AccessPopulationSummaryView[]> _populations;

        public PopulationDirectory(IEnumerable<AccessPopulationSummaryView>? populations = null)
        {
            _populations = (populations ?? []).GroupBy(population => population.SystemInstanceId)
                .ToDictionary(group => group.Key, group => group.ToArray());
        }

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);

        public ValueTask<Page<AccessPopulationSummaryView>> ListAsync(Uuid tenantId,
            Uuid systemInstanceId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<AccessPopulationSummaryView>(
                _populations.GetValueOrDefault(systemInstanceId) ?? [], null));
    }
}
