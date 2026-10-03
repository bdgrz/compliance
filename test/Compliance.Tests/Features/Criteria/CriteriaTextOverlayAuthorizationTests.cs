using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Criteria;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Criteria;

public sealed class CriteriaTextOverlayAuthorizationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRequireOrganizationWideProgramGrantGivenOverlayWrite(bool allowed)
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var permissions = new RecordingAccessGrantPermissionAuthorizer(allowed,
            Uuid.CreateVersion4());
        var authorizer = new ProgramManagementAuthorizer(new FixedMembershipDirectory(true),
            new ActiveTenant(), permissions, ProgramManagementServices.ResourceScopes());
        var context = new RequestContext<IProgramManagementRequest>(
            new SetCriteriaTextOverlay(tenantId, Uuid.CreateVersion4(), "CC6.1", null,
                new CriteriaTextOverlayContent("Synthetic text", "Supplier", "License",
                    new CriteriaOverlayUsageFlags(true, true))),
            ProgramManagementServices.Actor(userId));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(allowed, result.IsSuccess);
        Assert.Equal(allowed ? null : RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal(RbacPermissions.ProgramManage, permissions.Permission);
        Assert.Equal(tenantId, permissions.TenantId);
        Assert.Equal(userId, permissions.UserId);
        Assert.Equal(RbacIds.Member(tenantId, userId), permissions.MemberId);
    }

    sealed class RecordingAccessGrantPermissionAuthorizer(bool organizationWide,
        Uuid visibleProgramId) : IAccessGrantPermissionAuthorizer
    {
        public string? Permission { get; private set; }
        public Uuid TenantId { get; private set; }
        public Uuid UserId { get; private set; }
        public Uuid MemberId { get; private set; }

        public ValueTask<ProgramAccessVisibility> GetProgramVisibilityAsync(Uuid tenantId,
            Uuid userId, Uuid memberId, string permission, CancellationToken ct = default)
        {
            TenantId = tenantId;
            UserId = userId;
            MemberId = memberId;
            Permission = permission;
            return ValueTask.FromResult(new ProgramAccessVisibility(organizationWide,
                new HashSet<Uuid> { visibleProgramId }));
        }

        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            Uuid programId, string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(false);
    }
}
