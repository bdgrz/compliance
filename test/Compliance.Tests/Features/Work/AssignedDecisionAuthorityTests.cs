using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class AssignedDecisionAuthorityTests
{
    [Fact]
    public async Task ShouldExcludeBoundaryDecisionGivenAssignedReviewerLacksSourceManagementGrant()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var boundaryId = Uuid.CreateVersion4();
        var versionId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var content = new BoundaryContent("System boundary", "readiness", ["security"], []);
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new SystemBoundary(fixture.TenantId, boundaryId), boundary =>
            {
                Assert.True(boundary.Create(fixture.ProgramId, versionId, content,
                    fixture.LeadMemberId, "Lead", now.AddMinutes(-5)).IsSuccess);
                Assert.Null(boundary.AssignResponsibility(new ResponsibilityScope("boundary",
                        boundaryId, versionId, 1), Uuid.CreateVersion4(), fixture.ReviewerMemberId,
                    ResponsibilityType.AssignedReviewer, fixture.LeadMemberId, "Lead",
                    now.AddMinutes(-4), now.AddMinutes(-4), null, []));
                return Result.Success;
            });
        fixture.Boundaries.Add(new BoundaryView(fixture.TenantId, boundaryId, fixture.ProgramId,
            new BoundaryVersionView(fixture.TenantId, boundaryId, fixture.ProgramId,
                versionId, 1, content, "draft", null, fixture.LeadMemberId, "Lead",
                now.AddMinutes(-5)), null, null, 1));
        var authorizer = new ProgramManagementAuthorizer(
            fixture.Provider.GetRequiredService<ITenantMembershipDirectoryReader>(),
            fixture.Provider.GetRequiredService<ITenantActivity>(),
            fixture.Provider.GetRequiredService<IAccessGrantPermissionAuthorizer>(),
            ProgramManagementServices.ResourceScopes(fixture.ProgramId));
        var context = new RequestContext<IProgramManagementRequest>(new ReviewBoundary(
                fixture.TenantId, boundaryId, versionId, 1, "accept", "Reviewed."),
            ProgramManagementServices.Actor(fixture.ReviewerUserId));

        // Act
        var source = await authorizer.AuthorizeAsync(context, CancellationToken.None);
        var reviewer = await fixture.AsAsync(fixture.ReviewerUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId));
        var oversight = await fixture.AsAsync(fixture.LeadUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "all"));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, source.Error?.Kind);
        Assert.Empty(reviewer.Items);
        Assert.Equal(new WorkCountsView(0, 0, 0, 0), reviewer.Counts);
        Assert.Null(Assert.Single(oversight.Items).AssigneeMemberId);

        // Act: a current grant permits the same named source duty.
        fixture.Permissions.Managers.Add(fixture.ReviewerMemberId);
        var grantedSource = await authorizer.AuthorizeAsync(context, CancellationToken.None);
        var granted = await fixture.AsAsync(fixture.ReviewerUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId));

        // Assert
        Assert.True(grantedSource.IsSuccess);
        var item = Assert.Single(granted.Items);
        Assert.Equal(fixture.ReviewerMemberId, item.AssigneeMemberId);

        // Act: revocation removes action eligibility without erasing the duty.
        fixture.Permissions.Managers.Remove(fixture.ReviewerMemberId);
        var revokedSource = await authorizer.AuthorizeAsync(context, CancellationToken.None);
        var revoked = await fixture.AsAsync(fixture.ReviewerUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId));
        fixture.Permissions.Managers.Add(fixture.ReviewerMemberId);
        var restored = await fixture.AsAsync(fixture.ReviewerUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, revokedSource.Error?.Kind);
        Assert.Empty(revoked.Items);
        Assert.Equal(item.WorkItemId, Assert.Single(restored.Items).WorkItemId);

    }
}
