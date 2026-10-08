using System.Security.Claims;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.TechnologyInventory;
using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class InventoryManagementIndependenceTests
{
    [Theory]
    [InlineData("client_personnel", false, false)]
    [InlineData("client_personnel", true, false)]
    [InlineData("guest", false, false)]
    [InlineData("guest", true, false)]
    [InlineData("client_personnel", false, true)]
    [InlineData("client_personnel", true, true)]
    [InlineData("guest", false, true)]
    [InlineData("guest", true, true)]
    public async Task ShouldRefuseInventoryManagementWithoutChangingRecordsGivenCanonicalAttestHistoryAndRelinkedMembership(
        string affiliation, bool closed, bool mcp)
    {
        // Arrange
        await using var fixture = new Fixture(affiliation);
        await fixture.SeedRecordsAsync();
        await fixture.SeedHistoryAsync("attest", fixture.Tenant, closed);
        var context = fixture.Context(mcp);

        // Act
        var declare = await fixture.Bus.DispatchAsync(new DeclareApplication(fixture.Tenant, "Attempted", "Purpose"), context);
        var revise = await fixture.Bus.DispatchAsync(new ReviseApplication(fixture.Tenant, fixture.ApplicationId,
            1, "Attempted", "Purpose", null), context);
        var record = await fixture.Bus.DispatchAsync(new RecordTechnologyComponent(fixture.Tenant, "repository",
            "Attempted", fixture.PersonId), context);
        var component = await fixture.Bus.DispatchAsync(new ReviseTechnologyComponent(fixture.Tenant,
            fixture.ComponentId, 1, "Attempted", fixture.PersonId, "active"), context);
        var stage = await fixture.Bus.DispatchAsync(new StageApplicationImport(fixture.Tenant, Uuid.CreateVersion4(),
            "manual", "applications", "partial", [new("row", "Attempted", "Purpose", null)]), context);
        var application = await fixture.Reader.HydrateAsync(new DeclaredApplication(fixture.Tenant, fixture.ApplicationId));
        var technology = await fixture.Reader.HydrateAsync(new TechnologyComponent(fixture.Tenant, fixture.ComponentId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, declare.Error?.Kind);
        Assert.Equal(RequestErrorKind.Forbidden, revise.Error?.Kind);
        Assert.Equal(RequestErrorKind.Forbidden, record.Error?.Kind);
        Assert.Equal(RequestErrorKind.Forbidden, component.Error?.Kind);
        Assert.Equal(RequestErrorKind.Forbidden, stage.Error?.Kind);
        Assert.Equal(1, application.Revision);
        Assert.Equal(1, technology.Revision);
        Assert.Equal("Original component", technology.Content!.Name);
    }

    [Theory]
    [InlineData("none", "client_personnel")]
    [InlineData("advisory", "client_personnel")]
    [InlineData("other_client", "client_personnel")]
    [InlineData("none", "guest")]
    public async Task ShouldPersistInventoryManagementGivenOrdinaryGrantsAndNoOwningAttestHistory(string history, string affiliation)
    {
        // Arrange
        await using var fixture = new Fixture(affiliation);
        await fixture.SeedRecordsAsync();
        if (history != "none")
            await fixture.SeedHistoryAsync(history == "advisory" ? "advisory" : "attest",
                history == "other_client" ? Uuid.CreateVersion4() : fixture.Tenant, true);

        // Act
        var application = await fixture.Bus.SendAsync(new ReviseApplication(fixture.Tenant, fixture.ApplicationId,
            1, "Allowed revision", "Purpose", null), fixture.Actor);
        var component = await fixture.Bus.SendAsync(new ReviseTechnologyComponent(fixture.Tenant,
            fixture.ComponentId, 1, "Allowed revision", fixture.PersonId, "active"), fixture.Actor);
        var retained = await fixture.Reader.HydrateAsync(new TechnologyComponent(fixture.Tenant, fixture.ComponentId));

        // Assert
        Assert.True(application.IsSuccess, application.Error?.Message);
        Assert.True(component.IsSuccess, component.Error?.Message);
        Assert.Equal(2, retained.Revision);
        Assert.Equal("Allowed revision", retained.Content!.Name);
    }

    [Fact]
    public async Task ShouldPreserveAuthorizedInventoryReadsGivenClosedAttestHistory()
    {
        // Arrange
        await using var fixture = new Fixture("guest");
        await fixture.SeedRecordsAsync();
        await fixture.SeedHistoryAsync("attest", fixture.Tenant, true);
        await fixture.ProjectRecordsAsync();
        var context = fixture.Context(true);

        // Act
        var application = await fixture.Bus.DispatchAsync(new GetApplication(fixture.Tenant, fixture.ApplicationId), context);
        var component = await fixture.Bus.DispatchAsync(new GetTechnologyComponent(fixture.Tenant, fixture.ComponentId), context);

        // Assert
        Assert.True(application.IsSuccess, application.Error?.Message);
        Assert.True(component.IsSuccess, component.Error?.Message);
        Assert.Equal("Original application", application.Value!.Name);
        Assert.Equal("Original component", component.Value!.Content.Name);
    }

    [Fact]
    public async Task ShouldDenyInventoryWriteGivenNoHistoryAndMissingOrdinaryGrant()
    {
        // Arrange
        await using var fixture = new Fixture("client_personnel");
        fixture.Permissions.Allowed = false;

        // Act
        var application = await fixture.Bus.SendAsync(new DeclareApplication(fixture.Tenant, "Attempted", "Purpose"), fixture.Actor);
        var component = await fixture.Bus.SendAsync(new RecordTechnologyComponent(fixture.Tenant,
            "repository", "Attempted", Uuid.CreateVersion4()), fixture.Actor);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, application.Error?.Kind);
        Assert.Equal(RequestErrorKind.Forbidden, component.Error?.Kind);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        readonly Uuid _administrator = Uuid.CreateVersion4();
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid User { get; } = Uuid.CreateVersion4();
        public Uuid PersonId { get; } = Uuid.CreateVersion4();
        public Uuid ApplicationId { get; private set; }
        public Uuid ComponentId { get; private set; }
        public IRequestBus Bus { get; }
        public IAggregateReader Reader { get; }
        public IAggregateExecutor Executor { get; }
        public Permissions Permissions { get; } = new();
        public ClaimsPrincipal Actor => PersonalActor(User);

        public Fixture(string affiliation)
        {
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                ["Fitz:ApplicationName"] = "compliance"
            }).Build();
            services.AddCompliance(configuration, developerAuthentication: true);
            var events = new InMemoryEventStore();
            services.AddSingleton<IEventStore>(events);
            services.AddSingleton<IDomainEventReader>(events);
            services.AddSingleton<IKvClient>(new InMemoryKvClient());
            services.AddSingleton<IPermissionAuthorizer>(Permissions);
            services.AddSingleton<ITenantActivity>(new ActiveTenant());
            services.AddSingleton<ITenantMembershipDirectoryReader>(new MembershipDirectory(Tenant, User, _administrator, affiliation));
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
            Bus = _scope.ServiceProvider.GetRequiredService<IRequestBus>();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        }

        public RequestDispatchContext Context(bool mcp) => new(Actor, mcp
            ? new McpInvocation("synthetic-inventory-command")
            : new HttpInvocation("POST", "/synthetic", "/synthetic", "synthetic-inventory-command"));

        public async Task SeedRecordsAsync()
        {
            var admin = PersonalActor(_administrator);
            var person = await Executor.ExecuteAsync(new Person(Tenant, PersonId), source =>
                AggregateOutcome.CommitOnSuccess(source.Record("Synthetic owner", null,
                    ActorReference.ForMember(RbacIds.Member(Tenant, _administrator), "Client administrator"), DateTimeOffset.UtcNow)),
                new RequestDispatchContext(admin));
            Assert.True(person.IsSuccess);
            var application = await Bus.SendAsync(new DeclareApplication(Tenant, "Original application", "Purpose"), admin);
            Assert.True(application.IsSuccess, application.Error?.Message);
            ApplicationId = application.Value!.ApplicationId;
            var component = await Bus.SendAsync(new RecordTechnologyComponent(Tenant, "repository", "Original component", PersonId), admin);
            Assert.True(component.IsSuccess, component.Error?.Message);
            ComponentId = component.Value!.ComponentId;
        }

        public async Task SeedHistoryAsync(string practice, Uuid tenant, bool closed)
        {
            await AttestAssignmentHistoryFixture.SeedAsync(_provider, tenant, User, closed, practice);
            var retained = await Reader.HydrateAsync(new IndependenceLedger(tenant));
            Assert.Equal(User, Assert.Single(retained.ActualAssignmentHistory).UserId);
        }

        public async Task ProjectRecordsAsync()
        {
            var events = _scope.ServiceProvider.GetRequiredService<IDomainEventReader>();
            var application = _scope.ServiceProvider.GetRequiredService<IApplicationDirectoryProjection>();
            await ProjectAsync(application, application.ApplyAsync,
                new CheckpointIdentity("ApplicationDirectoryV2", EventStreamPattern.ForPattern(Tenant.ToString())));
            var technology = _scope.ServiceProvider.GetRequiredService<ITechnologyInventoryProjection>();
            await ProjectAsync(technology, technology.ApplyAsync,
                new CheckpointIdentity(FitzTechnologyInventoryDirectory.ProjectorName, TechnologyInventoryStreams.TenantPattern(Tenant)));

            async Task ProjectAsync(IProjectionStore store, Func<DomainEvent, CancellationToken, ValueTask> apply, CheckpointIdentity identity)
            {
                await using var batch = await store.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start));
                var cursor = ProjectionCheckpoint.Start.Cursor;
                await foreach (var record in events.ReadAsync(identity.Pattern, cursor, CancellationToken.None))
                {
                    await apply(record.Event, CancellationToken.None);
                    cursor = record.NextCursor;
                }
                await batch.CommitAsync(new ProjectionCheckpoint(cursor));
            }
        }

        static ClaimsPrincipal PersonalActor(Uuid user) => new(new ClaimsIdentity([
            new Claim("iss", "bdgrz"), new Claim("sub", user.ToString())], "BdgrzSession"));

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed class Permissions : IPermissionAuthorizer
    {
        public bool Allowed { get; set; } = true;
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission,
            CancellationToken ct = default) => ValueTask.FromResult(Allowed);
    }

    sealed class MembershipDirectory(Uuid tenant, Uuid user, Uuid administrator, string affiliation) : ITenantMembershipDirectoryReader
    {
        public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult<TenantMembershipView?>(tenantId == tenant.ToString() && (userId == user || userId == administrator)
                ? new TenantMembershipView(userId, tenant, userId == user ? affiliation : "client_personnel") : null);
        public async ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            await GetAsync(tenantId, userId, ct) is not null;
        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }
}
