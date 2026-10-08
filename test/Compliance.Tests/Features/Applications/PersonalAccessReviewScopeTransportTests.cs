using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class PersonalAccessReviewScopeTransportTests
{
    [Theory]
    [InlineData("direct")]
    [InlineData("mcp")]
    [InlineData("http")]
    public async Task ShouldEnforcePersonalTransportGivenValidScopeApproval(string transport)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var before = await fixture.ReadAsync();
        var instanceBefore = await fixture.ReadInstanceAsync();

        // Act
        var result = await fixture.DecideAsync(transport);
        var after = await fixture.ReadAsync();
        var instanceAfter = await fixture.ReadInstanceAsync();

        // Assert
        if (transport == "http")
        {
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.Equal(before.CommittedStreamPosition + 1, after.CommittedStreamPosition);
            var decision = Assert.Single(after.Decisions);
            Assert.Equal(RbacIds.Member(fixture.Tenant, fixture.Approver).ToString(), decision.ApprovedBy.Id);
            Assert.Equal(fixture.Application, decision.ApplicationId);
            Assert.Equal(fixture.Instance, decision.SystemInstanceId);
            Assert.Equal(1, decision.SystemInstanceRevision);
        }
        else
        {
            Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
            Assert.Contains("personal HTTP", result.Error!.Message, StringComparison.Ordinal);
            Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
            Assert.Empty(after.Decisions);
        }
        Assert.Equal(instanceBefore.CommittedStreamPosition, instanceAfter.CommittedStreamPosition);
        Assert.Equal(instanceBefore.Revision, instanceAfter.Revision);
    }

    [Theory]
    [InlineData("registrant")]
    [InlineData("owner")]
    public async Task ShouldPreserveScopeSeparationGivenNativeHttpConflictingActor(string role)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var before = await fixture.ReadAsync();

        // Act
        var result = await fixture.DecideAsync("http", role == "registrant" ? fixture.Registrant : fixture.Owner);
        var after = await fixture.ReadAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal("The registrant or access owner cannot approve the system instance's access-review scope without an active exact-scope waiver.", result.Error!.Message);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldPreserveOrganizationManagementGrantGivenNativeHttpWithoutGrant()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        fixture.Permissions.ManagesProgram = false;
        var before = await fixture.ReadAsync();

        // Act
        var result = await fixture.DecideAsync("http");
        var after = await fixture.ReadAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal("Access-review scope approval requires organization-wide program management.", result.Error!.Message);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldPreserveHistoricalManagementWallGivenNativeHttpWithOrdinaryGrants()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        await fixture.SeedAttestHistoryAsync();
        var before = await fixture.ReadAsync();

        // Act
        var result = await fixture.DecideAsync("http");
        var after = await fixture.ReadAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Contains("Attest", result.Error!.Message, StringComparison.Ordinal);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldPreserveApplicationParentGivenNativeHttpWithForeignApplication()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var other = await fixture.DeclareApplicationAsync();
        var before = await fixture.ReadAsync();

        // Act
        var result = await fixture.DecideAsync("http", application: other);
        var after = await fixture.ReadAsync();

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Equal("The system instance was not found.", result.Error!.Message);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldPreserveExactInstanceRevisionGivenNativeHttpWithStaleSource()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var before = await fixture.ReadAsync();

        // Act
        var result = await fixture.DecideAsync("http", revision: 2);
        var after = await fixture.ReadAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal("The system instance is at revision 1; the decision names revision 2.", result.Error!.Message);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid Registrant { get; } = Uuid.CreateVersion4();
        public Uuid Owner { get; } = Uuid.CreateVersion4();
        public Uuid Approver { get; } = Uuid.CreateVersion4();
        public Uuid Application { get; private set; }
        public Uuid Instance { get; private set; }
        public Permissions Permissions { get; } = new();

        Fixture()
        {
            var services = new ServiceCollection();
            services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                ["Fitz:ApplicationName"] = "compliance"
            }).Build(), developerAuthentication: true);
            var events = new InMemoryEventStore();
            services.AddSingleton<IEventStore>(events);
            services.AddSingleton<IDomainEventReader>(events);
            services.AddSingleton<IKvClient>(new InMemoryKvClient());
            services.AddSingleton<IPermissionAuthorizer>(Permissions);
            services.AddSingleton<IAccessGrantPermissionAuthorizer>(new PermissionBackedAccessGrantPermissionAuthorizer(Permissions));
            services.AddSingleton<ITenantActivity, ActiveTenant>();
            services.AddSingleton<ITenantMembershipDirectoryReader, AlwaysMemberDirectory>();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        }

        public static async Task<Fixture> CreateAsync()
        {
            var fixture = new Fixture();
            var personId = Uuid.CreateVersion4();
            await ProgramManagementServices.SeedAsync(fixture._provider, new Person(fixture.Tenant, personId), person =>
            {
                var actor = ActorReference.ForMember(RbacIds.Member(fixture.Tenant, fixture.Registrant), "Registrant");
                Assert.True(person.Record("Access owner", null, actor, DateTimeOffset.UtcNow).IsSuccess);
                Assert.Null(person.CorrelateMembership(1, fixture.Owner, actor, DateTimeOffset.UtcNow));
                return Result.Success;
            });
            fixture.Application = await fixture.DeclareApplicationAsync(personId);
            await using var scope = fixture._provider.CreateAsyncScope();
            var instance = await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(
                new DeclareSystemInstance(fixture.Tenant, fixture.Application, 1, "Production", "production"),
                ProgramManagementServices.Actor(fixture.Registrant));
            Assert.True(instance.IsSuccess, instance.Error?.Message);
            fixture.Instance = instance.Value.SystemInstanceId;
            return fixture;
        }
        public async Task<Uuid> DeclareApplicationAsync(Uuid? owner = null)
        {
            await using var scope = _provider.CreateAsyncScope();
            var application = await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(
                new DeclareApplication(Tenant, "Payroll", "Run payroll", AccessOwnerPersonId: owner),
                ProgramManagementServices.Actor(Registrant));
            Assert.True(application.IsSuccess, application.Error?.Message);
            return application.Value.ApplicationId;
        }
        public Task SeedAttestHistoryAsync() => AttestAssignmentHistoryFixture.SeedAsync(_provider, Tenant, Approver, revoked: true);
        public Task<SystemInstanceAccessReviewScope> ReadAsync() => ProgramManagementServices.HydrateAsync(_provider, new SystemInstanceAccessReviewScope(Tenant, Instance));
        public Task<DeclaredSystemInstance> ReadInstanceAsync() => ProgramManagementServices.HydrateAsync(_provider, new DeclaredSystemInstance(Tenant, Instance));
        public async Task<Result<AccessReviewScopeDecisionView>> DecideAsync(string transport, Uuid? user = null, Uuid? application = null, long revision = 1)
        {
            await using var scope = _provider.CreateAsyncScope();
            RequestInvocation invocation = transport == "http" ? new HttpInvocation("POST", "/synthetic/scope/decision", "/synthetic/scope/decision", "synthetic") :
                transport == "mcp" ? new McpInvocation("synthetic.scope.decision") : new DirectInvocation();
            return await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
                new DecideAccessReviewScope(Tenant, application ?? Application, Instance, revision, 0, "included", "Production data", DateTimeOffset.UtcNow),
                new RequestDispatchContext(ProgramManagementServices.Actor(user ?? Approver), invocation), CancellationToken.None);
        }
        public ValueTask DisposeAsync() => _provider.DisposeAsync();
    }

    sealed class Permissions : IPermissionAuthorizer
    {
        public bool ManagesProgram { get; set; } = true;
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission,
            CancellationToken ct = default) => ValueTask.FromResult(permission != IProgramScopedRequest.ManagementPermission || ManagesProgram);
    }
}
