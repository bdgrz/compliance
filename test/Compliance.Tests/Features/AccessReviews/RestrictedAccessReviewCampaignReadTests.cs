using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessReviews;

public sealed class RestrictedAccessReviewCampaignReadTests
{
    [Fact]
    public async Task ShouldHideCampaignGivenManagerWithoutRestrictedReadGrant()
    {
        // Arrange
        var scenario = Scenario();
        var handler = new GetAccessReviewCampaignHandler(scenario.Reader,
            new AccessReviewManagerPermissions(), TimeProvider.System,
            RestrictedApplicationVisibilityFixture.Create(scenario.Reader), null!);
        var context = new RequestContext<GetAccessReviewCampaign>(new GetAccessReviewCampaign(
            scenario.TenantId, scenario.CampaignId), Actor(scenario.UserId));

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound,
            Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Fact]
    public async Task ShouldReturnCampaignItemGivenExactSystemInstanceReadGrant()
    {
        // Arrange
        var scenario = Scenario();
        var visibility = RestrictedApplicationVisibilityFixture.Create(scenario.Reader,
            allowedScopes: [new AccessGrantScope(AccessGrantScopeKind.SystemInstance,
                scenario.InstanceId)]);
        var handler = new GetAccessReviewCampaignHandler(scenario.Reader,
            new AccessReviewManagerPermissions(), TimeProvider.System, visibility, null!);
        var context = new RequestContext<GetAccessReviewCampaign>(new GetAccessReviewCampaign(
            scenario.TenantId, scenario.CampaignId), Actor(scenario.UserId));

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(scenario.InstanceId, Assert.Single(result.Value.Items).Item.SystemInstanceId);
    }

    [Fact]
    public async Task ShouldOmitCampaignGivenAllItemsHaveRestrictedInstances()
    {
        // Arrange
        var scenario = Scenario();
        var summary = new AccessReviewCampaignSummaryView(scenario.TenantId,
            scenario.CampaignId, "Payroll review", DateTimeOffset.UtcNow.AddDays(1),
            AccessReviewCampaign.Active, 1, Uuid.CreateVersion4(), DateTimeOffset.UtcNow,
            null, null);
        var handler = new ListAccessReviewCampaignsHandler(
            new CampaignDirectory(summary), new InMemoryEventStore(), scenario.Reader,
            RestrictedApplicationVisibilityFixture.Create(scenario.Reader));
        var context = new RequestContext<ListAccessReviewCampaigns>(
            new ListAccessReviewCampaigns(scenario.TenantId), Actor(scenario.UserId));

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.Empty(result.Value.Items);
    }

    [Fact]
    public async Task ShouldRetainVisibleItemCountGivenExactSystemInstanceReadGrant()
    {
        // Arrange
        var scenario = Scenario();
        var summary = new AccessReviewCampaignSummaryView(scenario.TenantId,
            scenario.CampaignId, "Payroll review", DateTimeOffset.UtcNow.AddDays(1),
            AccessReviewCampaign.Active, 1, Uuid.CreateVersion4(), DateTimeOffset.UtcNow,
            null, null);
        var visibility = RestrictedApplicationVisibilityFixture.Create(scenario.Reader,
            allowedScopes: [new AccessGrantScope(AccessGrantScopeKind.SystemInstance,
                scenario.InstanceId)]);
        var handler = new ListAccessReviewCampaignsHandler(new CampaignDirectory(summary),
            new InMemoryEventStore(), scenario.Reader, visibility);
        var context = new RequestContext<ListAccessReviewCampaigns>(
            new ListAccessReviewCampaigns(scenario.TenantId), Actor(scenario.UserId));

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(1, Assert.Single(result.Value.Items).ItemCount);
    }

    [Fact]
    public async Task ShouldHideBulkPreviewGivenManagerWithoutRestrictedReadGrant()
    {
        // Arrange
        var scenario = Scenario();
        var handler = new PreviewBulkAccessDecisionHandler(scenario.Reader,
            RestrictedApplicationVisibilityFixture.Create(scenario.Reader), null!);
        var context = new RequestContext<PreviewBulkAccessDecision>(
            new PreviewBulkAccessDecision(scenario.TenantId, scenario.CampaignId,
                [scenario.ItemId], "keep"), Actor(scenario.UserId));

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound,
            Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Fact]
    public async Task ShouldHideAssignedDecisionGivenReviewerWithoutRestrictedReadGrant()
    {
        // Arrange
        var scenario = Scenario();
        var handler = new RecordAccessDecisionHandler(null!, scenario.Reader,
            TimeProvider.System, RestrictedApplicationVisibilityFixture.Create(scenario.Reader), null!);
        var context = PersonalAccessReviewTransportTests.HttpContext<RecordAccessDecision>(new RecordAccessDecision(
            scenario.TenantId, scenario.CampaignId, scenario.ItemId, 1, "keep",
            "Still required."), Actor(scenario.UserId));

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound,
            Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Fact]
    public async Task ShouldHideCampaignCompletionGivenManagerWithoutRestrictedReadGrant()
    {
        // Arrange
        var scenario = Scenario();
        var handler = new CompleteAccessReviewCampaignHandler(scenario.Reader, null!, null!,
            TimeProvider.System, RestrictedApplicationVisibilityFixture.Create(scenario.Reader));
        var context = PersonalAccessReviewTransportTests.HttpContext<CompleteAccessReviewCampaign>(
            new CompleteAccessReviewCampaign(scenario.TenantId, scenario.CampaignId, 1,
                "I reviewed the campaign."), Actor(scenario.UserId));

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound,
            Assert.IsType<RequestError>(result.Error).Kind);
    }

    static ScenarioData Scenario()
    {
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var memberId = RbacIds.Member(tenantId, userId);
        var applicationId = Uuid.CreateVersion4();
        var instanceId = Uuid.CreateVersion4();
        var populationId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        var itemId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var actor = ActorReference.ForMember(memberId, "Manager");
        var application = new DeclaredApplication(tenantId, applicationId);
        Assert.True(application.Declare("Payroll", "Run payroll", null, memberId, "Manager",
            now, isRestricted: true).IsSuccess);
        var instance = new DeclaredSystemInstance(tenantId, instanceId);
        Assert.True(instance.Declare(applicationId, "Production", "aws_account", null, null,
            memberId, "Manager", now).IsSuccess);
        var item = new AccessReviewItemView(itemId, populationId,
            Uuid.CreateVersion4(), instanceId, memberId, "subject", "user_account", "User",
            "human", null, null, "entitlement", "permission", "Read", false, "expected",
            [], null);
        var campaign = new AccessReviewCampaign(tenantId, campaignId);
        Assert.True(campaign.Launch("Payroll review", "Review payroll access",
            now.AddDays(7), Uuid.CreateVersion4(), new string('a', 64),
            [new AccessReviewerView(populationId, instanceId, memberId, false, null)], [item],
            actor, now).IsSuccess);
        var reader = new CampaignAggregateReader(application, instance, campaign);
        return new ScenarioData(tenantId, userId, applicationId, instanceId, populationId,
            campaignId, itemId, reader);
    }

    static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "BdgrzSession"));

    sealed record ScenarioData(Uuid TenantId, Uuid UserId, Uuid ApplicationId,
        Uuid InstanceId, Uuid PopulationId, Uuid CampaignId, Uuid ItemId,
        CampaignAggregateReader Reader);

    sealed class CampaignAggregateReader(DeclaredApplication application,
        DeclaredSystemInstance instance, AccessReviewCampaign campaign) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult(aggregate switch
            {
                DeclaredApplication => (TAggregate)(Aggregate)application,
                DeclaredSystemInstance => (TAggregate)(Aggregate)instance,
                AccessReviewCampaign => (TAggregate)(Aggregate)campaign,
                _ => aggregate,
            });
    }

    sealed class AccessReviewManagerPermissions : IPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(permission == RbacPermissions.AccessReviewManage);
    }

    sealed class CampaignDirectory(AccessReviewCampaignSummaryView item)
        : IAccessReviewCampaignDirectoryReader
    {
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);

        public ValueTask<Page<AccessReviewCampaignSummaryView>> ListAsync(Uuid tenantId,
            int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<AccessReviewCampaignSummaryView>([item], null));
    }
}
