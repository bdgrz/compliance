using System.Security.Claims;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Workforce;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationOwnerTests
{
    static readonly DateTimeOffset Now = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldReplayOnlyIdenticalOwnersGivenDeclaredApplication()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var systemOwner = Uuid.CreateVersion4();
        var accessOwner = Uuid.CreateVersion4();
        var application = new DeclaredApplication(tenantId, Uuid.CreateVersion4());
        Assert.True(application.Declare("Payroll", "Run payroll", null, actorId, "Manager",
            Now, null, systemOwner, accessOwner).IsSuccess);

        // Act
        var replay = application.Declare("Payroll", "Run payroll", null, actorId, "Manager",
            Now, null, systemOwner, accessOwner);
        var changedOwner = application.Declare("Payroll", "Run payroll", null, actorId,
            "Manager", Now, null, systemOwner, Uuid.CreateVersion4());
        var revised = application.Revise(1, "Payroll", "Run payroll", null, actorId,
            "Manager", Now.AddMinutes(1), null, accessOwner, null);

        // Assert
        Assert.True(replay.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict,
            Assert.IsType<RequestError>(changedOwner.Error).Kind);
        Assert.True(revised.IsSuccess);
        var events = new AggregateScenario<DeclaredApplication>(application).PendingEvents;
        var declared = Assert.IsType<ApplicationDeclared>(events[0]);
        Assert.Equal(systemOwner, declared.SystemOwnerPersonId);
        Assert.Equal(accessOwner, declared.AccessOwnerPersonId);
        var revision = Assert.IsType<ApplicationRevised>(events[1]);
        Assert.Equal(accessOwner, revision.SystemOwnerPersonId);
        Assert.Null(revision.AccessOwnerPersonId);
    }

    [Fact]
    public async Task ShouldProjectOwnersAndMissingOwnerGapsGivenDeclarationAndRevision()
    {
        // Arrange
        var directory = new FitzApplicationDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var legacyId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var systemOwner = Uuid.CreateVersion4();
        var accessOwner = Uuid.CreateVersion4();
        var identity = new CheckpointIdentity("ApplicationDirectoryV2",
            EventStreamPattern.ForPattern(tenantId.ToString()));

        // Act
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new ApplicationDeclared(tenantId, applicationId,
                "Payroll", "Run payroll", null, actorId, "Manager", Now, null, systemOwner));
            await directory.ApplyAsync(new ApplicationRevised(tenantId, applicationId, 2,
                "Payroll", "Run payroll", null, actorId, "Manager", Now.AddMinutes(1), null,
                systemOwner, accessOwner));
            await directory.ApplyAsync(new ApplicationDeclared(tenantId, legacyId,
                "Legacy", "Declared before owners", "Operations", actorId, "Manager", Now));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var current = await directory.GetAsync(tenantId, applicationId);
        var first = await directory.GetRevisionAsync(tenantId, applicationId, 1);
        var legacy = await directory.GetAsync(tenantId, legacyId);

        // Assert
        Assert.NotNull(current);
        Assert.Equal(systemOwner, current.SystemOwnerPersonId);
        Assert.Equal(accessOwner, current.AccessOwnerPersonId);
        Assert.DoesNotContain("system_owner_missing", current.Unresolved);
        Assert.DoesNotContain("access_owner_missing", current.Unresolved);
        Assert.NotNull(first);
        Assert.Equal(systemOwner, first.SystemOwnerPersonId);
        Assert.Null(first.AccessOwnerPersonId);
        Assert.Contains("access_owner_missing", first.Unresolved);
        Assert.NotNull(legacy);
        Assert.Null(legacy.SystemOwnerPersonId);
        Assert.Contains("system_owner_missing", legacy.Unresolved);
        Assert.Contains("access_owner_missing", legacy.Unresolved);
        Assert.Contains("owner_unverified", legacy.Unresolved);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ShouldRejectForeignTenantOwnerGivenApplicationWrite(bool revise,
        bool accessOwner)
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var foreignTenantId = Uuid.CreateVersion4();
        var foreignOwnerId = Uuid.CreateVersion4();
        var foreignOwner = new Person(foreignTenantId, foreignOwnerId);
        Assert.True(foreignOwner.Record("Ada Lovelace", null,
            ActorReference.ForMember(Uuid.CreateVersion4(), "Manager"), Now).IsSuccess);
        await scenario.Writer.SaveAsync(foreignOwner,
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        var requestId = Uuid.CreateVersion4();
        // Act
        RequestError? error;
        if (revise)
        {
            error = (await scenario.ReviseHandler.HandleAsync(scenario.ReviseContext(requestId,
                foreignOwnerId, accessOwner), CancellationToken.None)).Error;
        }
        else
        {
            var invalidOwner = new DeclareApplication(scenario.TenantId, "Payroll", "Run payroll",
                SystemOwnerPersonId: accessOwner ? null : foreignOwnerId,
                AccessOwnerPersonId: accessOwner ? foreignOwnerId : null);
            error = (await scenario.DeclareHandler.HandleAsync(
                new FixedRequestContext<DeclareApplication>(invalidOwner, requestId),
                CancellationToken.None)).Error;
        }

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(error).Kind);
        Assert.Empty(await scenario.ApplicationEventsAsync(requestId));
    }

    [Fact]
    public async Task ShouldPreserveTenantOwnerReferencesGivenApplicationDeclarationAndRevision()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var systemOwnerId = await scenario.RecordPersonAsync("System Owner");
        var accessOwnerId = await scenario.RecordPersonAsync("Access Owner");
        var applicationId = Uuid.CreateVersion4();
        var declaration = new DeclareApplication(scenario.TenantId, "Payroll", "Run payroll",
            SystemOwnerPersonId: systemOwnerId, AccessOwnerPersonId: accessOwnerId);
        var declared = await scenario.DeclareHandler.HandleAsync(
            new FixedRequestContext<DeclareApplication>(declaration, applicationId),
            CancellationToken.None);
        Assert.True(declared.IsSuccess);
        var revisionId = Uuid.CreateVersion4();
        var revision = new ReviseApplication(scenario.TenantId, applicationId, 1, "Payroll",
            "Run payroll", null, SystemOwnerPersonId: accessOwnerId,
            AccessOwnerPersonId: systemOwnerId);

        // Act
        var revised = await scenario.ReviseHandler.HandleAsync(
            new FixedRequestContext<ReviseApplication>(revision, revisionId),
            CancellationToken.None);

        // Assert
        Assert.True(revised.IsSuccess);
        var events = await scenario.ApplicationEventsAsync(applicationId);
        var applicationDeclared = Assert.IsType<ApplicationDeclared>(events[0].Event);
        Assert.Equal(systemOwnerId, applicationDeclared.SystemOwnerPersonId);
        Assert.Equal(accessOwnerId, applicationDeclared.AccessOwnerPersonId);
        var applicationRevised = Assert.IsType<ApplicationRevised>(events[1].Event);
        Assert.Equal(accessOwnerId, applicationRevised.SystemOwnerPersonId);
        Assert.Equal(systemOwnerId, applicationRevised.AccessOwnerPersonId);
    }

    sealed class Scenario : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;

        Scenario(ServiceProvider provider, Uuid tenantId, Uuid existingApplicationId)
        {
            _provider = provider;
            _scope = provider.CreateAsyncScope();
            TenantId = tenantId;
            ExistingApplicationId = existingApplicationId;
        }

        public Uuid TenantId { get; }
        public Uuid ExistingApplicationId { get; }
        public IAggregateReader Reader => _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        public IAggregateWriter Writer => _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        public IAggregateExecutor Executor => _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        IEventStore Events => _scope.ServiceProvider.GetRequiredService<IEventStore>();
        public DeclareApplicationHandler DeclareHandler =>
            new(Executor, Reader, TimeProvider.System);
        public ReviseApplicationHandler ReviseHandler =>
            new(Executor, Reader, TimeProvider.System);

        public static async Task<Scenario> CreateAsync()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(new InMemoryEventStore());
            services.AddPortia();
            var existingId = Uuid.CreateVersion4();
            var scenario = new Scenario(services.BuildServiceProvider(
                    new ServiceProviderOptions { ValidateScopes = true }), Uuid.CreateVersion4(),
                existingId);
            var existing = new DeclaredApplication(scenario.TenantId, existingId);
            Assert.True(existing.Declare("Existing", "Existing app", null,
                Uuid.CreateVersion4(), "Manager", Now).IsSuccess);
            await scenario.Writer.SaveAsync(existing,
                new RequestDispatchContext(RequestActor.System), CancellationToken.None);
            return scenario;
        }

        public async Task<Uuid> RecordPersonAsync(string displayName)
        {
            var personId = Uuid.CreateVersion4();
            var person = new Person(TenantId, personId);
            Assert.True(person.Record(displayName, null,
                ActorReference.ForMember(Uuid.CreateVersion4(), "Manager"), Now).IsSuccess);
            await Writer.SaveAsync(person, new RequestDispatchContext(RequestActor.System),
                CancellationToken.None);
            return personId;
        }

        public FixedRequestContext<ReviseApplication> ReviseContext(Uuid requestId,
            Uuid foreignOwnerId, bool accessOwner) =>
            new(new ReviseApplication(TenantId, ExistingApplicationId, 1, "Existing",
                "Existing app", null,
                SystemOwnerPersonId: accessOwner ? null : foreignOwnerId,
                AccessOwnerPersonId: accessOwner ? foreignOwnerId : null), requestId);

        public async Task<List<DomainEventRecord>> ApplicationEventsAsync(Uuid applicationId)
        {
            var records = new List<DomainEventRecord>();
            await foreach (var record in Events.ReadAsync(new EventStreamAddress(
                               TenantId.ToString(), "applications", applicationId.ToString()),
                               0, CancellationToken.None))
                records.Add(record);
            return records;
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed class FixedRequestContext<TRequest>(TRequest request, Uuid requestId)
        : IRequestContext<TRequest> where TRequest : notnull
    {
        public TRequest Request => request;
        public ClaimsPrincipal Actor { get; } = BdgrzActor();
        public Uuid ExecutionId { get; } = Uuid.CreateVersion4();
        public Uuid RequestId => requestId;
        public Uuid CorrelationId => requestId;
        public Uuid? CausationId => null;
        public Uuid CauseId => requestId;
        public DateTimeOffset StartedAt { get; } = Now;
        public RequestInvocation Invocation { get; } = new DirectInvocation();
    }

    static ClaimsPrincipal BdgrzActor() => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())],
        "BdgrzSession"));
}
