using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class AccessGrantProposalValidatorTests
{
    [Fact]
    public async Task ShouldRejectFirmStaffGivenDirectMemberGrant()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var roleId = Uuid.CreateVersion4();
        var validator = Validator(tenantId, roleId, userId, "firm_staff");

        // Act
        var result = await validator.ValidateAsync(tenantId, Proposal(
            tenantId, roleId, new AccessGrantPrincipal(AccessGrantPrincipalKind.Member,
                RbacIds.Member(tenantId, userId))));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error.Kind);
    }

    [Fact]
    public async Task ShouldAllowOrganizationMemberGivenKnownRoleAndProgramScope()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var roleId = Uuid.CreateVersion4();
        var validator = Validator(tenantId, roleId, userId, "client_personnel");

        // Act
        var result = await validator.ValidateAsync(tenantId, Proposal(tenantId, roleId,
            new AccessGrantPrincipal(AccessGrantPrincipalKind.Member, RbacIds.Member(tenantId, userId)),
            new AccessGrantScope(AccessGrantScopeKind.Program, Uuid.CreateVersion4())));

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ShouldRejectProgramFromAnotherTenantGivenGrantProposal()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var roleId = Uuid.CreateVersion4();
        var otherTenantId = Uuid.CreateVersion4();
        var validator = Validator(tenantId, roleId, userId, "client_personnel", otherTenantId);

        // Act
        var result = await validator.ValidateAsync(tenantId, Proposal(tenantId, roleId,
            new AccessGrantPrincipal(AccessGrantPrincipalKind.Member, RbacIds.Member(tenantId, userId)),
            new AccessGrantScope(AccessGrantScopeKind.Program, Uuid.CreateVersion4())));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Fact]
    public async Task ShouldAllowProgramGrantGivenSourceCommittedBeforeProjection()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var roleId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var program = new ComplianceProgram(tenantId, programId);
        Assert.Null(program.Create("Program", new ProgramPlan(null, null, null, null, null, null),
            Uuid.CreateVersion4(), "Admin", DateTimeOffset.UtcNow));
        var validator = new AccessGrantProposalValidator(
            new MembershipReader(userId, tenantId, "client_personnel"), new TeamDirectory(),
            new RoleDirectory(roleId), ResourceScopes(tenantId, program, false));

        // Act
        var result = await validator.ValidateAsync(tenantId, Proposal(tenantId, roleId,
            new AccessGrantPrincipal(AccessGrantPrincipalKind.Member,
                RbacIds.Member(tenantId, userId)),
            new AccessGrantScope(AccessGrantScopeKind.Program, programId)));

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ShouldRejectMismatchedRoleRecordGivenGrantProposal()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var roleId = Uuid.CreateVersion4();
        var validator = new AccessGrantProposalValidator(
            new MembershipReader(userId, tenantId, "client_personnel"), new TeamDirectory(),
            new RoleDirectory(roleId, Uuid.CreateVersion4()), ResourceScopes(tenantId));

        // Act
        var result = await validator.ValidateAsync(tenantId, Proposal(tenantId, roleId,
            new AccessGrantPrincipal(AccessGrantPrincipalKind.Member,
                RbacIds.Member(tenantId, userId))));

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Fact]
    public async Task ShouldRejectMismatchedTeamRecordGivenGrantProposal()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var roleId = Uuid.CreateVersion4();
        var teamId = Uuid.CreateVersion4();
        var validator = new AccessGrantProposalValidator(
            new MembershipReader(Uuid.CreateVersion4(), tenantId, "client_personnel"),
            new TeamDirectory(Uuid.CreateVersion4()), new RoleDirectory(roleId),
            ResourceScopes(tenantId));

        // Act
        var result = await validator.ValidateAsync(tenantId, Proposal(tenantId, roleId,
            new AccessGrantPrincipal(AccessGrantPrincipalKind.Team, teamId)));

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Fact]
    public async Task ShouldRejectForeignMembershipRowGivenGrantProposal()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var roleId = Uuid.CreateVersion4();
        var validator = new AccessGrantProposalValidator(
            new MembershipReader(userId, tenantId, "client_personnel", Uuid.CreateVersion4()),
            new TeamDirectory(), new RoleDirectory(roleId), ResourceScopes(tenantId));

        // Act
        var result = await validator.ValidateAsync(tenantId, Proposal(tenantId, roleId,
            new AccessGrantPrincipal(AccessGrantPrincipalKind.Member,
                RbacIds.Member(tenantId, userId))));

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Theory]
    [InlineData(AccessGrantScopeKind.Engagement)]
    [InlineData(AccessGrantScopeKind.SharedResource)]
    public async Task ShouldRejectUnsupportedScopeGivenGrantProposal(AccessGrantScopeKind scopeKind)
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var roleId = Uuid.CreateVersion4();
        var validator = Validator(tenantId, roleId, userId, "client_personnel");
        var resourceType = scopeKind == AccessGrantScopeKind.SharedResource ? "application" : null;

        // Act
        var result = await validator.ValidateAsync(tenantId, Proposal(tenantId, roleId,
            new AccessGrantPrincipal(AccessGrantPrincipalKind.Member,
                RbacIds.Member(tenantId, userId)),
            new AccessGrantScope(scopeKind, Uuid.CreateVersion4(), resourceType)));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error.Kind);
    }

    static AccessGrantProposalValidator Validator(Uuid tenantId, Uuid roleId, Uuid userId,
        string affiliation, Uuid? programTenantId = null) =>
        new(new MembershipReader(userId, tenantId, affiliation),
            new TeamDirectory(), new RoleDirectory(roleId),
            ResourceScopes(programTenantId ?? tenantId));

    static ProgramResourceScopeResolver ResourceScopes(Uuid programTenantId,
        ComplianceProgram? source = null, bool projected = true) =>
        new(new ProgramDirectory(programTenantId, projected),
            null!, null!, null!, new SourceReader(source));

    static AccessGrantProposal Proposal(Uuid tenantId, Uuid roleId, AccessGrantPrincipal principal,
        AccessGrantScope? scope = null) => new(principal, roleId,
        scope ?? new AccessGrantScope(AccessGrantScopeKind.Organization, tenantId),
        new AccessGrantSource("manual", "request-1"), DateTimeOffset.UnixEpoch, null);

    sealed class MembershipReader(Uuid memberUserId, Uuid membershipTenantId, string affiliation,
        Uuid? returnedTenantId = null)
        : ITenantMembershipDirectoryReader
    {
        public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId,
            CancellationToken ct = default) => ValueTask.FromResult<TenantMembershipView?>(null);

        public ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId,
            CancellationToken ct = default) => ValueTask.FromResult(false);

        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit,
            string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<TenantMembershipView>(
                tenantId == membershipTenantId
                    ? [new TenantMembershipView(memberUserId, returnedTenantId ?? tenantId,
                        affiliation)]
                    : [], null));
    }

    sealed class RoleDirectory(Uuid roleId, Uuid? returnedRoleId = null) : IRoleDirectoryReader
    {
        public ValueTask<RoleView?> GetAsync(Uuid tenantId, Uuid requestedRoleId,
            CancellationToken ct = default) => ValueTask.FromResult<RoleView?>(
            roleId == requestedRoleId
                ? new RoleView(returnedRoleId ?? roleId, "Compliance Lead")
                : null);

        public ValueTask<Page<RoleView>> ListAsync(Uuid tenantId, int? limit, string? cursor,
            string? search, bool descending, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<RoleView>([], null));
    }

    sealed class TeamDirectory(Uuid? returnedTeamId = null) : ITeamDirectoryReader
    {
        public ValueTask<TeamView?> GetAsync(Uuid tenantId, Uuid teamId, CancellationToken ct = default) =>
            ValueTask.FromResult<TeamView?>(new TeamView(returnedTeamId ?? teamId, "Reviewers"));

        public ValueTask<Page<TeamView>> ListAsync(Uuid tenantId, int? limit, string? cursor,
            string? search, bool descending, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<TeamView>([], null));
    }

    sealed class ProgramDirectory(Uuid programTenantId, bool projected) : IProgramDirectoryReader
    {
        public ValueTask<ProgramView?> GetAsync(Uuid tenantId, Uuid programId,
            CancellationToken ct = default) => ValueTask.FromResult<ProgramView?>(
            projected ? new ProgramView(programTenantId, programId, "Program", "readiness", null, 1,
                new ProgramPlan(null, null, null, null, null, null), Uuid.CreateVersion4(),
                "Admin", DateTimeOffset.UnixEpoch, []) : null);

        public ValueTask<Page<ProgramView>> ListAsync(Uuid tenantId, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(new Page<ProgramView>([], null));

        public ValueTask<Page<ProgramRevisionView>?> ListRevisionsAsync(Uuid tenantId, Uuid programId,
            int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult<Page<ProgramRevisionView>?>(new Page<ProgramRevisionView>([], null));

        public ValueTask<ProgramRevisionView?> GetRevisionAsync(Uuid tenantId, Uuid programId,
            long revision, CancellationToken ct = default) =>
            ValueTask.FromResult<ProgramRevisionView?>(null);

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);
    }

    sealed class SourceReader(ComplianceProgram? source) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult(source is not null && source.Stream == aggregate.Stream &&
                aggregate is ComplianceProgram ? (TAggregate)(Aggregate)source : aggregate);
    }
}
