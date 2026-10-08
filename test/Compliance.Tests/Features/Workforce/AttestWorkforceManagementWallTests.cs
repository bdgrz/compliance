using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Snapshots;
using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Workforce;

public sealed class AttestWorkforceManagementWallTests
{
    [Theory]
    [InlineData("person_record")]
    [InlineData("person_revise")]
    [InlineData("person_correlate")]
    [InlineData("relationship_record")]
    [InlineData("relationship_revise")]
    [InlineData("identity_record")]
    [InlineData("identity_revise")]
    [InlineData("source_record")]
    [InlineData("source_reconcile")]
    [InlineData("observation_resolve")]
    [InlineData("roster_freeze")]
    [InlineData("roster_amend")]
    public async Task ShouldDenyManualMutationGivenActualAttestHistoryDespiteCurrentWorkforceGrant(string operation)
    {
        // Arrange
        await using var ordinary = await Fixture.CreateAsync();
        var allowed = await ordinary.ExecuteAsync(operation);
        Assert.True(allowed.IsSuccess, allowed.Error?.Message);
        await using var attest = await Fixture.CreateAsync();
        await AttestAssignmentHistoryFixture.SeedAsync(attest.Provider, attest.TenantId, attest.UserId);
        var before = await attest.ManagementEventCountAsync();

        // Act
        var denied = await attest.ExecuteAsync(operation);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, denied.Error?.Kind);
        Assert.Contains("Attest", denied.Error!.Message, StringComparison.Ordinal);
        Assert.Equal(before, await attest.ManagementEventCountAsync());
    }

    [Theory]
    [InlineData("person_record")]
    [InlineData("person_revise")]
    [InlineData("person_correlate")]
    [InlineData("relationship_record")]
    [InlineData("relationship_revise")]
    [InlineData("identity_record")]
    [InlineData("identity_revise")]
    [InlineData("source_record")]
    [InlineData("source_reconcile")]
    [InlineData("observation_resolve")]
    [InlineData("roster_freeze")]
    [InlineData("roster_amend")]
    public async Task ShouldPreserveManualMutationGivenActualAdvisoryHistoryAndOrdinaryGrant(string operation)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        await AttestAssignmentHistoryFixture.SeedAsync(fixture.Provider, fixture.TenantId, fixture.UserId,
            practice: "advisory");

        // Act
        var result = await fixture.ExecuteAsync(operation);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    [Theory]
    [InlineData("client_personnel")]
    [InlineData("guest")]
    public async Task ShouldDenyMutationGivenClosedAttestHistoryAndCurrentNonFirmAffiliation(string affiliation)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(affiliation);
        await AttestAssignmentHistoryFixture.SeedAsync(fixture.Provider, fixture.TenantId, fixture.UserId, revoked: true);
        var before = await fixture.ManagementEventCountAsync();

        // Act
        var denied = await fixture.ExecuteAsync("person_record");

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, denied.Error?.Kind);
        Assert.Equal(before, await fixture.ManagementEventCountAsync());
    }

    [Fact]
    public async Task ShouldPreserveMutationGivenOnlyAnotherClientsActualAttestHistory()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        await AttestAssignmentHistoryFixture.SeedAsync(fixture.Provider, Uuid.CreateVersion4(), fixture.UserId, revoked: true);

        // Act
        var result = await fixture.ExecuteAsync("person_record");

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldPreserveReadsAndManifestRecomputationGivenActualAttestHistory(bool revoked)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        await AttestAssignmentHistoryFixture.SeedAsync(fixture.Provider, fixture.TenantId, fixture.UserId, revoked);
        var before = await fixture.ManagementEventCountAsync();

        // Act
        var person = await fixture.BusAsync(new GetPerson(fixture.TenantId, fixture.PersonId));
        var relationship = await fixture.BusAsync(new GetWorkRelationship(fixture.TenantId, fixture.RelationshipId));
        var identity = await fixture.BusAsync(new GetServiceIdentity(fixture.TenantId, fixture.IdentityId));
        var snapshot = await fixture.BusAsync(new GetWorkforceRosterSnapshot(fixture.TenantId, fixture.SnapshotId));
        var regenerated = await fixture.BusAsync(new RegenerateWorkforceRosterSnapshotManifest(fixture.TenantId, fixture.SnapshotId));

        // Assert
        Assert.True(person.IsSuccess, person.Error?.Message);
        Assert.True(relationship.IsSuccess, relationship.Error?.Message);
        Assert.True(identity.IsSuccess, identity.Error?.Message);
        Assert.True(snapshot.IsSuccess, snapshot.Error?.Message);
        Assert.True(regenerated.IsSuccess, regenerated.Error?.Message);
        Assert.Equal(snapshot.Value.ContentSha256, regenerated.Value.ContentSha256);
        Assert.Equal(before, await fixture.ManagementEventCountAsync());
    }

    [Fact]
    public async Task ShouldPreserveNativeSourceWriteAndProjectionGivenActualAttestHistory()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        await AttestAssignmentHistoryFixture.SeedAsync(fixture.Provider, fixture.TenantId, fixture.UserId);
        var personId = Uuid.CreateVersion4();
        var source = ActorReference.ForSystemProcess("synthetic-roster-source", "Synthetic authorized source");

        // Act
        await ProgramManagementServices.SeedAsync(fixture.Provider, new Person(fixture.TenantId, personId), person =>
            person.Record("Source worker", null, source, DateTimeOffset.UtcNow));
        await fixture.CatchUpAsync();
        var read = await fixture.BusAsync(new GetPerson(fixture.TenantId, personId));

        // Assert
        Assert.True(read.IsSuccess, read.Error?.Message);
        Assert.Equal("Source worker", read.Value.DisplayName);
    }

    [Fact]
    public async Task ShouldRetainOrdinaryPermissionDenialGivenNoAttestHistory()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        fixture.Permissions.Allowed = false;
        var before = await fixture.ManagementEventCountAsync();

        // Act
        var denied = await fixture.ExecuteAsync("person_record");

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, denied.Error?.Kind);
        Assert.DoesNotContain("Attest", denied.Error!.Message, StringComparison.Ordinal);
        Assert.Equal(before, await fixture.ManagementEventCountAsync());
    }

    sealed class Fixture : IAsyncDisposable
    {
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid UserId { get; } = Uuid.CreateVersion4();
        public Uuid PersonId { get; } = Uuid.CreateVersion4();
        public Uuid IdentityId { get; } = Uuid.CreateVersion4();
        public Uuid RelationshipId => WorkRelationship.IdFor(TenantId, "E-100");
        public Uuid SourceId => WorkforceSourceObservation.IdFor(TenantId, Source);
        public WorkforceSourceIdentity Source { get; } = new("hris", "manual", "E-100", "v1");
        public WorkforceSourceFacts Facts { get; } = new(Person: new WorkforcePersonSourceFacts("Ada", "ada@example.com"));
        public ServiceProvider Provider { get; private set; } = null!;
        public Permissions Permissions { get; } = new();
        Uuid _snapshotId;
        public Uuid SnapshotId => _snapshotId;
        Uuid _observationId;
        ActorReference Author => ActorReference.ForMember(RbacIds.Member(TenantId, UserId), "Client author");
        static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

        public static async Task<Fixture> CreateAsync(string affiliation = "client_personnel")
        {
            var fixture = new Fixture();
            var services = new ServiceCollection();
            _ = services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                ["Fitz:ApplicationName"] = "compliance"
            }).Build(), developerAuthentication: true);
            var store = new InMemoryEventStore();
            services.AddSingleton<IEventStore>(store);
            services.AddSingleton<IDomainEventReader>(store);
            services.AddSingleton<IKvClient>(new InMemoryKvClient());
            services.AddSingleton<IPermissionAuthorizer>(fixture.Permissions);
            services.AddSingleton<ITenantActivity, ActiveTenant>();
            services.AddSingleton<ITenantMembershipDirectoryReader>(new Memberships(fixture.TenantId, fixture.UserId, affiliation));
            fixture.Provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            await fixture.SeedAsync();
            return fixture;
        }

        async Task SeedAsync()
        {
            var now = DateTimeOffset.UtcNow.AddDays(-1);
            await ProgramManagementServices.SeedAsync(Provider, new Person(TenantId, PersonId), person =>
                person.Record("Ada", "ada@example.com", Author, now));
            await ProgramManagementServices.SeedAsync(Provider, new WorkRelationship(TenantId, RelationshipId), job =>
                job.Record(PersonId, "E-100", new WorkRelationshipTerms("employee", "active", Today.AddYears(-1),
                    null, "Engineering", null, null), Author, now));
            await ProgramManagementServices.SeedAsync(Provider, new ServiceIdentity(TenantId, IdentityId), identity =>
                identity.Record(new ServiceIdentityTerms("Deploy bot", "bot", "Approved deployment purpose", "production",
                    "active", "person", PersonId, Today.AddDays(90), null), Today, Author, now));
            await ProgramManagementServices.SeedAsync(Provider, new WorkforceSourceObservation(TenantId, SourceId), source =>
                source.Record(Source, "person", PersonId, 1, Facts, now, Author, now));
            await CatchUpAsync();
            await using var scope = Provider.CreateAsyncScope();
            var observations = await scope.ServiceProvider.GetRequiredService<IWorkforceObservationDirectoryReader>()
                .ListAsync(TenantId, null, 100, null);
            _observationId = Assert.Single(observations.Items).ObservationId;
            var frozen = await BusAsync(new FreezeWorkforceRosterSnapshot(TenantId));
            Assert.True(frozen.IsSuccess, frozen.Error?.Message);
            _snapshotId = frozen.Value.SnapshotId;
        }

        public async Task<Result<T>> BusAsync<T>(IRequest<T> request)
        {
            await using var scope = Provider.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(request,
                ProgramManagementServices.Actor(UserId));
        }

        async Task<Result> BusAsync(IRequest request)
        {
            await using var scope = Provider.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(request,
                ProgramManagementServices.Actor(UserId));
        }

        async Task<Result> SendAsync<T>(IRequest<T> request)
        {
            var result = await BusAsync(request);
            return result.IsSuccess ? Result.Success : Result.Failure(result.Error!);
        }

        public Task<Result> ExecuteAsync(string operation) => operation switch
        {
            "person_record" => SendAsync(new RecordPerson(TenantId, "Grace", "grace@example.com")),
            "person_revise" => BusAsync(new RevisePerson(TenantId, PersonId, 1, "Ada King", "ada@example.com")),
            "person_correlate" => BusAsync(new CorrelatePersonMembership(TenantId, PersonId, 1, UserId)),
            "relationship_record" => SendAsync(new RecordWorkRelationship(TenantId, PersonId, "E-200", "employee",
                "active", Today.AddYears(-1), Department: "Engineering")),
            "relationship_revise" => BusAsync(new ReviseWorkRelationship(TenantId, RelationshipId, 1, "employee",
                "active", Today.AddYears(-1), Department: "Finance")),
            "identity_record" => SendAsync(new RecordServiceIdentity(TenantId, "CI bot", "bot", "Approved CI purpose",
                "person", PersonId, Today.AddDays(90))),
            "identity_revise" => BusAsync(new ReviseServiceIdentity(TenantId, IdentityId, 1, "Deploy bot", "bot",
                "Approved deployment purpose", "person", PersonId, Today.AddDays(100), "active", "production")),
            "source_record" => SendAsync(new RecordWorkforceSourceObservation(TenantId, Source with { SourceRevision = "v2" },
                "person", PersonId, 1, Facts, DateTimeOffset.UtcNow.AddHours(-1))),
            "source_reconcile" => BusAsync(new ReconcileWorkforceSourceObservation(TenantId, SourceId, 1, 1,
                "accepted", "Checked retained source facts")),
            "observation_resolve" => BusAsync(new ResolveWorkforceObservation(TenantId, _observationId, "resolved",
                "Reviewed joiner observation")),
            "roster_freeze" => SendAsync(new FreezeWorkforceRosterSnapshot(TenantId)),
            "roster_amend" => SendAsync(new AmendWorkforceRosterSnapshot(TenantId, _snapshotId, "Reviewed roster amendment")),
            _ => throw new InvalidOperationException(operation)
        };

        public async Task<int> ManagementEventCountAsync()
        {
            var count = 0;
            await foreach (var record in Provider.GetRequiredService<IDomainEventReader>().ReadAsync(
                EventStreamPattern.ForPattern(TenantId.ToString()), EventCursor.Start, CancellationToken.None))
                if (record.Event is PersonRecorded or PersonRevised or PersonMembershipCorrelated or
                    WorkRelationshipRecorded or WorkRelationshipRevised or ServiceIdentityRecorded or ServiceIdentityRevised or
                    WorkforceSourceObserved or WorkforceSourceReconciled or WorkforceObservationResolved or PopulationSnapshotFrozen)
                    count++;
            return count;
        }

        public async Task CatchUpAsync()
        {
            await using var scope = Provider.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var people = services.GetRequiredService<FitzPersonDirectory>();
            var jobs = services.GetRequiredService<FitzWorkRelationshipDirectory>();
            var identities = services.GetRequiredService<FitzServiceIdentityDirectory>();
            var sources = services.GetRequiredService<FitzWorkforceSourceDirectory>();
            var observations = services.GetRequiredService<FitzWorkforceObservationDirectory>();
            await ProjectAsync("PersonDirectoryV2", "people", people, people.ApplyAsync, people.LoadCheckpointAsync);
            await ProjectAsync("WorkRelationshipDirectoryV1", "work-relationships", jobs, jobs.ApplyAsync, jobs.LoadCheckpointAsync);
            await ProjectAsync("ServiceIdentityDirectoryV1", "service-identities", identities, identities.ApplyAsync, identities.LoadCheckpointAsync);
            await ProjectAsync("WorkforceSourcesV1", "workforce-source-observations", sources, sources.ApplyAsync, sources.LoadCheckpointAsync);
            await ProjectAsync("WorkforceObservationsV1", "work-relationships", observations, observations.ApplyAsync, observations.LoadCheckpointAsync);

            async Task ProjectAsync(string name, string area, IProjectionStore projection,
                Func<DomainEvent, CancellationToken, ValueTask> apply,
                Func<Uuid, CancellationToken, ValueTask<ProjectionCheckpoint>> load)
            {
                var checkpoint = await load(TenantId, CancellationToken.None);
                var pattern = EventStreamPattern.ForPattern(TenantId.ToString(), area);
                await using var batch = await projection.BeginAsync(new ProjectionBatchContext(new CheckpointIdentity(name, pattern), checkpoint));
                var cursor = checkpoint.Cursor;
                await foreach (var record in services.GetRequiredService<IDomainEventReader>().ReadAsync(pattern, cursor, CancellationToken.None))
                {
                    await apply(record.Event, CancellationToken.None);
                    cursor = record.NextCursor;
                }
                await batch.CommitAsync(new ProjectionCheckpoint(cursor));
            }
        }

        public ValueTask DisposeAsync() => Provider.DisposeAsync();
    }

    sealed class Permissions : IPermissionAuthorizer
    {
        public bool Allowed { get; set; } = true;

        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission,
            CancellationToken ct = default) => ValueTask.FromResult(Allowed);
    }

    sealed class Memberships(Uuid tenant, Uuid user, string affiliation) : ITenantMembershipDirectoryReader
    {
        public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult<TenantMembershipView?>(tenantId == tenant.ToString() && userId == user
                ? new TenantMembershipView(user, tenant, affiliation, false) : null);

        public ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(tenantId == tenant.ToString() && userId == user);

        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }
}
