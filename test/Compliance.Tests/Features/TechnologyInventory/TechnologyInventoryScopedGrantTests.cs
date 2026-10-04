using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Features.TechnologyInventory;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.TechnologyInventory;

public sealed class TechnologyInventoryScopedGrantTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("information_asset", "public")]
    [InlineData("information_asset", "restricted")]
    [InlineData("data_flow", "public")]
    [InlineData("data_flow", "restricted")]
    public async Task ShouldReadChosenRecordGivenViewerWithExactComplianceLeadGrant(
        string resourceType, string classification)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(resourceType, classification);
        var scenario = RequestScenario.For(fixture.Services).GivenActor(fixture.Actor);

        // Act
        var allowed = resourceType == TechnologyInventoryResourceTypes.InformationAsset
            ? (await scenario.When(new GetInformationAsset(fixture.TenantId, fixture.ResourceId))
                .ExpectSuccess().ExpectHandled()).IsSuccess
            : (await scenario.When(new GetDataFlow(fixture.TenantId, fixture.ResourceId))
                .ExpectSuccess().ExpectHandled()).IsSuccess;

        // Assert
        Assert.True(allowed);
        await using var scope = fixture.Services.CreateAsyncScope();
        var permissions = scope.ServiceProvider.GetRequiredService<IPermissionAuthorizer>();
        Assert.True(await permissions.IsAllowedAsync(fixture.TenantId, fixture.UserId,
            RbacIds.Member(fixture.TenantId, fixture.UserId), RbacPermissions.TenantAccess));
        Assert.False(await permissions.IsAllowedAsync(fixture.TenantId, fixture.UserId,
            RbacIds.Member(fixture.TenantId, fixture.UserId), RbacPermissions.TechnologyInventoryManage));
    }

    [Theory]
    [InlineData("information_asset")]
    [InlineData("data_flow")]
    public async Task ShouldListOnlyChosenRecordGivenExactGrantAndUnrelatedPublicRecords(
        string resourceType)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(resourceType, "restricted");
        var scenario = RequestScenario.For(fixture.Services).GivenActor(fixture.Actor);

        // Act
        var visibleIds = resourceType == TechnologyInventoryResourceTypes.InformationAsset
            ? (await scenario.When(new ListInformationAssets(fixture.TenantId, 1))
                .ExpectSuccess().ExpectHandled()).Value.Items.Select(item => item.InformationAssetId)
            : (await scenario.When(new ListDataFlows(fixture.TenantId, 1))
                .ExpectSuccess().ExpectHandled()).Value.Items.Select(item => item.DataFlowId);

        // Assert
        Assert.Equal(fixture.ResourceId, Assert.Single(visibleIds));
    }

    [Theory]
    [InlineData("information_asset", false)]
    [InlineData("information_asset", true)]
    [InlineData("data_flow", false)]
    [InlineData("data_flow", true)]
    public async Task ShouldHideUnrelatedPublicRecordGivenGrantForAnotherRecordOrResourceType(
        string resourceType, bool differentType)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(resourceType, "restricted");
        var scenario = RequestScenario.For(fixture.Services).GivenActor(fixture.Actor);

        // Act
        var readAsset = (resourceType == TechnologyInventoryResourceTypes.InformationAsset) != differentType;
        var error = readAsset
            ? (await scenario.When(new GetInformationAsset(fixture.TenantId, fixture.OtherAssetId))
                .ExpectHandled()).Error
            : (await scenario.When(new GetDataFlow(fixture.TenantId, fixture.OtherFlowId))
                .ExpectHandled()).Error;

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(error).Kind);
    }

    [Theory]
    [InlineData("information_asset", "expired")]
    [InlineData("data_flow", "expired")]
    [InlineData("information_asset", "revoked")]
    [InlineData("data_flow", "revoked")]
    [InlineData("information_asset", "pending_revocation")]
    [InlineData("data_flow", "pending_revocation")]
    public async Task ShouldDenyReadGivenInactiveGrantOrUnprojectedRevocation(
        string resourceType, string condition)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(resourceType, "restricted", condition);
        var scenario = RequestScenario.For(fixture.Services).GivenActor(fixture.Actor);

        // Act
        var error = resourceType == TechnologyInventoryResourceTypes.InformationAsset
            ? (await scenario.When(new GetInformationAsset(fixture.TenantId, fixture.ResourceId))
                .ExpectDenied(RequestErrorKind.Forbidden).ExpectNotHandled()).Error
            : (await scenario.When(new GetDataFlow(fixture.TenantId, fixture.ResourceId))
                .ExpectDenied(RequestErrorKind.Forbidden).ExpectNotHandled()).Error;

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, Assert.IsType<RequestError>(error).Kind);
    }

    [Theory]
    [InlineData("information_asset")]
    [InlineData("data_flow")]
    public async Task ShouldHideForeignTenantGivenExactLocalResourceGrant(string resourceType)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(resourceType, "restricted");
        var scenario = RequestScenario.For(fixture.Services).GivenActor(fixture.Actor);
        var foreignTenantId = Uuid.CreateVersion4();

        // Act
        var error = resourceType == TechnologyInventoryResourceTypes.InformationAsset
            ? (await scenario.When(new GetInformationAsset(foreignTenantId, fixture.ResourceId))
                .ExpectDenied(RequestErrorKind.NotFound).ExpectNotHandled()).Error
            : (await scenario.When(new GetDataFlow(foreignTenantId, fixture.ResourceId))
                .ExpectDenied(RequestErrorKind.NotFound).ExpectNotHandled()).Error;

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(error).Kind);
    }

    [Fact]
    public async Task ShouldDenyWritesAndOtherInventoriesGivenExactAssetReadGrant()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync("information_asset", "restricted");
        await using var scope = fixture.Services.CreateAsyncScope();
        var authorizer = ActivatorUtilities.CreateInstance<TechnologyInventoryAuthorizer>(scope.ServiceProvider);
        ITechnologyInventoryRequest[] requests =
        [
            new RecordInformationAsset(fixture.TenantId, "New", "public", "RET-1", Uuid.CreateVersion4()),
            new ListTechnologyComponents(fixture.TenantId),
            new GetTechnologyComponent(fixture.TenantId, fixture.ResourceId),
            new ListLocations(fixture.TenantId),
            new ListOperationalProcesses(fixture.TenantId),
        ];

        // Act
        var results = new List<Result>();
        foreach (var request in requests)
            results.Add(await authorizer.AuthorizeAsync(
                new RequestContext<ITechnologyInventoryRequest>(request, fixture.Actor), CancellationToken.None));

        // Assert
        Assert.All(results, result => Assert.Equal(RequestErrorKind.Forbidden, result.Error!.Kind));
    }

    [Theory]
    [InlineData("public", true)]
    [InlineData("restricted", false)]
    public async Task ShouldRequireAdditionalRestrictedPermissionGivenScopedManageOnlyGrant(
        string classification, bool expected)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync("information_asset", classification,
            "manage_only");
        var scenario = RequestScenario.For(fixture.Services).GivenActor(fixture.Actor);

        // Act
        var result = await scenario.When(new GetInformationAsset(fixture.TenantId, fixture.ResourceId))
            .ExpectHandled();

        // Assert
        Assert.Equal(expected, result.IsSuccess);
        if (!expected)
            Assert.Equal(RequestErrorKind.NotFound, result.Error!.Kind);
    }

    sealed class Fixture(ServiceProvider services, Uuid tenantId, Uuid userId, Uuid resourceId,
        Uuid otherAssetId, Uuid otherFlowId) : IAsyncDisposable
    {
        public ServiceProvider Services { get; } = services;
        public Uuid TenantId { get; } = tenantId;
        public Uuid UserId { get; } = userId;
        public Uuid ResourceId { get; } = resourceId;
        public Uuid OtherAssetId { get; } = otherAssetId;
        public Uuid OtherFlowId { get; } = otherFlowId;
        public ClaimsPrincipal Actor => ProgramManagementServices.Actor(UserId);

        public static async Task<Fixture> CreateAsync(string resourceType, string classification,
            string condition = "active")
        {
            var tenantId = Uuid.CreateVersion4();
            var userId = Uuid.CreateVersion4();
            var resourceId = Uuid.CreateVersion4();
            var store = new InMemoryEventStore();
            var collection = new ServiceCollection();
            collection.AddSingleton<IEventStore>(store);
            collection.AddSingleton<IDomainEventReader>(store);
            collection.AddSingleton<IKvClient>(new InMemoryKvClient());
            collection.AddSingleton<TimeProvider>(new Clock());
            collection.AddSingleton<ITenantMembershipDirectoryReader>(new Memberships(tenantId, userId));
            collection.AddSingleton<ITeamMemberDirectoryReader, NoTeams>();
            collection.AddScoped<IMemberAccessEligibility, EventSourcedMemberAccessEligibility>();
            collection.AddScoped<ITenantActivity, EventSourcedTenantActivity>();
            collection.AddScoped<FitzPermissionAuthorizer>();
            collection.AddScoped<IPermissionAuthorizer>(provider => provider.GetRequiredService<FitzPermissionAuthorizer>());
            collection.AddScoped<FitzAccessGrantDirectory>();
            collection.AddScoped<IAccessGrantDirectory>(provider => provider.GetRequiredService<FitzAccessGrantDirectory>());
            collection.AddScoped<FitzRolePermissionDirectoryReader>();
            collection.AddScoped<IRolePermissionDirectoryReader>(provider => provider.GetRequiredService<FitzRolePermissionDirectoryReader>());
            collection.AddScoped<IAccessGrantScopePermissionAuthorizer, AccessGrantPermissionAuthorizer>();
            collection.AddScoped<FitzTechnologyInventoryDirectory>();
            collection.AddScoped<ITechnologyInventoryReader>(provider => provider.GetRequiredService<FitzTechnologyInventoryDirectory>());
            collection.AddScoped<TechnologyInventoryReadConsistency>();
            collection.AddScoped<TechnologyInventoryRestrictedVisibility>();
            collection.AddPortia().AddRequestAuthorizer<TechnologyInventoryAuthorizer>()
                .AddRequestHandler<GetInformationAssetHandler>().AddRequestHandler<GetDataFlowHandler>()
                .AddRequestHandler<ListInformationAssetsHandler>().AddRequestHandler<ListDataFlowsHandler>();
            var services = collection.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            var fixture = new Fixture(services, tenantId, userId, resourceId,
                Uuid.CreateVersion4(), Uuid.CreateVersion4());
            await fixture.SeedAsync(resourceType, classification, condition, store);
            return fixture;
        }

        async Task SeedAsync(string resourceType, string classification, string condition,
            InMemoryEventStore store)
        {
            var memberId = RbacIds.Member(TenantId, UserId);
            var viewerRoleId = BuiltInRbac.ViewerRoleId(TenantId);
            var viewerTeamId = BuiltInRbac.ViewersTeamId(TenantId);
            var leadRoleId = BuiltInRbac.ComplianceManagementRoleId(TenantId);
            await ProgramManagementServices.SeedAsync(Services, new Tenant(TenantId), tenant =>
            {
                Assert.True(tenant.Register(UserId, "Acme", "scoped-inventory").IsSuccess);
                return tenant.ConfirmSlug("scoped-inventory");
            });
            await ProgramManagementServices.SeedAsync(Services, new Member(TenantId, UserId), member => member.Register());
            var member = await ProgramManagementServices.HydrateAsync(Services, new Member(TenantId, UserId));
            await ProgramManagementServices.SeedAsync(Services, new Role(TenantId, viewerRoleId), role => role.Define("Viewer"));
            await ProgramManagementServices.SeedAsync(Services, new Team(TenantId, viewerTeamId), team => team.Define("Viewers"));
            await ProgramManagementServices.SeedAsync(Services, new TeamRole(TenantId, viewerTeamId, viewerRoleId), assignment => assignment.Assign());
            await ProgramManagementServices.SeedAsync(Services, new TeamMember(TenantId, viewerTeamId, memberId), assignment => assignment.Assign(member.MembershipEpisodeId));
            await ProgramManagementServices.SeedAsync(Services, new RolePermission(TenantId, viewerRoleId, RbacPermissions.TenantAccess), permission => permission.Assign());
            await ProgramManagementServices.SeedAsync(Services, new Role(TenantId, leadRoleId), role => role.Define("Compliance Lead"));
            await ProgramManagementServices.SeedAsync(Services, new RolePermission(TenantId, leadRoleId, RbacPermissions.TechnologyInventoryManage), permission => permission.Assign());
            if (condition != "manage_only")
                await ProgramManagementServices.SeedAsync(Services, new RolePermission(TenantId, leadRoleId, RbacPermissions.TechnologyInventoryRestrictedRead), permission => permission.Assign());
            var author = ActorReference.ForMember(memberId, "Administrator");
            var assetIds = resourceType == TechnologyInventoryResourceTypes.InformationAsset
                ? new[] { ResourceId, OtherAssetId } : [OtherAssetId];
            foreach (var id in assetIds)
            {
                var recordClassification = id == ResourceId ? classification : "public";
                await ProgramManagementServices.SeedAsync(Services, new InformationAsset(TenantId, id), asset =>
                    asset.Record(new InformationAssetContent(id.ToString(), recordClassification,
                        "RET-1", UserId, null, "active"), author, Now));
            }
            var flowIds = resourceType == TechnologyInventoryResourceTypes.DataFlow
                ? new[] { ResourceId, OtherFlowId } : [OtherFlowId];
            foreach (var id in flowIds)
            {
                var recordClassification = id == ResourceId ? classification : "public";
                await ProgramManagementServices.SeedAsync(Services, new DataFlow(TenantId, id), flow =>
                    flow.Record(new DataFlowContent("system_instance", Uuid.CreateVersion4(),
                        "external_party", null, "Provider", [OtherAssetId], id.ToString(), true, true,
                        null, new DateOnly(2026, 1, 1), UserId, "active", recordClassification), author, Now));
            }
            var grantId = Uuid.CreateVersion4();
            var terms = new AccessGrantTerms(new AccessGrantPrincipal(AccessGrantPrincipalKind.Member,
                    memberId), leadRoleId, new AccessGrantScope(AccessGrantScopeKind.SharedResource,
                    ResourceId, resourceType), new AccessGrantSource("manual", "inventory-read"), author,
                Now.AddDays(-2), condition == "expired" ? Now.AddDays(-1) : null);
            await ProgramManagementServices.SeedAsync(Services, new AccessGrant(TenantId, grantId),
                grant => grant.Issue(terms, member.MembershipEpisodeId));
            if (condition == "revoked")
                await ProgramManagementServices.SeedAsync(Services, new AccessGrant(TenantId, grantId),
                    grant => grant.Revoke(author, Now.AddMinutes(-1)));

            await using var scope = Services.CreateAsyncScope();
            var permissions = scope.ServiceProvider.GetRequiredService<FitzPermissionAuthorizer>();
            var grants = scope.ServiceProvider.GetRequiredService<FitzAccessGrantDirectory>();
            var rolePermissions = scope.ServiceProvider.GetRequiredService<FitzRolePermissionDirectoryReader>();
            var inventory = scope.ServiceProvider.GetRequiredService<FitzTechnologyInventoryDirectory>();
            var tenantPattern = EventStreamPattern.ForPattern(TenantId.ToString());
            var events = new List<DomainEvent>();
            var cursor = EventCursor.Start;
            await foreach (var record in store.ReadAsync(tenantPattern, cursor, CancellationToken.None))
            {
                events.Add(record.Event);
                cursor = record.NextCursor;
            }
            await ProjectAsync(permissions, "PermissionProjection", tenantPattern,
                new ProjectionCheckpoint(cursor), events, permissions.ApplyAsync);
            await ProjectAsync(grants, AccessGrantProjectionKeys.Projector, tenantPattern,
                new ProjectionCheckpoint(cursor), events, grants.ApplyAsync);
            await ProjectAsync(rolePermissions, "RolePermissionDirectory", tenantPattern,
                new ProjectionCheckpoint(cursor), events, rolePermissions.ApplyAsync);
            var inventoryPattern = TechnologyInventoryStreams.TenantPattern(TenantId);
            var inventoryEvents = new List<DomainEvent>();
            var inventoryCursor = EventCursor.Start;
            await foreach (var record in store.ReadAsync(inventoryPattern, inventoryCursor, CancellationToken.None))
            {
                inventoryEvents.Add(record.Event);
                inventoryCursor = record.NextCursor;
            }
            await ProjectAsync(inventory, FitzTechnologyInventoryDirectory.ProjectorName,
                inventoryPattern, new ProjectionCheckpoint(inventoryCursor), inventoryEvents, inventory.ApplyAsync);
            if (condition == "pending_revocation")
                await ProgramManagementServices.SeedAsync(Services, new AccessGrant(TenantId, grantId),
                    grant => grant.Revoke(author, Now.AddMinutes(-1)));
        }

        static async Task ProjectAsync(IProjectionStore projection, string name,
            EventStreamPattern pattern, ProjectionCheckpoint checkpoint, IReadOnlyList<DomainEvent> events,
            Func<DomainEvent, CancellationToken, ValueTask> apply)
        {
            await using var batch = await projection.BeginAsync(new ProjectionBatchContext(
                new CheckpointIdentity(name, pattern), ProjectionCheckpoint.Start));
            foreach (var domainEvent in events)
                await apply(domainEvent, CancellationToken.None);
            await batch.CommitAsync(checkpoint);
        }

        public ValueTask DisposeAsync() => Services.DisposeAsync();
    }

    sealed class Memberships(Uuid tenantId, Uuid userId) : ITenantMembershipDirectoryReader
    {
        public ValueTask<TenantMembershipView?> GetAsync(string tenant, Uuid user,
            CancellationToken ct = default) => ValueTask.FromResult<TenantMembershipView?>(
            tenant == tenantId.ToString() && user == userId ? new TenantMembershipView(userId, tenantId) : null);

        public ValueTask<bool> IsMemberAsync(string tenant, Uuid user,
            CancellationToken ct = default) => ValueTask.FromResult(tenant == tenantId.ToString() && user == userId);

        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenant, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }

    sealed class NoTeams : ITeamMemberDirectoryReader
    {
        public ValueTask<Page<TeamMemberView>> ListAsync(Uuid tenantId, Uuid teamId, int? limit,
            string? cursor, string? search, bool descending, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<TeamMemberView>([], null));
    }

    sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
