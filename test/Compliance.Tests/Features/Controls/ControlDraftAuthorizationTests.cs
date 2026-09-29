using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Controls;

public sealed class ControlDraftAuthorizationTests
{
    [Fact]
    public async Task ShouldUseProgramGrantInsteadOfTenantPermissionGivenScopedRequest()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var permissions = new RecordingPermissionAuthorizer(allowed: true);
        var scoped = new RecordingAccessGrantPermissionAuthorizer(allowed: false);
        var authorizer = new ProgramManagementAuthorizer(new FixedMembershipDirectory(true),
            new ActiveTenant(), scoped, ProgramManagementServices.ResourceScopes());
        var context = new RequestContext<IProgramManagementRequest>(
            new GetControlDraft(tenantId, programId, Uuid.CreateVersion4()),
            ProgramManagementServices.Actor(Uuid.CreateVersion4()));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(permissions.Permissions);
        Assert.Equal(programId, scoped.ProgramId);
        Assert.Equal(RbacPermissions.ProgramManage, scoped.Permission);
    }

    [Fact]
    public async Task ShouldUseProgramReadGrantGivenProgramReadRequest()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var scoped = new RecordingAccessGrantPermissionAuthorizer(allowed: true);
        var authorizer = new ProgramManagementAuthorizer(new FixedMembershipDirectory(true),
            new ActiveTenant(), scoped,
            ProgramManagementServices.ResourceScopes());
        var context = new RequestContext<IProgramManagementRequest>(new GetProgram(tenantId, programId),
            ProgramManagementServices.Actor(Uuid.CreateVersion4()));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(programId, scoped.ProgramId);
        Assert.Equal("tenant.access", scoped.Permission);
    }

    [Fact]
    public async Task ShouldHideMissingProgramGivenExplicitProgramReadRequest()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var scoped = new RecordingAccessGrantPermissionAuthorizer(allowed: true);
        var authorizer = new ProgramManagementAuthorizer(new FixedMembershipDirectory(true),
            new ActiveTenant(), scoped, new MissingProgramScopeResolver());
        var context = new RequestContext<IProgramManagementRequest>(
            new GetProgram(tenantId, Uuid.CreateVersion4()),
            ProgramManagementServices.Actor(Uuid.CreateVersion4()));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Fact]
    public async Task ShouldUseProgramReadGrantGivenProgramClientServiceList()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var scoped = new RecordingAccessGrantPermissionAuthorizer(allowed: true);
        var authorizer = new ProgramManagementAuthorizer(new FixedMembershipDirectory(true),
            new ActiveTenant(), scoped,
            ProgramManagementServices.ResourceScopes());
        var context = new RequestContext<IProgramManagementRequest>(
            new ListProgramClientServices(tenantId, programId),
            ProgramManagementServices.Actor(Uuid.CreateVersion4()));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(programId, scoped.ProgramId);
        Assert.Equal("tenant.access", scoped.Permission);
    }

    [Fact]
    public async Task ShouldAllowProgramDiscoveryGivenScopedReadGrant()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var permissions = new RecordingPermissionAuthorizer(allowed: false);
        var scoped = new RecordingAccessGrantPermissionAuthorizer(allowed: true,
            allowedProgramId: programId);
        var authorizer = new ProgramManagementAuthorizer(new FixedMembershipDirectory(true),
            new ActiveTenant(), scoped, ProgramManagementServices.ResourceScopes());
        var context = new RequestContext<IProgramManagementRequest>(new ListPrograms(tenantId),
            ProgramManagementServices.Actor(Uuid.CreateVersion4()));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains(programId, scoped.ProgramIds);
        Assert.Empty(permissions.Permissions);
    }

    [Fact]
    public async Task ShouldAllowClientServiceDiscoveryGivenScopedReadGrant()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var permissions = new RecordingPermissionAuthorizer(allowed: false);
        var scoped = new RecordingAccessGrantPermissionAuthorizer(allowed: true,
            allowedProgramId: programId);
        var authorizer = new ProgramManagementAuthorizer(new FixedMembershipDirectory(true),
            new ActiveTenant(), scoped, ProgramManagementServices.ResourceScopes());
        var context = new RequestContext<IProgramManagementRequest>(new ListClientServices(tenantId),
            ProgramManagementServices.Actor(Uuid.CreateVersion4()));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains(programId, scoped.ProgramIds);
        Assert.Equal("tenant.access", scoped.Permission);
        Assert.Empty(permissions.Permissions);
    }

    [Fact]
    public async Task ShouldRequireOrganizationWideGrantGivenProgramCreation()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var permissions = new RecordingPermissionAuthorizer(allowed: false);
        var scoped = new RecordingAccessGrantPermissionAuthorizer(allowed: true,
            organizationWide: true);
        var authorizer = new ProgramManagementAuthorizer(new FixedMembershipDirectory(true),
            new ActiveTenant(), scoped, ProgramManagementServices.ResourceScopes());
        var context = new RequestContext<IProgramManagementRequest>(
            new CreateProgram(tenantId, "Readiness", new ProgramPlan(null, null, null, null, null, null)),
            ProgramManagementServices.Actor(Uuid.CreateVersion4()));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(RbacPermissions.ProgramManage, scoped.Permission);
        Assert.True(scoped.OrganizationWide);
        Assert.Empty(permissions.Permissions);
    }

    [Fact]
    public async Task ShouldRequireOrganizationWideReadGivenCrossProgramApplicationPreview()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var scoped = new RecordingAccessGrantPermissionAuthorizer(allowed: true,
            allowedProgramId: Uuid.CreateVersion4());
        var authorizer = new ProgramManagementAuthorizer(new FixedMembershipDirectory(true),
            new ActiveTenant(), scoped, ProgramManagementServices.ResourceScopes());
        var context = new RequestContext<IProgramManagementRequest>(
            new PreviewApplicationChange(tenantId, Uuid.CreateVersion4(), 1, "retire"),
            ProgramManagementServices.Actor(Uuid.CreateVersion4()));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Equal("tenant.access", scoped.Permission);
        Assert.False(scoped.OrganizationWide);
    }

    [Fact]
    public async Task ShouldAllowApplicationPreviewGivenOrganizationWideReadGrant()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var scoped = new RecordingAccessGrantPermissionAuthorizer(allowed: true,
            organizationWide: true);
        var authorizer = new ProgramManagementAuthorizer(new FixedMembershipDirectory(true),
            new ActiveTenant(), scoped, ProgramManagementServices.ResourceScopes());
        var context = new RequestContext<IProgramManagementRequest>(
            new PreviewApplicationChange(tenantId, Uuid.CreateVersion4(), 1, "retire"),
            ProgramManagementServices.Actor(Uuid.CreateVersion4()));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("tenant.access", scoped.Permission);
        Assert.True(scoped.OrganizationWide);
    }

    [Fact]
    public async Task ShouldAuthorizeBoundaryReadGivenOwningProgramGrant()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var permissions = new RecordingPermissionAuthorizer(allowed: false);
        var scoped = new RecordingAccessGrantPermissionAuthorizer(allowed: true);
        var authorizer = new ProgramManagementAuthorizer(new FixedMembershipDirectory(true),
            new ActiveTenant(), scoped,
            ProgramManagementServices.ResourceScopes(programId));
        var context = new RequestContext<IProgramManagementRequest>(
            new GetBoundary(tenantId, boundaryId), ProgramManagementServices.Actor(Uuid.CreateVersion4()));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(programId, scoped.ProgramId);
        Assert.Equal("tenant.access", scoped.Permission);
        Assert.Empty(permissions.Permissions);
    }

    [Fact]
    public async Task ShouldDenyDraftReadGivenTenantMemberWithoutProgramManage()
    {
        // Arrange
        var permissions = new RecordingPermissionAuthorizer(allowed: false);
        await using var provider = ProgramManagementServices.Build(permissions,
            portia => portia.AddRequestHandler<GetControlDraftHandler>());

        await RequestScenario.For(provider)
            .GivenActor(ProgramManagementServices.Actor(Uuid.CreateVersion4()))
            // Act
            .When(new GetControlDraft(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
                Uuid.CreateVersion4()))
            // Assert
            .ExpectDenied(RequestErrorKind.Forbidden)
            .ExpectNotHandled();
        Assert.Equal([RbacPermissions.ProgramManage], permissions.Permissions);
    }

    sealed class RecordingAccessGrantPermissionAuthorizer(bool allowed, Uuid? allowedProgramId = null,
        bool organizationWide = false)
        : IAccessGrantPermissionAuthorizer
    {
        public Uuid ProgramId { get; private set; }
        public string? Permission { get; private set; }
        public IReadOnlySet<Uuid> ProgramIds { get; private set; } = new HashSet<Uuid>();
        public bool OrganizationWide { get; private set; }

        public ValueTask<ProgramAccessVisibility> GetProgramVisibilityAsync(Uuid tenantId,
            Uuid userId, Uuid memberId, string permission, CancellationToken ct = default)
        {
            Permission = permission;
            var programIds = new HashSet<Uuid>();
            if (allowed && allowedProgramId is { } programId)
                programIds.Add(programId);
            ProgramIds = programIds;
            OrganizationWide = allowed && organizationWide;
            return ValueTask.FromResult(allowed
                ? new ProgramAccessVisibility(OrganizationWide, ProgramIds)
                : new ProgramAccessVisibility(false, new HashSet<Uuid>()));
        }

        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            Uuid programId, string permission, CancellationToken ct = default)
        {
            ProgramId = programId;
            Permission = permission;
            return ValueTask.FromResult(allowed);
        }
    }

    sealed class MissingProgramScopeResolver : IProgramResourceScopeResolver
    {
        public ValueTask<bool> IsTenantProgramAsync(Uuid tenantId, Uuid programId,
            CancellationToken ct = default) => ValueTask.FromResult(false);

        public ValueTask<Uuid?> ResolveProgramIdAsync(Uuid tenantId, IProgramResourceRequest request,
            CancellationToken ct = default) => ValueTask.FromResult<Uuid?>(null);
    }
}
