using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

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
    public async Task ShouldRejectDeprovisionedMemberGivenDirectGrant()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var roleId = Uuid.CreateVersion4();
        var validator = new AccessGrantProposalValidator(
            new MembershipReader(userId, tenantId, "client_personnel", isDeprovisioned: true),
            new TeamDirectory(), new RoleDirectory(roleId), ResourceScopes(tenantId),
            new SourceReader());

        // Act
        var result = await validator.ValidateAsync(tenantId, Proposal(tenantId, roleId,
            new AccessGrantPrincipal(AccessGrantPrincipalKind.Member,
                RbacIds.Member(tenantId, userId))));

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(result.Error).Kind);
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
            new RoleDirectory(roleId), ResourceScopes(tenantId, program, false),
            new SourceReader());

        // Act
        var result = await validator.ValidateAsync(tenantId, Proposal(tenantId, roleId,
            new AccessGrantPrincipal(AccessGrantPrincipalKind.Member,
                RbacIds.Member(tenantId, userId)),
            new AccessGrantScope(AccessGrantScopeKind.Program, programId)));

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(AccessGrantScopeKind.Application)]
    [InlineData(AccessGrantScopeKind.SystemInstance)]
    public async Task ShouldAllowExistingApplicationResourceScopeGivenRestrictedReadGrant(
        AccessGrantScopeKind scopeKind)
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var roleId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var instanceId = Uuid.CreateVersion4();
        var actorId = RbacIds.Member(tenantId, userId);
        var application = new DeclaredApplication(tenantId, applicationId);
        Assert.True(application.Declare("Payroll", "Run payroll", null, actorId,
            "Org Admin", DateTimeOffset.UtcNow, isRestricted: true).IsSuccess);
        var instance = new DeclaredSystemInstance(tenantId, instanceId);
        Assert.True(instance.Declare(applicationId, "Production", "production", null,
            "payroll-prod", actorId, "Org Admin", DateTimeOffset.UtcNow).IsSuccess);
        var scopeId = scopeKind == AccessGrantScopeKind.Application ? applicationId : instanceId;
        var validator = new AccessGrantProposalValidator(
            new MembershipReader(userId, tenantId, "client_personnel"), new TeamDirectory(),
            new RoleDirectory(roleId), ResourceScopes(tenantId),
            new ScopeSourceReader(application, instance));

        // Act
        var result = await validator.ValidateAsync(tenantId, Proposal(tenantId, roleId,
            new AccessGrantPrincipal(AccessGrantPrincipalKind.Member,
                RbacIds.Member(tenantId, userId)), new AccessGrantScope(scopeKind, scopeId)));

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ShouldAllowLegacySystemInstanceScopeGivenRestrictedReadGrant()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var roleId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var instanceId = Uuid.CreateVersion4();
        var actorId = RbacIds.Member(tenantId, userId);
        var now = DateTimeOffset.UtcNow;
        var application = new DeclaredApplication(tenantId, applicationId);
        Assert.True(application.Declare("Payroll", "Run payroll", null, actorId,
            "Org Admin", now, isRestricted: true).IsSuccess);
        var applicationDeclared = new ApplicationDeclared(tenantId, applicationId, "Payroll",
            "Run payroll", null, actorId, "Org Admin", now, IsRestricted: true);
        var declared = new SystemInstanceDeclared(tenantId, applicationId, instanceId, 2,
            "Production", "production", null, "payroll-prod", actorId, "Org Admin", now);
        var events = new InMemoryEventStore();
        await events.AppendAsync(new EventStreamAddress(tenantId.ToString(), "applications",
                applicationId.ToString()), 0,
            [DomainEventSeed.Attach(applicationDeclared, applicationId, 1),
                DomainEventSeed.Attach(declared, applicationId, 2)]);
        var directory = new FitzApplicationDirectory(new InMemoryKvClient());
        var checkpointIdentity = new CheckpointIdentity("ApplicationDirectoryV2",
            EventStreamPattern.ForPattern(tenantId.ToString()));
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         checkpointIdentity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(applicationDeclared);
            await directory.ApplyAsync(declared);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var validator = new AccessGrantProposalValidator(
            new MembershipReader(userId, tenantId, "client_personnel"), new TeamDirectory(),
            new RoleDirectory(roleId), ResourceScopes(tenantId), new ScopeSourceReader(application),
            directory, events);

        // Act
        var result = await validator.ValidateAsync(tenantId, Proposal(tenantId, roleId,
            new AccessGrantPrincipal(AccessGrantPrincipalKind.Member,
                RbacIds.Member(tenantId, userId)), new AccessGrantScope(
                AccessGrantScopeKind.SystemInstance, instanceId)));

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ShouldRejectMissingSystemInstanceGivenInstanceScopedGrant()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var roleId = Uuid.CreateVersion4();
        var application = new DeclaredApplication(tenantId, Uuid.CreateVersion4());
        Assert.True(application.Declare("Payroll", "Run payroll", null,
            RbacIds.Member(tenantId, userId), "Org Admin", DateTimeOffset.UtcNow).IsSuccess);
        var validator = new AccessGrantProposalValidator(
            new MembershipReader(userId, tenantId, "client_personnel"), new TeamDirectory(),
            new RoleDirectory(roleId), ResourceScopes(tenantId),
            new ScopeSourceReader(application));

        // Act
        var result = await validator.ValidateAsync(tenantId, Proposal(tenantId, roleId,
            new AccessGrantPrincipal(AccessGrantPrincipalKind.Member,
                RbacIds.Member(tenantId, userId)), new AccessGrantScope(
                AccessGrantScopeKind.SystemInstance, Uuid.CreateVersion4())));

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Fact]
    public async Task ShouldRejectDeletedRoleGivenStaleRoleDirectory()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var roleId = Uuid.CreateVersion4();
        var validator = Validator(tenantId, roleId, userId, "client_personnel",
            roleDeleted: true);

        // Act
        var result = await validator.ValidateAsync(tenantId, Proposal(tenantId, roleId,
            new AccessGrantPrincipal(AccessGrantPrincipalKind.Member,
                RbacIds.Member(tenantId, userId))));

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Fact]
    public async Task ShouldRejectDeletedTeamGivenStaleTeamDirectory()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var roleId = Uuid.CreateVersion4();
        var teamId = Uuid.CreateVersion4();
        var validator = Validator(tenantId, roleId, Uuid.CreateVersion4(),
            "client_personnel", teamDeleted: true);

        // Act
        var result = await validator.ValidateAsync(tenantId, Proposal(tenantId, roleId,
            new AccessGrantPrincipal(AccessGrantPrincipalKind.Team, teamId)));

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(result.Error).Kind);
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
            new RoleDirectory(roleId, Uuid.CreateVersion4()), ResourceScopes(tenantId),
            new SourceReader());

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
            ResourceScopes(tenantId), new SourceReader());

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
            new TeamDirectory(), new RoleDirectory(roleId), ResourceScopes(tenantId),
            new SourceReader());

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
        string affiliation, Uuid? programTenantId = null, bool roleDeleted = false,
        bool teamDeleted = false) =>
        new(new MembershipReader(userId, tenantId, affiliation),
            new TeamDirectory(), new RoleDirectory(roleId),
            ResourceScopes(programTenantId ?? tenantId),
            new SourceReader(roleDeleted: roleDeleted, teamDeleted: teamDeleted));

    static ProgramResourceScopeResolver ResourceScopes(Uuid programTenantId,
        ComplianceProgram? source = null, bool projected = true) =>
        new(new ProgramDirectory(programTenantId, projected),
            null!, null!, null!, new SourceReader(source));

    static AccessGrantProposal Proposal(Uuid tenantId, Uuid roleId, AccessGrantPrincipal principal,
        AccessGrantScope? scope = null) => new(principal, roleId,
        scope ?? new AccessGrantScope(AccessGrantScopeKind.Organization, tenantId),
        new AccessGrantSource("manual", "request-1"), DateTimeOffset.UnixEpoch, null);

    sealed class MembershipReader(Uuid memberUserId, Uuid membershipTenantId, string affiliation,
        Uuid? returnedTenantId = null, bool isDeprovisioned = false)
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
                        affiliation, IsDeprovisioned: isDeprovisioned)]
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

    sealed class SourceReader(ComplianceProgram? source = null, bool roleDeleted = false,
        bool teamDeleted = false) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            if (source is not null && source.Stream == aggregate.Stream &&
                aggregate is ComplianceProgram)
                return ValueTask.FromResult((TAggregate)(Aggregate)source);
            switch (aggregate)
            {
                case Role role:
                    Assert.True(role.Define("Program Admin").IsSuccess);
                    if (roleDeleted)
                        Assert.True(role.Delete().IsSuccess);
                    break;
                case Team team:
                    Assert.True(team.Define("Reviewers").IsSuccess);
                    if (teamDeleted)
                        Assert.True(team.Delete().IsSuccess);
                    break;
            }
            return ValueTask.FromResult(aggregate);
        }
    }

    sealed class ScopeSourceReader(DeclaredApplication? application = null,
        DeclaredSystemInstance? instance = null) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            if (aggregate is Role role)
                Assert.True(role.Define("Compliance Lead").IsSuccess);
            if (application is not null && aggregate is DeclaredApplication requestedApplication &&
                requestedApplication.Stream == application.Stream)
                return ValueTask.FromResult((TAggregate)(Aggregate)application);
            if (instance is not null && aggregate is DeclaredSystemInstance requestedInstance &&
                requestedInstance.Stream == instance.Stream)
                return ValueTask.FromResult((TAggregate)(Aggregate)instance);
            return ValueTask.FromResult(aggregate);
        }
    }
}
