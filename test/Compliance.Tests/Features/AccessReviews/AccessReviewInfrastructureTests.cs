using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessReviews;

public sealed class AccessReviewInfrastructureTests
{
    static readonly ActorReference Actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Actor");

    [Fact]
    public async Task ShouldProjectPopulationStatusIdempotentlyGivenReplayedEvents()
    {
        // Arrange
        var directory = new FitzAccessPopulationDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var instanceId = Uuid.CreateVersion4();
        var populationId = Uuid.CreateVersion4();
        var opened = new AccessPopulationOpened(tenantId, populationId, Uuid.CreateVersion4(),
            instanceId, 1, DateTimeOffset.UtcNow.AddDays(-1), "manual_attestation", "Export", Actor,
            DateTimeOffset.UtcNow);
        var recorded = new AccessPopulationFactsRecorded(tenantId, populationId, 2,
            new AccessPopulationFacts([], [], [], []), Actor, DateTimeOffset.UtcNow);
        var accepted = new AccessPopulationAccepted(tenantId, populationId, 3, Uuid.CreateVersion4(),
            new string('a', 64), Uuid.CreateVersion4(), "Attested.", Actor, DateTimeOffset.UtcNow);

        // Act
        await ProjectAsync(directory, tenantId, opened, recorded, accepted, opened, recorded, accepted);
        var page = await directory.ListAsync(tenantId, instanceId, 10, null);
        var otherTenant = await directory.ListAsync(Uuid.CreateVersion4(), instanceId, 10, null);

        // Assert
        var summary = Assert.Single(page.Items);
        Assert.Equal("accepted", summary.Status);
        Assert.Equal(3, summary.Revision);
        Assert.Equal(accepted.SnapshotId, summary.SnapshotId);
        Assert.Empty(otherTenant.Items);
    }

    [Fact]
    public async Task ShouldRejectOutOfOrderProjectionGivenChangeBeforeOpen()
    {
        // Arrange
        var directory = new FitzAccessPopulationDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var recorded = new AccessPopulationFactsRecorded(tenantId, Uuid.CreateVersion4(), 2,
            new AccessPopulationFacts([], [], [], []), Actor, DateTimeOffset.UtcNow);

        // Act
        var failure = await Record.ExceptionAsync(() => ProjectAsync(directory, tenantId, recorded));

        // Assert
        Assert.IsType<InvalidOperationException>(failure);
    }

    [Fact]
    public async Task ShouldAllowParticipantButRequirePermissionGivenManagementRequest()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var permissions = new RecordingPermissionAuthorizer(false);
        var authorizer = new AccessReviewAuthorizer(new FixedMembershipDirectory(true),
            new ActiveTenant(), permissions);
        var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())], "BdgrzSession"));

        // Act
        var participant = await authorizer.AuthorizeAsync(new RequestContext<IAccessReviewRequest>(
            new GetAccessReviewCampaign(tenantId, Uuid.CreateVersion4()), actor), CancellationToken.None);
        var management = await authorizer.AuthorizeAsync(new RequestContext<IAccessReviewRequest>(
            new LaunchAccessReviewCampaign(tenantId, "Review", "Do it", DateTimeOffset.UtcNow.AddDays(1), []),
            actor), CancellationToken.None);
        var firmStaff = await new AccessReviewAuthorizer(new FixedMembershipDirectory(true, "firm_staff"),
                new ActiveTenant(), permissions)
            .AuthorizeAsync(new RequestContext<IAccessReviewRequest>(
                new GetAccessReviewCampaign(tenantId, Uuid.CreateVersion4()), actor), CancellationToken.None);
        var deprovisioned = await new AccessReviewAuthorizer(new FixedMembershipDirectory(true,
                isDeprovisioned: true), new ActiveTenant(), permissions)
            .AuthorizeAsync(new RequestContext<IAccessReviewRequest>(
                new GetAccessReviewCampaign(tenantId, Uuid.CreateVersion4()), actor), CancellationToken.None);

        // Assert
        Assert.True(participant.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, Assert.IsType<RequestError>(management.Error).Kind);
        Assert.Equal(RbacPermissions.AccessReviewManage, Assert.Single(permissions.Permissions));
        Assert.Equal(RequestErrorKind.Forbidden, Assert.IsType<RequestError>(firmStaff.Error).Kind);
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(deprovisioned.Error).Kind);
    }

    [Fact]
    public async Task ShouldGrantAccessReviewPermissionGivenTenantRegistration()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario().Given(
            new TenantRegistered(tenantId, Uuid.CreateVersion4(), "Acme", "acme"));

        // Act
        await scenario.RunAsync(new AccessReviewGrantBackfillReactor(
            new InMemoryProjectionCheckpointStore(), scenario.Requests));

        // Assert
        Assert.Equal(2, scenario.SentRequests.Count);
        Assert.All(scenario.SentRequests, request => Assert.Equal(RbacPermissions.AccessReviewManage,
            Assert.IsType<AssignRolePermission>(request).Permission));
    }

    static async Task ProjectAsync(FitzAccessPopulationDirectory directory, Uuid tenantId,
        params DomainEvent[] events)
    {
        var identity = new CheckpointIdentity("AccessPopulationDirectoryV2",
            EventStreamPattern.ForPattern(tenantId.ToString(), "access-populations"));
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            ProjectionCheckpoint.Start));
        foreach (var ev in events)
            await directory.ApplyAsync(ev);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }
}
