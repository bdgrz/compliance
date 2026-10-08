using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Workforce;

public sealed class WorkforceSourceHandlerTests
{
    [Fact]
    public async Task ShouldRequireExplicitCanonicalCorrectionBeforeAcceptedSourceGivenConflictingPersonFacts()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var source = new WorkforceSourceIdentity("hris", "system", "100", "v1");
        var facts = new WorkforceSourceFacts(Person: new WorkforcePersonSourceFacts("Ada King", "ada@example.com"));
        var request = new RecordWorkforceSourceObservation(fixture.TenantId, source, "person",
            fixture.PersonId, 1, facts, DateTimeOffset.UtcNow.AddDays(-1));
        var registration = (await fixture.As().When(request).ExpectSuccess()).Value;

        // Act
        var lagged = await fixture.As().When(new GetWorkforceSourceObservation(fixture.TenantId,
            registration.ObservationId)).ExpectFailure(RequestErrorKind.Conflict);
        await fixture.ProjectSourcesAsync();
        var preview = (await fixture.As().When(new PreviewWorkforceSourceObservation(fixture.TenantId,
            registration.ObservationId)).ExpectSuccess()).Value;
        _ = await PersonalWorkforceObservationTransportTests.HttpAsync(fixture.Provider, fixture.UserId, new ReconcileWorkforceSourceObservation(fixture.TenantId,
            registration.ObservationId, 1, 1, "accepted", "HRIS checked"), RequestErrorKind.Conflict);
        await ProgramManagementServices.SeedAsync(fixture.Provider, new Person(fixture.TenantId, fixture.PersonId),
            person => person.Revise(1, "Ada King", "ada@example.com", fixture.Author, DateTimeOffset.UtcNow) is null
                ? Result.Success : Result.Failure(new RequestError(RequestErrorKind.Conflict, "revise")));
        await fixture.ProjectPeopleAsync();
        _ = await PersonalWorkforceObservationTransportTests.HttpAsync(fixture.Provider, fixture.UserId, new ReconcileWorkforceSourceObservation(fixture.TenantId,
            registration.ObservationId, 1, 1, "accepted", "HRIS checked"), RequestErrorKind.Conflict);
        _ = await PersonalWorkforceObservationTransportTests.HttpAsync(fixture.Provider, fixture.UserId, new ReconcileWorkforceSourceObservation(fixture.TenantId,
            registration.ObservationId, 1, 2, "accepted", "HRIS checked"));
        await fixture.ProjectSourcesAsync();
        var accepted = (await fixture.As().When(new GetWorkforceSourceObservation(fixture.TenantId,
            registration.ObservationId)).ExpectSuccess()).Value;
        var canonical = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new Person(fixture.TenantId, fixture.PersonId));

        // Assert
        Assert.True(Assert.IsType<RequestError>(lagged.Error).IsTransient);
        Assert.Equal(["display_name"], preview.ConflictingFields);
        Assert.False(preview.CanAccept);
        Assert.Equal("accepted", accepted.Decision!.Outcome);
        Assert.Equal(source, accepted.Source);
        Assert.Equal(2, accepted.Decision.TargetRevision);
        Assert.True(accepted.AcceptedForCurrentRevision);
        Assert.Equal(2, canonical.Revision);
        Assert.Equal("manual", (await fixture.People.GetAsync(fixture.TenantId, fixture.PersonId))!.SourceKind);
    }

    [Fact]
    public async Task ShouldDenyUnauthorizedAndCrossTenantReadsGivenRecordedSourceObservation()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var registered = (await fixture.As().When(new RecordWorkforceSourceObservation(fixture.TenantId,
            new WorkforceSourceIdentity("idp", "system", "100", "v1"), "person", fixture.PersonId,
            1, new WorkforceSourceFacts(Person: new WorkforcePersonSourceFacts("Ada", "ada@example.com")),
            DateTimeOffset.UtcNow.AddDays(-1))).ExpectSuccess()).Value;
        await fixture.ProjectSourcesAsync();

        // Act
        var denied = await fixture.As(Uuid.CreateVersion4()).When(new GetWorkforceSourceObservation(
            fixture.TenantId, registered.ObservationId)).ExpectFailure(RequestErrorKind.Forbidden);
        var alien = await fixture.As().When(new GetWorkforceSourceObservation(Uuid.CreateVersion4(),
            registered.ObservationId)).ExpectFailure(RequestErrorKind.NotFound);

        // Assert
        Assert.NotNull(denied.Error);
        Assert.NotNull(alien.Error);
    }

    [Fact]
    public async Task ShouldAlwaysRedactListsAndRequireDetailFieldGrantsGivenRestrictedRelationshipSourceFacts()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var managerId = Uuid.CreateVersion4();
        var jobId = WorkRelationship.IdFor(fixture.TenantId, "E-100");
        var terms = new WorkRelationshipTerms("employee", "active", new DateOnly(2025, 1, 1),
            null, "Engineering", managerId, null, "private reason");
        await ProgramManagementServices.SeedAsync(fixture.Provider, new WorkRelationship(fixture.TenantId,
            jobId), relationship => relationship.Record(fixture.PersonId, "E-100", terms,
            fixture.Author, DateTimeOffset.UtcNow));
        await fixture.ProjectRelationshipsAsync();
        var registered = (await fixture.As().When(new RecordWorkforceSourceObservation(fixture.TenantId,
            new WorkforceSourceIdentity("hris", "system", "100", "v1"), "work_relationship", jobId,
            1, new WorkforceSourceFacts(WorkRelationship: terms), DateTimeOffset.UtcNow.AddDays(-1)))
            .ExpectSuccess()).Value;
        await fixture.ProjectSourcesAsync();

        // Act
        var granted = (await fixture.As().When(new GetWorkforceSourceObservation(fixture.TenantId,
            registered.ObservationId)).ExpectSuccess()).Value;
        var listed = (await fixture.As().When(new ListWorkforceSourceObservations(fixture.TenantId,
            "work_relationship", jobId)).ExpectSuccess()).Value;
        fixture.Permissions.AllowRestricted = false;
        var redacted = (await fixture.As().When(new GetWorkforceSourceObservation(fixture.TenantId,
            registered.ObservationId)).ExpectSuccess()).Value;

        // Assert
        Assert.Equal(managerId, granted.Facts.WorkRelationship!.ManagerPersonId);
        Assert.Equal("private reason", granted.Facts.WorkRelationship.EmploymentStatusReason);
        Assert.False(granted.RestrictedFieldsRedacted);
        foreach (var view in new[] { Assert.Single(listed.Items), redacted })
        {
            Assert.True(view.RestrictedFieldsRedacted);
            Assert.Null(view.Facts.WorkRelationship!.ManagerPersonId);
            Assert.Null(view.Facts.WorkRelationship.EmploymentStatusReason);
        }
    }

    [Fact]
    public async Task ShouldAcceptProviderProvenanceWithoutChangingGovernedOwnerAndPurposeGivenMatchingServiceFacts()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var identityId = Uuid.CreateVersion4();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var terms = new ServiceIdentityTerms("Deploy bot", "bot", "Approved deploy purpose", "production",
            "active", "person", fixture.PersonId, today.AddDays(90), today.AddDays(100));
        await ProgramManagementServices.SeedAsync(fixture.Provider, new ServiceIdentity(fixture.TenantId,
            identityId), identity => identity.Record(terms, today, fixture.Author, DateTimeOffset.UtcNow));
        await fixture.ProjectIdentitiesAsync();
        var facts = new WorkforceSourceFacts(ServiceIdentity: new WorkforceServiceSourceFacts(
            terms.DisplayName, terms.IdentityKind, terms.Environment, terms.LifecycleStatus, terms.ExpiresOn));
        var registered = (await fixture.As().When(new RecordWorkforceSourceObservation(fixture.TenantId,
            new WorkforceSourceIdentity("provider", "github", "bot-100", "v1"), "service_identity",
            identityId, 1, facts, DateTimeOffset.UtcNow.AddDays(-1))).ExpectSuccess()).Value;

        // Act
        var preview = (await fixture.As().When(new PreviewWorkforceSourceObservation(fixture.TenantId,
            registered.ObservationId)).ExpectSuccess()).Value;
        _ = await PersonalWorkforceObservationTransportTests.HttpAsync(fixture.Provider, fixture.UserId, new ReconcileWorkforceSourceObservation(fixture.TenantId,
            registered.ObservationId, 1, 1, "accepted", "Provider facts checked"));
        await fixture.ProjectSourcesAsync();
        var source = (await fixture.As().When(new GetWorkforceSourceObservation(fixture.TenantId,
            registered.ObservationId)).ExpectSuccess()).Value;
        var current = await fixture.Identities.GetAsync(fixture.TenantId, identityId);

        // Assert
        Assert.Equal("corroborating", preview.SourceAuthority);
        Assert.True(preview.CanAccept);
        Assert.True(source.AcceptedForCurrentRevision);
        Assert.Equal(fixture.PersonId, current!.OwnerId);
        Assert.Equal("Approved deploy purpose", current.Purpose);
        Assert.Equal(1, current.Revision);
        Assert.Equal(terms.ExpiresOn, source.Facts.ServiceIdentity!.ExpiresOn);
    }

    [Fact]
    public async Task ShouldKeepOriginalAttributionGivenHydratedRetriesAfterCanonicalRevisionAdvances()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var request = new RecordWorkforceSourceObservation(fixture.TenantId,
            new WorkforceSourceIdentity("hris", "system", "100", "v1"), "person", fixture.PersonId,
            1, new WorkforceSourceFacts(Person: new WorkforcePersonSourceFacts("Ada", "ada@example.com")),
            DateTimeOffset.UtcNow.AddDays(-1));
        var registration = (await fixture.As().When(request).ExpectSuccess()).Value;
        var decision = new ReconcileWorkforceSourceObservation(fixture.TenantId,
            registration.ObservationId, 1, 1, "accepted", "Source checked");
        _ = await PersonalWorkforceObservationTransportTests.HttpAsync(fixture.Provider, fixture.UserId, decision);
        var original = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new WorkforceSourceObservation(fixture.TenantId, registration.ObservationId));
        await ProgramManagementServices.SeedAsync(fixture.Provider, new Person(fixture.TenantId,
            fixture.PersonId), person => person.Revise(1, "Ada King", "ada@example.com", fixture.Author,
                DateTimeOffset.UtcNow) is null ? Result.Success : Result.Failure(new RequestError(RequestErrorKind.Conflict, "revise")));
        await fixture.ProjectPeopleAsync();
        var retryingUser = Uuid.CreateVersion4();
        fixture.Permissions.OtherUserId = retryingUser;

        // Act
        var retry = (await fixture.As(retryingUser).When(request).ExpectSuccess()).Value;
        _ = await PersonalWorkforceObservationTransportTests.HttpAsync(fixture.Provider, retryingUser, decision);
        var hydrated = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new WorkforceSourceObservation(fixture.TenantId, registration.ObservationId));
        await fixture.ProjectSourcesAsync();
        var current = (await fixture.As().When(new GetWorkforceSourceObservation(fixture.TenantId,
            registration.ObservationId)).ExpectSuccess()).Value;

        // Assert
        Assert.Equal(registration, retry);
        Assert.Equal(2, hydrated.Revision);
        Assert.Equal(original.Observation!.Actor, hydrated.Observation!.Actor);
        Assert.Equal(original.Observation.RecordedAt, hydrated.Observation.RecordedAt);
        Assert.Equal(original.Decision, hydrated.Decision);
        Assert.False(current.AcceptedForCurrentRevision);
        Assert.Equal(2, current.CurrentTargetRevision);
        Assert.Equal(1, current.Decision!.TargetRevision);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task ShouldApplyIndependentFieldGrantsGivenRestrictedRelationshipSource(
        bool canReadManager, bool canReadReason)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var managerId = Uuid.CreateVersion4();
        var relationshipId = WorkRelationship.IdFor(fixture.TenantId, "E-200");
        var terms = new WorkRelationshipTerms("employee", "active", new DateOnly(2025, 1, 1),
            null, null, managerId, null, "private reason");
        await ProgramManagementServices.SeedAsync(fixture.Provider, new WorkRelationship(fixture.TenantId,
            relationshipId), relationship => relationship.Record(fixture.PersonId, "E-200", terms,
            fixture.Author, DateTimeOffset.UtcNow));
        await fixture.ProjectRelationshipsAsync();
        var registration = (await fixture.As().When(new RecordWorkforceSourceObservation(fixture.TenantId,
            new WorkforceSourceIdentity("hris", "system", "200", "v1"), "work_relationship",
            relationshipId, 1, new WorkforceSourceFacts(WorkRelationship: terms), DateTimeOffset.UtcNow.AddDays(-1)))
            .ExpectSuccess()).Value;
        await fixture.ProjectSourcesAsync();
        fixture.Permissions.SetRestrictedPermissions(canReadManager
            ? FieldClasses.WorkforceManagerChain.ReadPermission : FieldClasses.WorkforcePersonalDetails.ReadPermission);

        // Act
        var detail = (await fixture.As().When(new GetWorkforceSourceObservation(fixture.TenantId,
            registration.ObservationId)).ExpectSuccess()).Value;
        var list = (await fixture.As().When(new ListWorkforceSourceObservations(fixture.TenantId,
            "work_relationship", relationshipId)).ExpectSuccess()).Value;

        // Assert
        Assert.Equal(canReadManager ? managerId : (Uuid?)null, detail.Facts.WorkRelationship!.ManagerPersonId);
        Assert.Equal(canReadReason ? "private reason" : null, detail.Facts.WorkRelationship.EmploymentStatusReason);
        Assert.True(detail.RestrictedFieldsRedacted);
        var listed = Assert.Single(list.Items);
        Assert.True(listed.RestrictedFieldsRedacted);
        Assert.Null(listed.Facts.WorkRelationship!.ManagerPersonId);
        Assert.Null(listed.Facts.WorkRelationship.EmploymentStatusReason);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldDenyRecordRetryBeforePrivateEqualityGivenEitherRestrictedGrantMissing(bool managerOnly)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var relationshipId = WorkRelationship.IdFor(fixture.TenantId, "E-300");
        var terms = new WorkRelationshipTerms("employee", "active", new DateOnly(2025, 1, 1),
            null, null, Uuid.CreateVersion4(), null, "private reason");
        await ProgramManagementServices.SeedAsync(fixture.Provider, new WorkRelationship(fixture.TenantId,
            relationshipId), relationship => relationship.Record(fixture.PersonId, "E-300", terms,
            fixture.Author, DateTimeOffset.UtcNow));
        await fixture.ProjectRelationshipsAsync();
        var request = new RecordWorkforceSourceObservation(fixture.TenantId,
            new WorkforceSourceIdentity("hris", "system", "300", "v1"), "work_relationship",
            relationshipId, 1, new WorkforceSourceFacts(WorkRelationship: terms), DateTimeOffset.UtcNow.AddDays(-1));
        _ = await fixture.As().When(request).ExpectSuccess();
        fixture.Permissions.SetRestrictedPermissions(managerOnly
            ? FieldClasses.WorkforceManagerChain.ReadPermission : FieldClasses.WorkforcePersonalDetails.ReadPermission);

        // Act
        var matching = await fixture.As().When(request).ExpectFailure(RequestErrorKind.Forbidden);
        var guessed = await fixture.As().When(request with
        {
            Facts = new WorkforceSourceFacts(WorkRelationship: terms with { EmploymentStatusReason = "guess" }),
        }).ExpectFailure(RequestErrorKind.Forbidden);
        var disguised = await fixture.As().When(request with
        {
            TargetKind = "person",
            TargetId = fixture.PersonId,
            Facts = new WorkforceSourceFacts(Person: new WorkforcePersonSourceFacts("Ada", "ada@example.com")),
        }).ExpectFailure(RequestErrorKind.Forbidden);
        var absent = await fixture.As().When(request with
        {
            Source = request.Source with { SourceRevision = "v2" },
            Facts = new WorkforceSourceFacts(WorkRelationship: terms with
            {
                ManagerPersonId = null,
                EmploymentStatusReason = null,
            }),
        }).ExpectFailure(RequestErrorKind.Forbidden);

        // Assert
        Assert.Equal(matching.Error!.Message, guessed.Error!.Message);
        Assert.Equal(matching.Error.Message, disguised.Error!.Message);
        Assert.Equal(matching.Error.Message, absent.Error!.Message);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task ShouldDenyPreviewBeforePrivateComparisonGivenEitherRestrictedGrantMissing(
        bool managerOnly, bool privateValues)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var relationshipId = WorkRelationship.IdFor(fixture.TenantId, "E-301");
        var terms = new WorkRelationshipTerms("employee", "active", new DateOnly(2025, 1, 1),
            null, null, Uuid.CreateVersion4(), null, "private reason");
        await ProgramManagementServices.SeedAsync(fixture.Provider, new WorkRelationship(fixture.TenantId,
            relationshipId), relationship => relationship.Record(fixture.PersonId, "E-301", terms,
            fixture.Author, DateTimeOffset.UtcNow));
        await fixture.ProjectRelationshipsAsync();
        var facts = privateValues ? terms : terms with { ManagerPersonId = null, EmploymentStatusReason = null };
        var registration = (await fixture.As().When(new RecordWorkforceSourceObservation(fixture.TenantId,
            new WorkforceSourceIdentity("hris", "system", "301", "v1"), "work_relationship",
            relationshipId, 1, new WorkforceSourceFacts(WorkRelationship: facts), DateTimeOffset.UtcNow.AddDays(-1)))
            .ExpectSuccess()).Value;
        fixture.Permissions.SetRestrictedPermissions(managerOnly
            ? FieldClasses.WorkforceManagerChain.ReadPermission : FieldClasses.WorkforcePersonalDetails.ReadPermission);

        // Act
        var result = await fixture.As().When(new PreviewWorkforceSourceObservation(fixture.TenantId,
            registration.ObservationId)).ExpectFailure(RequestErrorKind.Forbidden);

        // Assert
        Assert.Contains("both restricted field read grants", result.Error!.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task ShouldDenyDecisionAndRetryBeforePrivateComparisonGivenEitherRestrictedGrantMissing(
        bool managerOnly, bool privateValues)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var relationshipId = WorkRelationship.IdFor(fixture.TenantId, "E-302");
        var terms = new WorkRelationshipTerms("employee", "active", new DateOnly(2025, 1, 1),
            null, null, Uuid.CreateVersion4(), null, "private reason");
        await ProgramManagementServices.SeedAsync(fixture.Provider, new WorkRelationship(fixture.TenantId,
            relationshipId), relationship => relationship.Record(fixture.PersonId, "E-302", terms,
            fixture.Author, DateTimeOffset.UtcNow));
        await fixture.ProjectRelationshipsAsync();
        var facts = privateValues ? terms : terms with { ManagerPersonId = null, EmploymentStatusReason = null };
        var registration = (await fixture.As().When(new RecordWorkforceSourceObservation(fixture.TenantId,
            new WorkforceSourceIdentity("hris", "system", "302", "v1"), "work_relationship",
            relationshipId, 1, new WorkforceSourceFacts(WorkRelationship: facts), DateTimeOffset.UtcNow.AddDays(-1)))
            .ExpectSuccess()).Value;
        var accepted = new ReconcileWorkforceSourceObservation(fixture.TenantId, registration.ObservationId,
            1, 1, "accepted", "Checked source facts");
        var dismissed = accepted with { Outcome = "dismissed", Note = "Keep the canonical facts" };
        var restrictedPermission = managerOnly
            ? FieldClasses.WorkforceManagerChain.ReadPermission : FieldClasses.WorkforcePersonalDetails.ReadPermission;
        fixture.Permissions.SetRestrictedPermissions(restrictedPermission);

        // Act
        var matching = await PersonalWorkforceObservationTransportTests.HttpAsync(fixture.Provider, fixture.UserId, accepted, RequestErrorKind.Forbidden);
        var kept = await PersonalWorkforceObservationTransportTests.HttpAsync(fixture.Provider, fixture.UserId, dismissed, RequestErrorKind.Forbidden);
        fixture.Permissions.SetRestrictedPermissions(FieldClasses.WorkforceManagerChain.ReadPermission,
            FieldClasses.WorkforcePersonalDetails.ReadPermission);
        var decision = privateValues ? accepted : dismissed;
        await PersonalWorkforceObservationTransportTests.HttpAsync(fixture.Provider, fixture.UserId, decision);
        fixture.Permissions.SetRestrictedPermissions(restrictedPermission);
        var retry = await PersonalWorkforceObservationTransportTests.HttpAsync(fixture.Provider, fixture.UserId, decision, RequestErrorKind.Forbidden);
        var guessed = await PersonalWorkforceObservationTransportTests.HttpAsync(fixture.Provider, fixture.UserId,
            decision with { Note = "guess" }, RequestErrorKind.Forbidden);

        // Assert
        Assert.Equal(matching.Error!.Message, kept.Error!.Message);
        Assert.Equal(matching.Error.Message, retry.Error!.Message);
        Assert.Equal(matching.Error.Message, guessed.Error!.Message);
    }

    sealed class Fixture : IAsyncDisposable
    {
        public ServiceProvider Provider { get; private set; } = null!;
        public SourcePermissions Permissions { get; } = new();
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid UserId { get; } = Uuid.CreateVersion4();
        public Uuid PersonId { get; } = Uuid.CreateVersion4();
        public ActorReference Author => ActorReference.ForMember(RbacIds.Member(TenantId, UserId), "Author");
        public FitzPersonDirectory People { get; } = new(new InMemoryKvClient());
        public FitzWorkRelationshipDirectory Relationships { get; } = new(new InMemoryKvClient());
        public FitzServiceIdentityDirectory Identities { get; } = new(new InMemoryKvClient());
        public FitzWorkforceSourceDirectory Sources { get; } = new(new InMemoryKvClient());

        public static async Task<Fixture> CreateAsync()
        {
            var fixture = new Fixture();
            fixture.Permissions.UserId = fixture.UserId;
            fixture.Provider = ProgramManagementServices.Build(fixture.Permissions,
                portia => portia.AddRequestAuthorizer<WorkforceAuthorizer>()
                    .AddRequestHandler<RecordWorkforceSourceObservationHandler>()
                    .AddRequestHandler<GetWorkforceSourceObservationHandler>()
                    .AddRequestHandler<PreviewWorkforceSourceObservationHandler>()
                    .AddRequestHandler<ReconcileWorkforceSourceObservationHandler>()
                    .AddRequestHandler<ListWorkforceSourceObservationsHandler>(),
                services =>
                {
                    var store = new InMemoryEventStore();
                    services.AddSingleton<IEventStore>(store);
                    services.AddSingleton<IDomainEventReader>(store);
                    services.AddSingleton<IPersonDirectoryReader>(fixture.People);
                    services.AddSingleton<IWorkRelationshipDirectoryReader>(fixture.Relationships);
                    services.AddSingleton<IServiceIdentityDirectoryReader>(fixture.Identities);
                    services.AddSingleton<IWorkforceSourceDirectoryReader>(fixture.Sources);
                    services.AddScoped<PersonReadConsistency>();
                    services.AddScoped<WorkRelationshipReadConsistency>();
                    services.AddScoped<ServiceIdentityReadConsistency>();
                    services.AddScoped<WorkforceSourceReadConsistency>();
                    services.AddScoped<WorkforceSourceTargets>();
                });
            await ProgramManagementServices.SeedAsync(fixture.Provider, new Person(fixture.TenantId,
                fixture.PersonId), person => person.Record("Ada", "ada@example.com", fixture.Author, DateTimeOffset.UtcNow));
            await fixture.ProjectPeopleAsync();
            return fixture;
        }

        public RequestScenario As(Uuid? userId = null) => RequestScenario.For(Provider)
            .GivenActor(ProgramManagementServices.Actor(userId ?? UserId));

        public Task ProjectPeopleAsync() => ProjectAsync("PersonDirectoryV2", "people", People,
            People.ApplyAsync, People.LoadCheckpointAsync);

        public Task ProjectSourcesAsync() => ProjectAsync("WorkforceSourcesV1", "workforce-source-observations",
            Sources, Sources.ApplyAsync, Sources.LoadCheckpointAsync);

        public Task ProjectRelationshipsAsync() => ProjectAsync("WorkRelationshipDirectoryV1", "work-relationships",
            Relationships, Relationships.ApplyAsync, Relationships.LoadCheckpointAsync);

        public Task ProjectIdentitiesAsync() => ProjectAsync("ServiceIdentityDirectoryV1", "service-identities",
            Identities, Identities.ApplyAsync, Identities.LoadCheckpointAsync);

        async Task ProjectAsync(string name, string area, IProjectionStore projection,
            Func<DomainEvent, CancellationToken, ValueTask> apply,
            Func<Uuid, CancellationToken, ValueTask<ProjectionCheckpoint>> load)
        {
            var checkpoint = await load(TenantId, CancellationToken.None);
            var pattern = EventStreamPattern.ForPattern(TenantId.ToString(), area);
            await using var batch = await projection.BeginAsync(new ProjectionBatchContext(
                new CheckpointIdentity(name, pattern), checkpoint));
            var cursor = checkpoint.Cursor;
            await foreach (var record in Provider.GetRequiredService<IDomainEventReader>().ReadAsync(pattern, cursor, CancellationToken.None))
            {
                await apply(record.Event, CancellationToken.None);
                cursor = record.NextCursor;
            }
            await batch.CommitAsync(new ProjectionCheckpoint(cursor));
        }

        public ValueTask DisposeAsync() => Provider.DisposeAsync();
    }

    sealed class SourcePermissions : IPermissionAuthorizer
    {
        readonly HashSet<string> _restricted = [FieldClasses.WorkforceManagerChain.ReadPermission,
            FieldClasses.WorkforcePersonalDetails.ReadPermission];

        public Uuid UserId { get; set; }
        public Uuid? OtherUserId { get; set; }
        public bool AllowRestricted { get; set; } = true;

        public void SetRestrictedPermissions(params string[] permissions)
        {
            _restricted.Clear();
            _restricted.UnionWith(permissions);
        }

        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission,
            CancellationToken ct = default) => ValueTask.FromResult((userId == UserId || userId == OtherUserId) &&
                (permission == RbacPermissions.WorkforceManage || AllowRestricted && _restricted.Contains(permission)));
    }
}
