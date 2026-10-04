using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.TechnologyInventory;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.TechnologyInventory;

public sealed class TechnologyInventoryRestrictedReadHandlerTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid UserId = Uuid.CreateVersion4();
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");

    [Fact]
    public async Task ShouldHideRestrictedAssetGivenDirectReadWithoutRestrictedPermission()
    {
        // Arrange
        var asset = Asset("restricted");
        var directory = new InventoryDirectory([asset]);
        var handler = new GetInformationAssetHandler(new AggregateReader(AssetAggregate(asset)),
            Consistency(directory, AssetAggregate(asset)), Visibility());

        // Act
        var result = await handler.HandleAsync(Context(new GetInformationAsset(TenantId,
            asset.InformationAssetId)), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error!.Kind);
    }

    [Fact]
    public async Task ShouldKeepRestrictedAssetNotFoundGivenProjectionBehindClassificationChange()
    {
        // Arrange
        var id = Uuid.CreateVersion4();
        var source = new InformationAsset(TenantId, id);
        Assert.True(source.Record(Content("public"), Author, DateTimeOffset.UtcNow).IsSuccess);
        Assert.Null(source.Revise(1, current => current with { Classification = "restricted" },
            Author, DateTimeOffset.UtcNow));
        var staleProjection = AssetView(id, 1, Content("public"));
        var directory = new InventoryDirectory([staleProjection]);
        var handler = new GetInformationAssetHandler(new AggregateReader(source),
            Consistency(directory, source), Visibility());

        // Act
        var result = await handler.HandleAsync(Context(new GetInformationAsset(TenantId, id)),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error!.Kind);
    }

    [Fact]
    public async Task ShouldFilterRestrictedAssetsBeforeFillingListPageGivenNoScopedGrant()
    {
        // Arrange
        var restricted = Asset("restricted");
        var publicAsset = Asset("public");
        var confidential = Asset("confidential");
        var directory = new InventoryDirectory([restricted, publicAsset, confidential]);
        var handler = new ListInformationAssetsHandler(directory,
            Consistency(directory), Visibility());

        // Act
        var result = await handler.HandleAsync(Context(new ListInformationAssets(TenantId, 2)),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal([publicAsset.InformationAssetId, confidential.InformationAssetId],
            result.Value.Items.Select(item => item.InformationAssetId));
        Assert.Equal([(2, (string?)null), (1, "2")], directory.AssetListCalls);
    }

    [Fact]
    public async Task ShouldRejectFilteredPageGivenSourceEventArrivesDuringRead()
    {
        // Arrange
        var visible = Asset("public");
        var events = new InMemoryEventStore();
        var incomingFlowId = Uuid.CreateVersion4();
        var arriving = DomainEventSeed.Attach(new DataFlowRevisionRecorded(TenantId,
            incomingFlowId, 1, new DataFlowContent("technology_component", Uuid.CreateVersion4(),
                "external_party", null, "Payroll provider", [Uuid.CreateVersion4()], "Payroll", true,
                true, null, new DateOnly(2026, 1, 1), Uuid.CreateVersion4(), "active", "restricted"),
            Author, DateTimeOffset.UtcNow), incomingFlowId, 1);
        var directory = new InventoryDirectory([visible])
        {
            OnAssetListAsync = ct => events.AppendAsync(new EventStreamAddress(TenantId.ToString(),
                "technology-inventory", incomingFlowId.ToString()), 0, [arriving], ct),
        };
        var handler = new ListInformationAssetsHandler(directory,
            new TechnologyInventoryReadConsistency(directory, new AggregateReader(), events),
            Visibility());

        // Act
        var result = await handler.HandleAsync(Context(new ListInformationAssets(TenantId, 1)),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.True(result.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldHideRestrictedDataFlowGivenDirectReadWithoutRestrictedPermission()
    {
        // Arrange
        var flow = Flow("restricted");
        var directory = new InventoryDirectory(flows: [flow]);
        var handler = new GetDataFlowHandler(new AggregateReader(FlowAggregate(flow)),
            Consistency(directory, FlowAggregate(flow)), Visibility());

        // Act
        var result = await handler.HandleAsync(Context(new GetDataFlow(TenantId, flow.DataFlowId)),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error!.Kind);
    }

    [Fact]
    public async Task ShouldFilterRestrictedDataFlowsBeforeFillingListPageGivenNoScopedGrant()
    {
        // Arrange
        var restricted = Flow("restricted");
        var publicFlow = Flow("public");
        var directory = new InventoryDirectory(flows: [restricted, publicFlow]);
        var handler = new ListDataFlowsHandler(directory, Consistency(directory), Visibility());

        // Act
        var result = await handler.HandleAsync(Context(new ListDataFlows(TenantId, 1)),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(publicFlow.DataFlowId, Assert.Single(result.Value.Items).DataFlowId);
        Assert.Equal([(1, (string?)null), (1, "1")], directory.FlowListCalls);
    }

    [Fact]
    public async Task ShouldFilterRestrictedHistoryRowsGivenCurrentAssetIsPublic()
    {
        // Arrange
        var aggregate = new InformationAsset(TenantId, Uuid.CreateVersion4());
        Assert.True(aggregate.Record(Content("restricted"), Author, DateTimeOffset.UtcNow).IsSuccess);
        Assert.Null(aggregate.Revise(1, current => current with { Classification = "public" },
            Author, DateTimeOffset.UtcNow));
        var restrictedRevision = AssetView(aggregate.Id, 1, Content("restricted"));
        var currentRevision = AssetView(aggregate.Id, 2, Content("public"));
        var directory = new InventoryDirectory([currentRevision],
            assetRevisions: [restrictedRevision, currentRevision]);
        var handler = new ListInformationAssetRevisionsHandler(new AggregateReader(aggregate), directory,
            Consistency(directory, aggregate), Visibility());

        // Act
        var result = await handler.HandleAsync(Context(new ListInformationAssetRevisions(
            TenantId, aggregate.Id, 1)), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, Assert.Single(result.Value.Items).Revision);
        Assert.Equal([(1, (string?)null), (1, "1")], directory.AssetRevisionListCalls);
    }

    [Fact]
    public async Task ShouldValidateMinimumRevisionBeforeHidingRestrictedAssetGivenNonPositiveValue()
    {
        // Arrange
        var asset = Asset("restricted");
        var aggregate = AssetAggregate(asset);
        var directory = new InventoryDirectory([asset]);
        var handler = new ListInformationAssetRevisionsHandler(new AggregateReader(aggregate),
            directory, Consistency(directory, aggregate), Visibility());

        // Act
        var result = await handler.HandleAsync(Context(new ListInformationAssetRevisions(TenantId,
            asset.InformationAssetId, MinimumRevision: 0)), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
    }

    [Fact]
    public async Task ShouldValidateMinimumRevisionBeforeHidingRestrictedFlowGivenNonPositiveValue()
    {
        // Arrange
        var flow = Flow("restricted");
        var aggregate = FlowAggregate(flow);
        var directory = new InventoryDirectory(flows: [flow]);
        var handler = new ListDataFlowRevisionsHandler(new AggregateReader(aggregate), directory,
            Consistency(directory, aggregate), Visibility());

        // Act
        var result = await handler.HandleAsync(Context(new ListDataFlowRevisions(TenantId,
            flow.DataFlowId, MinimumRevision: 0)), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
    }

    [Fact]
    public async Task ShouldNotScanFlowsGivenPreviewOfHiddenRestrictedAsset()
    {
        // Arrange
        var asset = Asset("restricted");
        var directory = new InventoryDirectory([asset]);
        var handler = new PreviewInformationAssetChangeHandler(new AggregateReader(AssetAggregate(asset)),
            directory,
            Consistency(directory, AssetAggregate(asset)), Visibility());

        // Act
        var result = await handler.HandleAsync(Context(new PreviewInformationAssetChange(
            TenantId, asset.InformationAssetId, asset.Revision)), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error!.Kind);
        Assert.Empty(directory.FlowListCalls);
    }

    static TechnologyInventoryReadConsistency Consistency(InventoryDirectory directory,
        params Aggregate[] aggregates) => new(directory, new AggregateReader(aggregates),
        new InMemoryEventStore());

    static TechnologyInventoryRestrictedVisibility Visibility() =>
        new(new DenyPermission(), new DenyScopedPermission());

    static RequestContext<T> Context<T>(T request) where T : IRequestBase =>
        new(request, new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", UserId.ToString())], "test")));

    static InformationAssetView Asset(string classification) =>
        AssetView(Uuid.CreateVersion4(), 1, Content(classification));

    static InformationAssetView AssetView(Uuid id, long revision,
        InformationAssetContent content) => new(TenantId, id, revision, content, "manual", Author,
        DateTimeOffset.UtcNow);

    static InformationAssetContent Content(string classification) =>
        new("Customer data", classification, "RET-1", Uuid.CreateVersion4(), null, "active");

    static InformationAsset AssetAggregate(InformationAssetView view)
    {
        var asset = new InformationAsset(TenantId, view.InformationAssetId);
        Assert.True(asset.Record(view.Content, Author, DateTimeOffset.UtcNow).IsSuccess);
        return asset;
    }

    static DataFlowView Flow(string classification)
    {
        var id = Uuid.CreateVersion4();
        return new DataFlowView(TenantId, id, 1, new DataFlowContent("technology_component",
            Uuid.CreateVersion4(), "external_party", null, "Payroll provider", [Uuid.CreateVersion4()],
            "Payroll processing", true, true, null, new DateOnly(2026, 1, 1),
            Uuid.CreateVersion4(), "active", classification), "manual", Author, DateTimeOffset.UtcNow);
    }

    static DataFlow FlowAggregate(DataFlowView view)
    {
        var flow = new DataFlow(TenantId, view.DataFlowId);
        Assert.True(flow.Record(view.Content, Author, DateTimeOffset.UtcNow).IsSuccess);
        return flow;
    }

    sealed class AggregateReader(params Aggregate[] aggregates) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            var source = aggregates.FirstOrDefault(item => item.GetType() == aggregate.GetType() &&
                item.Stream == aggregate.Stream);
            return ValueTask.FromResult(source is null ? aggregate : (TAggregate)source);
        }
    }

    sealed class InventoryDirectory(InformationAssetView[]? assets = null,
        DataFlowView[]? flows = null, InformationAssetView[]? assetRevisions = null)
        : ITechnologyInventoryReader
    {
        readonly InformationAssetView[] _assets = assets ?? [];
        readonly DataFlowView[] _flows = flows ?? [];
        readonly InformationAssetView[] _assetRevisions = assetRevisions ?? [];

        public List<(int Limit, string? Cursor)> AssetListCalls { get; } = [];
        public List<(int Limit, string? Cursor)> FlowListCalls { get; } = [];
        public List<(int Limit, string? Cursor)> AssetRevisionListCalls { get; } = [];
        public Func<CancellationToken, ValueTask>? OnAssetListAsync { get; init; }

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);

        public ValueTask<TechnologyComponentView?> GetComponentAsync(Uuid tenantId, Uuid componentId,
            CancellationToken ct = default) => ValueTask.FromResult<TechnologyComponentView?>(null);

        public ValueTask<Page<TechnologyComponentView>> ListComponentsAsync(Uuid tenantId, int limit,
            string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<TechnologyComponentView>([], null));

        public ValueTask<Page<TechnologyComponentView>> ListComponentRevisionsAsync(Uuid tenantId,
            Uuid componentId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<TechnologyComponentView>([], null));

        public ValueTask<InformationAssetView?> GetAssetAsync(Uuid tenantId, Uuid assetId,
            CancellationToken ct = default) => ValueTask.FromResult(_assets.SingleOrDefault(item =>
            item.TenantId == tenantId && item.InformationAssetId == assetId));

        public async ValueTask<Page<InformationAssetView>> ListAssetsAsync(Uuid tenantId, int limit,
            string? cursor, CancellationToken ct = default)
        {
            AssetListCalls.Add((limit, cursor));
            var page = Page(_assets, limit, cursor);
            if (OnAssetListAsync is not null)
                await OnAssetListAsync(ct).ConfigureAwait(false);
            return page;
        }

        public ValueTask<Page<InformationAssetView>> ListAssetRevisionsAsync(Uuid tenantId,
            Uuid assetId, int limit, string? cursor, CancellationToken ct = default)
        {
            AssetRevisionListCalls.Add((limit, cursor));
            return ValueTask.FromResult(Page(_assetRevisions, limit, cursor));
        }

        public ValueTask<DataFlowView?> GetFlowAsync(Uuid tenantId, Uuid flowId,
            CancellationToken ct = default) => ValueTask.FromResult(_flows.SingleOrDefault(item =>
            item.TenantId == tenantId && item.DataFlowId == flowId));

        public ValueTask<Page<DataFlowView>> ListFlowsAsync(Uuid tenantId, int limit,
            string? cursor, CancellationToken ct = default)
        {
            FlowListCalls.Add((limit, cursor));
            return ValueTask.FromResult(Page(_flows, limit, cursor));
        }

        public ValueTask<Page<DataFlowView>> ListFlowRevisionsAsync(Uuid tenantId, Uuid flowId,
            int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<DataFlowView>([], null));

        static Page<T> Page<T>(IReadOnlyList<T> items, int limit, string? cursor)
        {
            var offset = cursor is null ? 0 : int.Parse(cursor, System.Globalization.CultureInfo.InvariantCulture);
            var pageItems = items.Skip(offset).Take(limit).ToArray();
            var nextOffset = offset + pageItems.Length;
            return new Page<T>(pageItems,
                nextOffset < items.Count ? nextOffset.ToString(System.Globalization.CultureInfo.InvariantCulture) : null);
        }
    }

    sealed class DenyPermission : IPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            string permission, CancellationToken ct = default) => ValueTask.FromResult(false);
    }

    sealed class DenyScopedPermission : IAccessGrantScopePermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAtAnyScopeAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            IReadOnlyCollection<AccessGrantScope> scopes, string permission,
            CancellationToken ct = default) => ValueTask.FromResult(false);

        public ValueTask<bool> IsAllowedAtAnyApplicationInventoryScopeAsync(Uuid tenantId,
            Uuid userId, Uuid memberId, string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(false);
    }
}
