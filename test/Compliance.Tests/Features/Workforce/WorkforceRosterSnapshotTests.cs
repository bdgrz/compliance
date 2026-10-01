using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Snapshots;
using Bdgrz.Compliance.Features.Workforce;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Workforce;

public sealed class WorkforceRosterSnapshotTests
{
    static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");

    [Fact]
    public void ShouldHashRosterDeterministicallyGivenReorderedEquivalentFacts()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var alice = Person(tenantId, "Alice");
        var bob = Person(tenantId, "Bob");
        var aliceJob = Relationship(tenantId, alice.PersonId, "E-200", bob.PersonId);
        var bobJob = Relationship(tenantId, bob.PersonId, "E-100", null);

        // Act
        var first = WorkforceRosterSnapshotContent.Rows([alice, bob], [aliceJob, bobJob]);
        var reordered = WorkforceRosterSnapshotContent.Rows([bob, alice], [bobJob, aliceJob]);
        var changed = WorkforceRosterSnapshotContent.Rows([alice, bob],
            [aliceJob with { Department = "Finance" }, bobJob]);
        var firstDigest = PopulationContentIdentity.Compute(WorkforceRosterSnapshotContent.Kind, first);
        var reorderedDigest = PopulationContentIdentity.Compute(WorkforceRosterSnapshotContent.Kind,
            reordered);
        var changedDigest = PopulationContentIdentity.Compute(WorkforceRosterSnapshotContent.Kind,
            changed);

        // Assert
        Assert.Equal(4, first.Count);
        Assert.Equal(firstDigest.Value.Sha256, reorderedDigest.Value.Sha256);
        Assert.NotEqual(firstDigest.Value.Sha256, changedDigest.Value.Sha256);
    }

    [Fact]
    public void ShouldRejectForgedDigestAndChangedRetryGivenFrozenPopulation()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var snapshotId = Uuid.CreateVersion4();
        var person = Person(tenantId, "Alice");
        var rows = WorkforceRosterSnapshotContent.Rows([person], []);
        var digest = PopulationContentIdentity.Compute(WorkforceRosterSnapshotContent.Kind, rows).Value;
        var snapshot = new PopulationSnapshot(tenantId, snapshotId);
        var otherRows = WorkforceRosterSnapshotContent.Rows([person with { DisplayName = "Eve" }], []);
        var otherDigest = PopulationContentIdentity.Compute(WorkforceRosterSnapshotContent.Kind,
            otherRows).Value;

        // Act
        var forged = snapshot.Freeze(snapshotId, null, WorkforceRosterSnapshotContent.Kind, rows,
            new string('a', 64), null, Author, Now);
        var frozen = snapshot.Freeze(snapshotId, null, WorkforceRosterSnapshotContent.Kind, rows,
            digest.Sha256, null, Author, Now);
        var replay = snapshot.Freeze(snapshotId, null, WorkforceRosterSnapshotContent.Kind, rows,
            digest.Sha256, null, Author, Now);
        var changed = snapshot.Freeze(snapshotId, null, WorkforceRosterSnapshotContent.Kind,
            otherRows, otherDigest.Sha256, null, Author, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(forged.Error).Kind);
        Assert.True(frozen.IsSuccess);
        Assert.True(replay.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(changed.Error).Kind);
        Assert.Single(new AggregateScenario<PopulationSnapshot>(snapshot).PendingEvents);
        Assert.Equal(1, snapshot.RowCount);
    }

    [Fact]
    public async Task ShouldKeepFrozenRosterAndLinkAmendmentGivenLaterRosterChange()
    {
        // Arrange
        await using var harness = await Harness.CreateAsync();
        var tenantId = harness.TenantId;
        var manager = Person(tenantId, "Morgan");
        var worker = Person(tenantId, "Wes");
        var job = Relationship(tenantId, worker.PersonId, "E-100", manager.PersonId);
        await harness.ProjectAsync([manager, worker], [job]);
        var freeze = await harness.FreezeAsync();
        var original = await harness.GetAsync(freeze.SnapshotId, canReadManagerChain: true);
        await harness.ProjectAsync([], [job with { Revision = 2, Department = "Finance" }]);

        // Act
        var amended = await harness.AmendAsync(freeze.SnapshotId, "Department corrected");
        var reread = await harness.GetAsync(freeze.SnapshotId, canReadManagerChain: true);
        var amendment = await harness.GetAsync(amended.SnapshotId, canReadManagerChain: true);

        // Assert
        Assert.Equal(original.ContentSha256, reread.ContentSha256);
        Assert.Equal("Engineering", Assert.Single(reread.WorkRelationships).Department);
        Assert.Equal("Finance", Assert.Single(amendment.WorkRelationships).Department);
        Assert.NotEqual(original.ContentSha256, amendment.ContentSha256);
        Assert.Equal(freeze.SnapshotId, amendment.AmendsSnapshotId);
        Assert.Equal(freeze.SnapshotId, amendment.RootSnapshotId);
        Assert.Equal("Department corrected", amendment.AmendmentReason);
        Assert.Equal(harness.ActorDisplay, amendment.FrozenBy.Display);
        Assert.Null(reread.AmendsSnapshotId);
        Assert.Equal(2, reread.People.Count);
    }

    [Fact]
    public async Task ShouldRedactManagerChainGivenActorWithoutRestrictedFieldPermission()
    {
        // Arrange
        await using var harness = await Harness.CreateAsync();
        var manager = Person(harness.TenantId, "Morgan");
        var worker = Person(harness.TenantId, "Wes");
        await harness.ProjectAsync([manager, worker],
            [Relationship(harness.TenantId, worker.PersonId, "E-100", manager.PersonId)]);
        var freeze = await harness.FreezeAsync();

        // Act
        var general = await harness.GetAsync(freeze.SnapshotId, canReadManagerChain: false);
        var restricted = await harness.GetAsync(freeze.SnapshotId, canReadManagerChain: true);

        // Assert
        Assert.True(general.RestrictedFieldsRedacted);
        Assert.Null(Assert.Single(general.WorkRelationships).ManagerPersonId);
        Assert.True(restricted.RestrictedFieldsRedacted);
        Assert.Equal(manager.PersonId, Assert.Single(restricted.WorkRelationships).ManagerPersonId);
        Assert.Equal(general.ContentSha256, restricted.ContentSha256);
    }

    [Fact]
    public async Task ShouldRejectFreezeWithoutWritingGivenLaggingRosterProjection()
    {
        // Arrange
        await using var harness = await Harness.CreateAsync();
        var person = Person(harness.TenantId, "Wes");
        await harness.ProjectAsync([person], []);
        await harness.RecordUnprojectedRelationshipAsync(person.PersonId);

        // Act
        var result = await harness.TryFreezeAsync();

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
        Assert.False((await harness.HydrateAsync(result.RequestId)).IsFrozen);
    }

    [Fact]
    public async Task ShouldRejectFreezeWithoutWritingGivenRosterProjectionAdvancedDuringRead()
    {
        // Arrange
        await using var harness = await Harness.CreateAsync();
        var person = Person(harness.TenantId, "Wes");
        var job = Relationship(harness.TenantId, person.PersonId, "E-100", null);
        await harness.ProjectAsync([person], [job]);
        harness.AdvanceDuringRead(job with { Revision = 2, Department = "Finance" });

        // Act
        var result = await harness.TryFreezeAsync();

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
        Assert.False((await harness.HydrateAsync(result.RequestId)).IsFrozen);
    }

    [Fact]
    public async Task ShouldRedactManagerOnLiveReadGivenActorWithoutRestrictedFieldPermission()
    {
        // Arrange
        await using var harness = await Harness.CreateAsync();
        var manager = Person(harness.TenantId, "Morgan");
        var worker = Person(harness.TenantId, "Wes");
        var relationshipId = await harness.RecordRelationshipAsync(worker.PersonId,
            manager.PersonId);

        // Act
        var general = await harness.GetRelationshipAsync(relationshipId, canReadManagerChain: false);
        var restricted = await harness.GetRelationshipAsync(relationshipId,
            canReadManagerChain: true);

        // Assert
        Assert.True(general.Value.RestrictedFieldsRedacted);
        Assert.Null(general.Value.ManagerPersonId);
        Assert.Null(general.Value.EmploymentStatusReason);
        Assert.False(restricted.Value.RestrictedFieldsRedacted);
        Assert.Equal(manager.PersonId, restricted.Value.ManagerPersonId);
        Assert.Equal("retirement", restricted.Value.EmploymentStatusReason);
    }

    [Fact]
    public async Task ShouldKeepPersonalDetailsSeparateFromManagerChainGrantGivenSingleRelationshipRead()
    {
        // Arrange
        await using var harness = await Harness.CreateAsync();
        var manager = Person(harness.TenantId, "Morgan");
        var worker = Person(harness.TenantId, "Wes");
        var relationshipId = await harness.RecordRelationshipAsync(worker.PersonId,
            manager.PersonId);

        // Act
        var managerOnly = await harness.GetRelationshipAsync(relationshipId,
            new GrantedPermissions(FieldClasses.WorkforceManagerChain.ReadPermission));
        var personalOnly = await harness.GetRelationshipAsync(relationshipId,
            new GrantedPermissions(FieldClasses.WorkforcePersonalDetails.ReadPermission));

        // Assert
        Assert.Equal(manager.PersonId, managerOnly.Value.ManagerPersonId);
        Assert.Null(managerOnly.Value.EmploymentStatusReason);
        Assert.True(managerOnly.Value.RestrictedFieldsRedacted);
        Assert.Null(personalOnly.Value.ManagerPersonId);
        Assert.Equal("retirement", personalOnly.Value.EmploymentStatusReason);
        Assert.True(personalOnly.Value.RestrictedFieldsRedacted);
    }

    [Fact]
    public async Task ShouldNotDiscloseSnapshotGivenOtherTenant()
    {
        // Arrange
        await using var harness = await Harness.CreateAsync();
        await harness.ProjectAsync([Person(harness.TenantId, "Wes")], []);
        var freeze = await harness.FreezeAsync();

        // Act
        var foreign = await harness.TryGetAsync(Uuid.CreateVersion4(), freeze.SnapshotId);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(foreign.Error).Kind);
    }

    [Fact]
    public async Task ShouldReturnLatestSnapshotAtOrBeforeInstantGivenAsOfRead()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var directory = new FitzPopulationSnapshotDirectory(new InMemoryKvClient());
        var person = Person(tenantId, "Wes");
        var rows = WorkforceRosterSnapshotContent.Rows([person], []);
        var digest = PopulationContentIdentity.Compute(WorkforceRosterSnapshotContent.Kind, rows).Value;
        var snapshots = new List<PopulationSnapshot>();
        foreach (var offset in new[] { 0, 10, 20 })
        {
            var id = Uuid.CreateVersion4();
            var snapshot = new PopulationSnapshot(tenantId, id);
            Assert.True(snapshot.Freeze(id, null, WorkforceRosterSnapshotContent.Kind, rows,
                digest.Sha256, null, Author, Now.AddMinutes(offset)).IsSuccess);
            snapshots.Add(snapshot);
        }
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         new CheckpointIdentity("PopulationSnapshotDirectoryV2",
                             EventStreamPattern.ForPattern(tenantId.ToString(), "population-snapshots")),
                         ProjectionCheckpoint.Start)))
        {
            foreach (var snapshot in snapshots)
                await directory.ApplyAsync(Assert.Single(
                    new AggregateScenario<PopulationSnapshot>(snapshot).PendingEvents));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var handler = new GetWorkforceRosterSnapshotAsOfHandler(directory,
            new SnapshotsReader(snapshots), new FixedPermissions(false));

        // Act
        var between = await handler.HandleAsync(Context(
            new GetWorkforceRosterSnapshotAsOf(tenantId, Now.AddMinutes(15))), CancellationToken.None);
        var before = await handler.HandleAsync(Context(
            new GetWorkforceRosterSnapshotAsOf(tenantId, Now.AddMinutes(-1))), CancellationToken.None);
        var listed = await new ListWorkforceRosterSnapshotsHandler(directory).HandleAsync(
            Context(new ListWorkforceRosterSnapshots(tenantId)), CancellationToken.None);

        // Assert
        Assert.Equal(snapshots[1].Id, between.Value.SnapshotId);
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(before.Error).Kind);
        Assert.Equal(3, listed.Value.Items.Count);
    }

    static PersonView Person(Uuid tenantId, string name) =>
        new(tenantId, Uuid.CreateVersion4(), 1, name, $"{name.ToLowerInvariant()}@example.com",
            "manual", Author, Now);

    static WorkRelationshipView Relationship(Uuid tenantId, Uuid personId, string workerId,
        Uuid? managerId) =>
        new(tenantId, WorkRelationship.IdFor(tenantId, workerId), 1, personId, workerId, "employee",
            "active", new DateOnly(2025, 1, 6), null, "Engineering", managerId, null, false,
            "manual", Author, Now);

    static RequestContext<T> Context<T>(T request) where T : IRequestBase =>
        new(request, Actor(Uuid.CreateVersion4()));

    static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "BdgrzSession"));

    sealed class FixedPermissions(bool allowed) : IPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            string permission, CancellationToken ct = default) => ValueTask.FromResult(allowed);
    }

    sealed class GrantedPermissions(params string[] permissions) : IPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(permissions.Contains(permission, StringComparer.Ordinal));
    }

    sealed class SnapshotsReader(IReadOnlyList<PopulationSnapshot> snapshots) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult(snapshots.FirstOrDefault(snapshot => snapshot.Id == aggregate.Id)
                is TAggregate found ? found : aggregate);
    }

    sealed class Harness : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly FitzPersonDirectory _people = new(new InMemoryKvClient());
        readonly FitzWorkRelationshipDirectory _relationships = new(new InMemoryKvClient());
        readonly Uuid _userId = Uuid.CreateVersion4();
        WorkRelationshipView? _advanceDuringRead;

        Harness(ServiceProvider provider) => _provider = provider;

        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public string ActorDisplay => _userId.ToString();

        public static ValueTask<Harness> CreateAsync()
        {
            var services = new ServiceCollection();
            var store = new InMemoryEventStore();
            services.AddSingleton<IEventStore>(store);
            services.AddSingleton<IDomainEventReader>(store);
            services.AddSingleton(TimeProvider.System);
            services.AddPortia();
            return ValueTask.FromResult(new Harness(services.BuildServiceProvider()));
        }

        IAggregateReader Reader => _provider.GetRequiredService<IAggregateReader>();

        WorkforceRosterSnapshotter Snapshotter()
        {
            var events = _provider.GetRequiredService<IDomainEventReader>();
            IWorkRelationshipDirectoryReader relationships = _advanceDuringRead is { } advance
                ? new AdvancingRelationshipReader(this, advance)
                : _relationships;
            return new WorkforceRosterSnapshotter(_people,
                new PersonReadConsistency(_people, Reader, events), relationships,
                new WorkRelationshipReadConsistency(_relationships, Reader, events),
                new PopulationSnapshotFreezer(Reader,
                    new AggregateExecutor(Reader, _provider.GetRequiredService<IAggregateWriter>()),
                    TimeProvider.System));
        }

        public async Task ProjectAsync(IEnumerable<PersonView> people,
            IEnumerable<WorkRelationshipView> relationships)
        {
            await using (var batch = await _people.BeginAsync(new ProjectionBatchContext(
                             new CheckpointIdentity("PersonDirectoryV1",
                                 EventStreamPattern.ForPattern(TenantId.ToString(), "people")),
                             ProjectionCheckpoint.Start)))
            {
                foreach (var person in people)
                    await _people.ApplyAsync(new PersonRecorded(person.TenantId, person.PersonId,
                        person.DisplayName, person.WorkEmail, Author, Now));
                await batch.CommitAsync(ProjectionCheckpoint.Start);
            }
            await using (var batch = await _relationships.BeginAsync(new ProjectionBatchContext(
                             new CheckpointIdentity("WorkRelationshipDirectoryV1",
                                 EventStreamPattern.ForPattern(TenantId.ToString(), "work-relationships")),
                             ProjectionCheckpoint.Start)))
            {
                foreach (var job in relationships)
                {
                    var terms = new WorkRelationshipTerms(job.WorkerType, job.LifecycleStatus,
                        job.StartDate, job.EndDate, job.Department, job.ManagerPersonId,
                        job.SponsorPersonId);
                    await _relationships.ApplyAsync(job.Revision == 1
                        ? new WorkRelationshipRecorded(job.TenantId, job.RelationshipId, job.PersonId,
                            job.SourceWorkerId, terms, Author, Now)
                        : new WorkRelationshipRevised(job.TenantId, job.RelationshipId, job.Revision,
                            terms, Author, Now));
                }
                await batch.CommitAsync(ProjectionCheckpoint.Start);
            }
        }

        public void AdvanceDuringRead(WorkRelationshipView revision) =>
            _advanceDuringRead = revision;

        /// <summary>Projects a later revision under a new checkpoint, as a worker would mid-read.</summary>
        async Task ProjectRevisionAsync(WorkRelationshipView job)
        {
            await using var batch = await _relationships.BeginAsync(new ProjectionBatchContext(
                new CheckpointIdentity("WorkRelationshipDirectoryV1",
                    EventStreamPattern.ForPattern(TenantId.ToString(), "work-relationships")),
                ProjectionCheckpoint.Start));
            await _relationships.ApplyAsync(new WorkRelationshipRevised(job.TenantId,
                job.RelationshipId, job.Revision, new WorkRelationshipTerms(job.WorkerType,
                    job.LifecycleStatus, job.StartDate, job.EndDate, job.Department,
                    job.ManagerPersonId, job.SponsorPersonId), Author, Now));
            await batch.CommitAsync(new ProjectionCheckpoint(new EventCursor("advanced")));
        }

        public async Task<Uuid> RecordRelationshipAsync(Uuid personId, Uuid managerId)
        {
            var relationshipId = WorkRelationship.IdFor(TenantId, "E-500");
            var relationship = new WorkRelationship(TenantId, relationshipId);
            Assert.True(relationship.Record(personId, "E-500",
                new WorkRelationshipTerms("employee", "active", new DateOnly(2025, 1, 6), null, null,
                    managerId, null, "retirement"), Author, Now).IsSuccess);
            var events = new AggregateScenario<WorkRelationship>(relationship).PendingEvents.ToList();
            await _provider.GetRequiredService<IAggregateWriter>().SaveAsync(relationship,
                new RequestDispatchContext(RequestActor.System));
            await using var batch = await _relationships.BeginAsync(new ProjectionBatchContext(
                new CheckpointIdentity("WorkRelationshipDirectoryV1",
                    EventStreamPattern.ForPattern(TenantId.ToString(), "work-relationships")),
                ProjectionCheckpoint.Start));
            foreach (var domainEvent in events)
                await _relationships.ApplyAsync(domainEvent);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
            return relationshipId;
        }

        public ValueTask<Result<WorkRelationshipView>> GetRelationshipAsync(Uuid relationshipId,
            bool canReadManagerChain)
            => GetRelationshipAsync(relationshipId, new FixedPermissions(canReadManagerChain));

        public ValueTask<Result<WorkRelationshipView>> GetRelationshipAsync(Uuid relationshipId,
            IPermissionAuthorizer permissions)
        {
            var consistency = new WorkRelationshipReadConsistency(_relationships, Reader,
                _provider.GetRequiredService<IDomainEventReader>());
            return new GetWorkRelationshipHandler(consistency,
                    permissions)
                .HandleAsync(new RequestContext<GetWorkRelationship>(
                    new GetWorkRelationship(TenantId, relationshipId), Actor(_userId)),
                    CancellationToken.None);
        }

        sealed class AdvancingRelationshipReader(Harness harness, WorkRelationshipView revision)
            : IWorkRelationshipDirectoryReader
        {
            bool _advanced;

            public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
                CancellationToken ct = default) =>
                harness._relationships.LoadCheckpointAsync(tenantId, ct);

            public ValueTask<WorkRelationshipView?> GetAsync(Uuid tenantId, Uuid relationshipId,
                CancellationToken ct = default) =>
                harness._relationships.GetAsync(tenantId, relationshipId, ct);

            public async ValueTask<Page<WorkRelationshipView>> ListAsync(Uuid tenantId, int limit,
                string? cursor, CancellationToken ct = default)
            {
                if (!_advanced)
                {
                    _advanced = true;
                    await harness.ProjectRevisionAsync(revision);
                }
                return await harness._relationships.ListAsync(tenantId, limit, cursor, ct);
            }
        }

        public async Task RecordUnprojectedRelationshipAsync(Uuid personId)
        {
            var relationship = new WorkRelationship(TenantId, WorkRelationship.IdFor(TenantId, "E-900"));
            Assert.True(relationship.Record(personId, "E-900",
                new WorkRelationshipTerms("employee", "active", new DateOnly(2025, 1, 6), null, null,
                    null, null), Author, Now).IsSuccess);
            await _provider.GetRequiredService<IAggregateWriter>().SaveAsync(relationship,
                new RequestDispatchContext(RequestActor.System));
        }

        public async Task<(Result<SnapshotRegistration> Result, Uuid RequestId)> TryFreezeCoreAsync()
        {
            var context = new RequestContext<FreezeWorkforceRosterSnapshot>(
                new FreezeWorkforceRosterSnapshot(TenantId), Actor(_userId));
            var result = await new FreezeWorkforceRosterSnapshotHandler(Snapshotter())
                .HandleAsync(context, CancellationToken.None);
            return (result, context.RequestId);
        }

        public async Task<FreezeAttempt> TryFreezeAsync()
        {
            var (result, requestId) = await TryFreezeCoreAsync();
            return new FreezeAttempt(result.IsSuccess ? null : result.Error, requestId);
        }

        public async Task<SnapshotRegistration> FreezeAsync()
        {
            var (result, _) = await TryFreezeCoreAsync();
            Assert.True(result.IsSuccess, result.IsSuccess ? null : result.Error?.Message);
            return result.Value;
        }

        public async Task<SnapshotRegistration> AmendAsync(Uuid snapshotId, string reason)
        {
            var result = await new AmendWorkforceRosterSnapshotHandler(Snapshotter()).HandleAsync(
                new RequestContext<AmendWorkforceRosterSnapshot>(
                    new AmendWorkforceRosterSnapshot(TenantId, snapshotId, reason), Actor(_userId)),
                CancellationToken.None);
            Assert.True(result.IsSuccess, result.IsSuccess ? null : result.Error?.Message);
            return result.Value;
        }

        public ValueTask<Result<WorkforceRosterSnapshotView>> TryGetAsync(Uuid tenantId,
            Uuid snapshotId, bool canReadManagerChain = false) =>
            new GetWorkforceRosterSnapshotHandler(Reader, new FixedPermissions(canReadManagerChain))
                .HandleAsync(new RequestContext<GetWorkforceRosterSnapshot>(
                    new GetWorkforceRosterSnapshot(tenantId, snapshotId), Actor(_userId)),
                    CancellationToken.None);

        public async Task<WorkforceRosterSnapshotView> GetAsync(Uuid snapshotId,
            bool canReadManagerChain)
        {
            var result = await TryGetAsync(TenantId, snapshotId, canReadManagerChain);
            Assert.True(result.IsSuccess, result.IsSuccess ? null : result.Error?.Message);
            return result.Value;
        }

        public ValueTask<PopulationSnapshot> HydrateAsync(Uuid snapshotId) =>
            Reader.HydrateAsync(new PopulationSnapshot(TenantId, snapshotId));

        public ValueTask DisposeAsync() => _provider.DisposeAsync();
    }

    sealed record FreezeAttempt(RequestError? Error, Uuid RequestId);
}
