using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.TechnologyInventory;
using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.TechnologyInventory;

public sealed class InventoryBoundaryReferenceReadTests
{
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");

    [Fact]
    public async Task ShouldListComponentReferencesGivenARecordedComponent()
    {
        // Arrange
        var scenario = CreateScenario();
        scenario.Directory.Page = new([Reference(scenario.TenantId, "component", scenario.ComponentId)], null);
        var handler = new ListTechnologyComponentBoundaryReferencesHandler(scenario.Reader,
            scenario.Directory, scenario.Consistency);

        // Act
        var result = await handler.HandleAsync(Context(new ListTechnologyComponentBoundaryReferences(
            scenario.TenantId, scenario.ComponentId)), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Equal(("component", scenario.ComponentId, 50), scenario.Directory.LastQuery);
    }

    [Fact]
    public async Task ShouldListAssetReferencesGivenARecordedAsset()
    {
        // Arrange
        var scenario = CreateScenario();
        scenario.Directory.Page = new([Reference(scenario.TenantId, "information", scenario.AssetId)], null);
        var handler = new ListInformationAssetBoundaryReferencesHandler(scenario.Reader,
            scenario.Directory, scenario.Consistency, Visibility());

        // Act
        var result = await handler.HandleAsync(Context(new ListInformationAssetBoundaryReferences(
            scenario.TenantId, scenario.AssetId, 10)), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(("information", scenario.AssetId, 10), scenario.Directory.LastQuery);
    }

    [Fact]
    public async Task ShouldHideRestrictedAssetReferencesGivenMemberWithoutRestrictedPermission()
    {
        // Arrange
        var scenario = CreateScenario("restricted");
        var handler = new ListInformationAssetBoundaryReferencesHandler(scenario.Reader,
            scenario.Directory, scenario.Consistency, Visibility());
        var userId = Uuid.CreateVersion4();
        var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "test"));

        // Act
        var result = await handler.HandleAsync(new RequestContext<ListInformationAssetBoundaryReferences>(
            new ListInformationAssetBoundaryReferences(scenario.TenantId, scenario.AssetId), actor),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error!.Kind);
        Assert.Equal(0, scenario.Directory.ListCalls);
    }

    [Fact]
    public async Task ShouldHideUnknownRecordsWithoutReadingTheIndexGivenNoSuchComponentOrAsset()
    {
        // Arrange
        var scenario = CreateScenario();
        var components = new ListTechnologyComponentBoundaryReferencesHandler(scenario.Reader,
            scenario.Directory, scenario.Consistency);
        var assets = new ListInformationAssetBoundaryReferencesHandler(scenario.Reader,
            scenario.Directory, scenario.Consistency, Visibility());

        // Act
        var component = await components.HandleAsync(Context(new ListTechnologyComponentBoundaryReferences(
            scenario.TenantId, Uuid.CreateVersion4())), CancellationToken.None);
        var asset = await assets.HandleAsync(Context(new ListInformationAssetBoundaryReferences(
            scenario.TenantId, Uuid.CreateVersion4())), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, component.Error!.Kind);
        Assert.Equal(RequestErrorKind.NotFound, asset.Error!.Kind);
        Assert.Equal(0, scenario.Directory.ListCalls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public async Task ShouldRejectOutOfRangeLimitGivenInventoryBoundaryReferenceLists(int limit)
    {
        // Arrange
        var scenario = CreateScenario();
        var handler = new ListTechnologyComponentBoundaryReferencesHandler(scenario.Reader,
            scenario.Directory, scenario.Consistency);

        // Act
        var result = await handler.HandleAsync(Context(new ListTechnologyComponentBoundaryReferences(
            scenario.TenantId, scenario.ComponentId, limit)), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.Equal(0, scenario.Directory.ListCalls);
    }

    [Fact]
    public async Task ShouldHideMismatchedRowsGivenAnotherTenantsReference()
    {
        // Arrange
        var scenario = CreateScenario();
        scenario.Directory.Page = new([Reference(Uuid.CreateVersion4(), "component", scenario.ComponentId)], null);
        var handler = new ListTechnologyComponentBoundaryReferencesHandler(scenario.Reader,
            scenario.Directory, scenario.Consistency);

        // Act
        var result = await handler.HandleAsync(Context(new ListTechnologyComponentBoundaryReferences(
            scenario.TenantId, scenario.ComponentId)), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error!.Kind);
    }

    [Fact]
    public async Task ShouldRejectInvalidCursorGivenInventoryBoundaryReferenceLists()
    {
        // Arrange
        var scenario = CreateScenario();
        scenario.Directory.RejectCursor = true;
        var handler = new ListInformationAssetBoundaryReferencesHandler(scenario.Reader,
            scenario.Directory, scenario.Consistency, Visibility());

        // Act
        var result = await handler.HandleAsync(Context(new ListInformationAssetBoundaryReferences(
            scenario.TenantId, scenario.AssetId, Cursor: "nope")), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
    }

    [Fact]
    public async Task ShouldDenyBoundaryReferenceReadsGivenMemberWithoutInventoryGrant()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "test"));
        var authorizer = new TechnologyInventoryAuthorizer(new Memberships(tenantId, userId),
            new ActiveTenants(), new DenyAll());

        // Act
        var component = await authorizer.AuthorizeAsync(new RequestContext<ITechnologyInventoryRequest>(
            new ListTechnologyComponentBoundaryReferences(tenantId, Uuid.CreateVersion4()), actor),
            CancellationToken.None);
        var asset = await authorizer.AuthorizeAsync(new RequestContext<ITechnologyInventoryRequest>(
            new ListInformationAssetBoundaryReferences(tenantId, Uuid.CreateVersion4()), actor),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, component.Error!.Kind);
        Assert.Equal(RequestErrorKind.Forbidden, asset.Error!.Kind);
    }

    static RequestContext<T> Context<T>(T request) where T : IRequestBase =>
        new(request, new ClaimsPrincipal());

    static TechnologyInventoryRestrictedVisibility Visibility() =>
        new(new DenyAll(), new DenyAllScopes());

    static Scenario CreateScenario(string assetClassification = "confidential")
    {
        var tenantId = Uuid.CreateVersion4();
        var component = new TechnologyComponent(tenantId, Uuid.CreateVersion4());
        Assert.True(component.Record(new TechnologyComponentContent("network", "Core VPC",
            Uuid.CreateVersion4(), null, null, null, null, null, "active"), Author,
            DateTimeOffset.UtcNow).IsSuccess);
        var asset = new InformationAsset(tenantId, Uuid.CreateVersion4());
        Assert.True(asset.Record(new InformationAssetContent("Customer PII", assetClassification,
            "RET-1", Uuid.CreateVersion4(), null, "active"), Author, DateTimeOffset.UtcNow).IsSuccess);
        var directory = new Directory();
        return new Scenario(tenantId, component.Id, asset.Id, directory,
            new SourceReader(component, asset),
            new ApplicationBoundaryReferenceReadConsistency(directory, new InMemoryEventStore()));
    }

    static ApplicationBoundaryReferenceView Reference(Uuid tenantId, string subjectType,
        Uuid governedRecordId) => new(tenantId, subjectType, governedRecordId,
        Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(),
        1, "draft", null, "inclusion", "Core VPC", "Operations", "In scope");

    sealed record Scenario(Uuid TenantId, Uuid ComponentId, Uuid AssetId, Directory Directory,
        SourceReader Reader, ApplicationBoundaryReferenceReadConsistency Consistency);

    sealed class Directory : IApplicationBoundaryReferenceDirectory
    {
        public bool RejectCursor { get; set; }
        public int ListCalls { get; private set; }
        public (string SubjectType, Uuid RecordId, int Limit) LastQuery { get; private set; }
        public Page<ApplicationBoundaryReferenceView> Page { get; set; } = new([], null);

        public ValueTask<Page<ApplicationBoundaryReferenceView>> ListAsync(Uuid tenantId,
            string subjectType, Uuid recordId, int limit, string? cursor,
            CancellationToken ct = default)
        {
            ListCalls++;
            LastQuery = (subjectType, recordId, limit);
            return RejectCursor
                ? ValueTask.FromException<Page<ApplicationBoundaryReferenceView>>(
                    new KvDirectoryQueryException())
                : ValueTask.FromResult(Page);
        }

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);
    }

    sealed class SourceReader(TechnologyComponent component, InformationAsset asset)
        : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult(aggregate.Id == component.Id ? (TAggregate)(Aggregate)component :
                aggregate.Id == asset.Id ? (TAggregate)(Aggregate)asset : aggregate);
    }

    sealed class Memberships(Uuid tenantId, Uuid userId) : ITenantMembershipDirectoryReader
    {
        public ValueTask<TenantMembershipView?> GetAsync(string tenant, Uuid user,
            CancellationToken ct = default) =>
            ValueTask.FromResult<TenantMembershipView?>(
                tenant == tenantId.ToString() && user == userId
                    ? new TenantMembershipView(userId, tenantId)
                    : null);

        public ValueTask<bool> IsMemberAsync(string tenant, Uuid user, CancellationToken ct = default) =>
            ValueTask.FromResult(tenant == tenantId.ToString() && user == userId);

        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenant, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }

    sealed class ActiveTenants : ITenantActivity
    {
        public ValueTask<bool> IsActiveAsync(Uuid tenantId, CancellationToken ct = default) =>
            ValueTask.FromResult(true);
    }

    sealed class DenyAll : IPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            string permission, CancellationToken ct = default) => ValueTask.FromResult(false);
    }

    sealed class DenyAllScopes : IAccessGrantScopePermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAtAnyScopeAsync(Uuid tenantId, Uuid userId,
            Uuid memberId, IReadOnlyCollection<AccessGrantScope> scopes, string permission,
            CancellationToken ct = default) => ValueTask.FromResult(false);

        public ValueTask<bool> IsAllowedAtAnyApplicationInventoryScopeAsync(Uuid tenantId,
            Uuid userId, Uuid memberId, string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(false);
    }
}
