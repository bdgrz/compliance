using System.Security.Claims;
using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Providers;

public sealed class ProviderAuthorizationTests
{
    [Theory]
    [InlineData("organization", false, null)]
    [InlineData("organization", true, null)]
    [InlineData("read_only", false, null)]
    [InlineData("read_only", true, RequestErrorKind.Forbidden)]
    [InlineData("program", false, RequestErrorKind.Forbidden)]
    [InlineData("program", true, RequestErrorKind.Forbidden)]
    [InlineData("missing", false, RequestErrorKind.NotFound)]
    [InlineData("suspended", true, RequestErrorKind.NotFound)]
    [InlineData("unprojected_suspension", true, RequestErrorKind.Forbidden)]
    [InlineData("inactive", false, RequestErrorKind.Forbidden)]
    [InlineData("tenant_suspended", true, RequestErrorKind.Forbidden)]
    [InlineData("firm_staff", false, RequestErrorKind.Forbidden)]
    [InlineData("operator", true, RequestErrorKind.Unauthorized)]
    public async Task ShouldEnforceOrganizationAuthorityGivenActualGrantAndRequestPipeline(
        string condition, bool write, RequestErrorKind? expected)
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var roleId = Uuid.CreateVersion4();
        string[] allowed = condition == "read_only" ? [RbacPermissions.TenantAccess] :
            [RbacPermissions.TenantAccess, RbacPermissions.ProviderInventoryManage];
        var services = new ServiceCollection();
        var store = new InMemoryEventStore();
        var grants = new Grants(tenantId, userId, roleId, condition == "program");
        services.AddSingleton<IEventStore>(store);
        services.AddSingleton<IDomainEventReader>(store);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ITenantMembershipDirectoryReader>(new FixedMembershipDirectory(condition != "missing",
            condition == "firm_staff" ? "firm_staff" : "client_personnel", condition == "suspended"));
        services.AddSingleton<IAccessGrantDirectory>(grants);
        services.AddSingleton<ITeamMemberDirectoryReader, NoTeams>();
        services.AddSingleton<IRolePermissionDirectoryReader>(new RolePermissions(allowed));
        services.AddScoped<IMemberAccessEligibility, EventSourcedMemberAccessEligibility>();
        services.AddScoped<ITenantActivity, EventSourcedTenantActivity>();
        services.AddScoped<IAccessGrantPermissionAuthorizer, AccessGrantPermissionAuthorizer>();
        services.AddPortia().AddRequestAuthorizer<ProviderAuthorizer>()
            .AddRequestHandler<ProviderReadAuthorizationProbeHandler>()
            .AddRequestHandler<ProviderWriteAuthorizationProbeHandler>();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await ProgramManagementServices.SeedAsync(provider, new Tenant(tenantId), tenant =>
        {
            Assert.True(tenant.Register(userId, "Organization", "provider-test").IsSuccess);
            if (condition != "inactive")
                Assert.True(tenant.ConfirmSlug("provider-test").IsSuccess);
            if (condition == "tenant_suspended")
                Assert.True(tenant.Suspend(userId, "Provider authorization test", DateTimeOffset.UnixEpoch).IsSuccess);
            return Result.Success;
        });
        await ProgramManagementServices.SeedAsync(provider, new Member(tenantId, userId), member =>
        {
            Assert.True(member.Register(condition == "firm_staff" ? "firm_staff" : "client_personnel").IsSuccess);
            if (condition == "unprojected_suspension")
                Assert.True(member.Suspend(RbacIds.Member(tenantId, userId), "Admin", DateTimeOffset.UtcNow, "Review").IsSuccess);
            return Result.Success;
        });
        await using (var scope = provider.CreateAsyncScope())
        {
            grants.MembershipEpisodeId = (await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
                .HydrateAsync(new Member(tenantId, userId))).MembershipEpisodeId;
        }
        await ProgramManagementServices.SeedAsync(provider, new Role(tenantId, roleId), role => role.Define("Authored role"));
        foreach (var permission in allowed)
            await ProgramManagementServices.SeedAsync(provider, new RolePermission(tenantId, roleId, permission), item => item.Assign());
        var actor = condition == "operator" ? new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "platform"), new Claim("sub", userId.ToString()), new Claim("role", "operator")], "Operator"))
            : ProgramManagementServices.Actor(userId);
        var scenario = RequestScenario.For(provider).GivenActor(actor);

        // Act
        RequestError? error;
        if (write)
        {
            var call = scenario.When(new RecordProvider(tenantId, new ProviderContent("Provider", "Supplier")));
            error = (expected is { } denied ? await call.ExpectDenied(denied).ExpectNotHandled() : await call.ExpectSuccess().ExpectHandled()).Error;
        }
        else
        {
            var call = scenario.When(new GetProvider(tenantId, Uuid.CreateVersion4()));
            error = (expected is { } denied ? await call.ExpectDenied(denied).ExpectNotHandled() : await call.ExpectSuccess().ExpectHandled()).Error;
        }

        // Assert
        Assert.Equal(expected, error?.Kind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldBackfillOnlyProviderAuthoringGrantsGivenNewOrRetainedTenantRegistration(bool historical)
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var ev = DomainEventSeed.Attach(new TenantRegistered(tenantId, Uuid.CreateVersion4(), "Organization", "provider-test"),
            tenantId, 1, occurredOn: historical ? DateTimeOffset.UtcNow.AddYears(-1) : DateTimeOffset.UtcNow);
        var scenario = new ReactorScenario().Given(ev);

        // Act
        await scenario.RunAsync(new ProviderInventoryGrantBackfillReactor(new InMemoryProjectionCheckpointStore(), scenario.Requests));

        // Assert
        Assert.Equal(2, scenario.SentRequests.Count);
        Assert.All(scenario.SentRequests, request => Assert.Equal(RbacPermissions.ProviderInventoryManage,
            Assert.IsType<AssignRolePermission>(request).Permission));
        Assert.Equal(new[] { BuiltInRbac.TenantAdministrationRoleId(tenantId), BuiltInRbac.ComplianceManagementRoleId(tenantId) }.Order(),
            scenario.SentRequests.Cast<AssignRolePermission>().Select(static request => request.RoleId).Order());
    }

    sealed class Grants(Uuid tenantId, Uuid userId, Uuid roleId, bool programOnly) : IAccessGrantDirectory
    {
        public Uuid? MembershipEpisodeId { get; set; }
        readonly AccessGrantView _grant = new(tenantId, Uuid.CreateVersion4(), new AccessGrantTerms(
            new AccessGrantPrincipal(AccessGrantPrincipalKind.Member, RbacIds.Member(tenantId, userId)), roleId,
            new AccessGrantScope(programOnly ? AccessGrantScopeKind.Program : AccessGrantScopeKind.Organization,
                programOnly ? Uuid.CreateVersion4() : tenantId), new AccessGrantSource("manual", "source"),
            ActorReference.ForMember(RbacIds.Member(tenantId, userId), "Admin"), DateTimeOffset.UtcNow.AddDays(-1), null), null, null);

        public ValueTask<AccessGrantSetView> ListAsync(Uuid requestedTenantId, CancellationToken ct = default) =>
            ValueTask.FromResult(new AccessGrantSetView(requestedTenantId, 1, [_grant]));
        public ValueTask<AccessGrantView?> GetAsync(Uuid requestedTenantId, Uuid grantId, CancellationToken ct = default) =>
            ValueTask.FromResult<AccessGrantView?>(grantId == _grant.GrantId ? _grant : null);
        public ValueTask<Uuid?> GetMembershipEpisodeIdAsync(Uuid requestedTenantId, Uuid grantId,
            CancellationToken ct = default) => ValueTask.FromResult(MembershipEpisodeId);
        public ValueTask<IReadOnlySet<Uuid>> FindPendingRevocationsAsync(Uuid requestedTenantId, IReadOnlySet<Uuid> ids,
            CancellationToken ct = default) => ValueTask.FromResult<IReadOnlySet<Uuid>>(new HashSet<Uuid>());
    }

    sealed class NoTeams : ITeamMemberDirectoryReader
    {
        public ValueTask<Page<TeamMemberView>> ListAsync(Uuid tenantId, Uuid teamId, int? limit, string? cursor,
            string? search, bool descending, CancellationToken ct = default) => ValueTask.FromResult(new Page<TeamMemberView>([], null));
    }

    sealed class RolePermissions(IReadOnlyList<string> allowed) : IRolePermissionDirectoryReader
    {
        public ValueTask<Page<RolePermissionView>> ListAsync(Uuid tenantId, Uuid roleId, int? limit, string? cursor,
            string? search, bool descending, CancellationToken ct = default) => ValueTask.FromResult(new Page<RolePermissionView>(
                allowed.Select(permission => new RolePermissionView(roleId, permission)).ToArray(), null));
    }
}
