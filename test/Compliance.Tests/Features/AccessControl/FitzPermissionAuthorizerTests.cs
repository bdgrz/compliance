using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class FitzPermissionAuthorizerTests
{
    [Fact]
    public async Task ShouldDenyGivenRevocationProjectsBetweenExplanationAndPendingScan()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var memberId = RbacIds.Member(tenantId, userId);
        var teamId = BuiltInRbac.AdministratorsTeamId(tenantId);
        var roleId = BuiltInRbac.TenantAdministrationRoleId(tenantId);
        FitzPermissionAuthorizer? permissions = null;
        var events = new ProjectionInterleavingReader(async () =>
        {
            var identity = new CheckpointIdentity("PermissionProjection",
                EventStreamPattern.ForPattern(tenantId.ToString()));
            await using var batch = await permissions!.BeginAsync(new ProjectionBatchContext(
                identity, ProjectionCheckpoint.Start));
            await permissions.ApplyAsync(new TeamMemberRemoved(tenantId, teamId, memberId));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        });
        permissions = new FitzPermissionAuthorizer(client, new FixedMembershipDirectory(true), events);
        await SeedActiveTeamGrantAsync(permissions, tenantId, userId, teamId, roleId);

        // Act: the read of explained access sees the old team grant, then the projector
        // removes its key just before the pending-event iterator is read.
        var allowed = await permissions.IsAllowedAsync(tenantId, userId, memberId,
            RbacPermissions.TenantAccess);

        // Assert
        Assert.True(events.Interleaved);
        Assert.False(allowed);
    }

    [Fact]
    public async Task ShouldDenyGivenLegacyGrantKeyWithoutExplanationAndPendingSuspension()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var events = new InMemoryEventStore();
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var memberId = RbacIds.Member(tenantId, userId);
        var permissions = new FitzPermissionAuthorizer(client, new FixedMembershipDirectory(true), events);
        var route = await new PermissionRouteReader(client).GetRouteAsync(tenantId);
        await using (var tx = await client.BeginAsync(route, KvDurability.Sync, KvMode.ReadWrite))
        {
            await tx.PutAsync(PermissionProjectionKeys.Grant(memberId, RbacPermissions.TenantAccess),
                "1"u8.ToArray());
            await tx.CommitAsync();
        }
        Assert.Empty(await permissions.ReadAsync(tenantId, memberId));
        Assert.False(await permissions.IsAllowedAsync(tenantId, userId, memberId,
            RbacPermissions.TenantAccess));
        DomainEvent suspension = new MemberSuspended(tenantId, memberId, userId, Uuid.CreateVersion4(),
            "Alex Admin", DateTimeOffset.UtcNow, "Employment ended.");
        suspension.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), memberId, 1,
            DateTimeOffset.UtcNow));
        await events.AppendAsync(new EventStreamAddress(tenantId.ToString(), "rbac-members",
            memberId.ToString()), 0, [suspension]);

        // Act
        var allowed = await permissions.IsAllowedAsync(tenantId, userId, memberId,
            RbacPermissions.TenantAccess);

        // Assert
        Assert.False(allowed);
    }

    [Fact]
    public async Task ShouldDenyGivenMemberSuspensionHasNotReachedPermissionProjection()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var events = new InMemoryEventStore();
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var memberId = RbacIds.Member(tenantId, userId);
        var teamId = BuiltInRbac.AdministratorsTeamId(tenantId);
        var roleId = BuiltInRbac.TenantAdministrationRoleId(tenantId);
        var membership = new FixedMembershipDirectory(true);
        using var services = CreateServices(client, membership, events);
        var permissions = ActivatorUtilities.CreateInstance<FitzPermissionAuthorizer>(services);
        var pattern = EventStreamPattern.ForPattern(tenantId.ToString());
        var identity = new CheckpointIdentity("PermissionProjection", pattern);
        await SeedActiveTeamGrantAsync(permissions, tenantId, userId, teamId, roleId);
        Assert.True(await permissions.IsAllowedAsync(tenantId, userId, memberId,
            RbacPermissions.TenantAccess));
        DomainEvent suspension = new MemberSuspended(tenantId, memberId, userId, Uuid.CreateVersion4(),
            "Alex Admin", DateTimeOffset.UtcNow, "Employment ended.");
        suspension.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), memberId, 1,
            DateTimeOffset.UtcNow));
        var stream = new EventStreamAddress(tenantId.ToString(), "rbac-members", memberId.ToString());
        await events.AppendAsync(stream, 0, [suspension]);

        // Act
        var allowedWhileProjectionLags = await permissions.IsAllowedAsync(tenantId, userId,
            memberId, RbacPermissions.TenantAccess);
        var cursor = EventCursor.Start;
        await foreach (var record in events.ReadAsync(pattern, cursor, CancellationToken.None))
        {
            await using var batch = await permissions.BeginAsync(new ProjectionBatchContext(
                identity, new ProjectionCheckpoint(cursor)));
            await permissions.ApplyAsync(record.Event);
            cursor = record.NextCursor;
            await batch.CommitAsync(new ProjectionCheckpoint(cursor));
        }
        var deniedAfterProjection = await permissions.IsAllowedAsync(tenantId, userId,
            memberId, RbacPermissions.TenantAccess);

        DomainEvent reinstatement = new MemberReinstated(tenantId, memberId, userId,
            "client_personnel",
            Uuid.CreateVersion4(), "Alex Admin", DateTimeOffset.UtcNow);
        reinstatement.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), memberId, 2,
            DateTimeOffset.UtcNow));
        await events.AppendAsync(stream, 1, [reinstatement]);
        var deniedWhileReinstatementLags = await permissions.IsAllowedAsync(tenantId, userId,
            memberId, RbacPermissions.TenantAccess);
        var restartedPermissions = new FitzPermissionAuthorizer(client, membership, events);
        await foreach (var record in events.ReadAsync(pattern, cursor, CancellationToken.None))
        {
            await using var batch = await restartedPermissions.BeginAsync(new ProjectionBatchContext(
                identity, new ProjectionCheckpoint(cursor)));
            await restartedPermissions.ApplyAsync(record.Event);
            cursor = record.NextCursor;
            await batch.CommitAsync(new ProjectionCheckpoint(cursor));
        }
        var allowedAfterRecovery = await restartedPermissions.IsAllowedAsync(tenantId, userId,
            memberId, RbacPermissions.TenantAccess);

        // Assert
        Assert.False(allowedWhileProjectionLags);
        Assert.False(deniedAfterProjection);
        Assert.False(deniedWhileReinstatementLags);
        Assert.True(allowedAfterRecovery);
    }

    [Fact]
    public async Task ShouldDenyGivenTeamMemberRemovalHasNotReachedPermissionProjection()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var events = new InMemoryEventStore();
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var memberId = RbacIds.Member(tenantId, userId);
        var teamId = BuiltInRbac.AdministratorsTeamId(tenantId);
        var roleId = BuiltInRbac.TenantAdministrationRoleId(tenantId);
        var membership = new FixedMembershipDirectory(true);
        using var services = CreateServices(client, membership, events);
        var permissions = ActivatorUtilities.CreateInstance<FitzPermissionAuthorizer>(services);
        var pattern = EventStreamPattern.ForPattern(tenantId.ToString());
        var identity = new CheckpointIdentity("PermissionProjection", pattern);
        await SeedActiveTeamGrantAsync(permissions, tenantId, userId, teamId, roleId);
        var assignmentId = RbacIds.TeamMember(tenantId, teamId, memberId);
        DomainEvent assigned = new TeamMemberAssigned(tenantId, teamId, memberId);
        assigned.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), assignmentId, 1,
            DateTimeOffset.UtcNow));
        DomainEvent removal = new TeamMemberRemoved(tenantId, teamId, memberId);
        removal.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), assignmentId, 2,
            DateTimeOffset.UtcNow));
        await events.AppendAsync(new EventStreamAddress(tenantId.ToString(), "rbac-team-members",
            assignmentId.ToString()), 0, [assigned, removal]);

        // Act
        var allowedWhileProjectionLags = await permissions.IsAllowedAsync(tenantId, userId,
            memberId, RbacPermissions.TenantAccess);
        var cursor = EventCursor.Start;
        await foreach (var record in events.ReadAsync(pattern, cursor, CancellationToken.None))
        {
            await using var batch = await permissions.BeginAsync(new ProjectionBatchContext(
                identity, new ProjectionCheckpoint(cursor)));
            await permissions.ApplyAsync(record.Event);
            cursor = record.NextCursor;
            await batch.CommitAsync(new ProjectionCheckpoint(cursor));
        }
        var allowedAfterProjectionRecovers = await permissions.IsAllowedAsync(tenantId, userId,
            memberId, RbacPermissions.TenantAccess);

        // Assert
        Assert.False(allowedWhileProjectionLags);
        Assert.False(allowedAfterProjectionRecovers);
    }

    [Fact]
    public async Task ShouldAllowGivenAnotherMembersTeamRemovalHasNotReachedPermissionProjection()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var events = new InMemoryEventStore();
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var memberId = RbacIds.Member(tenantId, userId);
        var teamId = BuiltInRbac.AdministratorsTeamId(tenantId);
        var roleId = BuiltInRbac.TenantAdministrationRoleId(tenantId);
        using var services = CreateServices(client, new FixedMembershipDirectory(true), events);
        var permissions = ActivatorUtilities.CreateInstance<FitzPermissionAuthorizer>(services);
        var pattern = EventStreamPattern.ForPattern(tenantId.ToString());
        await SeedActiveTeamGrantAsync(permissions, tenantId, userId, teamId, roleId);
        var otherMemberId = RbacIds.Member(tenantId, Uuid.CreateVersion4());
        var assignmentId = RbacIds.TeamMember(tenantId, teamId, otherMemberId);
        DomainEvent removal = new TeamMemberRemoved(tenantId, teamId, otherMemberId);
        removal.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), assignmentId, 1,
            DateTimeOffset.UtcNow));
        await events.AppendAsync(new EventStreamAddress(tenantId.ToString(), "rbac-team-members",
            assignmentId.ToString()), 0, [removal]);

        // Act
        var allowed = await permissions.IsAllowedAsync(tenantId, userId, memberId,
            RbacPermissions.TenantAccess);

        // Assert
        Assert.True(allowed);
    }

    [Fact]
    public async Task ShouldAllowGivenUnprojectedProgramEventDoesNotChangePermissions()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var memberId = RbacIds.Member(tenantId, userId);
        var teamId = BuiltInRbac.AdministratorsTeamId(tenantId);
        var roleId = BuiltInRbac.TenantAdministrationRoleId(tenantId);
        var events = new InMemoryEventStore();
        using var services = CreateServices(new InMemoryKvClient(),
            new FixedMembershipDirectory(true), events);
        var permissions = ActivatorUtilities.CreateInstance<FitzPermissionAuthorizer>(services);
        await SeedActiveTeamGrantAsync(permissions, tenantId, userId, teamId, roleId);
        var programId = Uuid.CreateVersion4();
        DomainEvent created = new ProgramCreated(tenantId, programId, "SOC 2",
            new ProgramPlan(null, null, null, null, null, null), memberId, "Owner",
            DateTimeOffset.UtcNow);
        created.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), programId, 1,
            DateTimeOffset.UtcNow));
        await events.AppendAsync(new EventStreamAddress(tenantId.ToString(), "programs",
            programId.ToString()), 0, [created]);

        // Act
        var allowed = await permissions.IsAllowedAsync(tenantId, userId, memberId,
            RbacPermissions.TenantAccess);

        // Assert
        Assert.True(allowed);
    }

    [Fact]
    public async Task ShouldDenyFirmStaffGivenPersistedPreUpgradePermissionGrant()
    {
        // Arrange: an old permission projection has a grant, while membership says firm_staff.
        var client = new InMemoryKvClient();
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var memberId = RbacIds.Member(tenantId, userId);
        var teamId = Uuid.CreateVersion4();
        var roleId = Uuid.CreateVersion4();
        var memberships = new FitzTenantMembershipDirectoryReader(client);
        var permissions = new FitzPermissionAuthorizer(client, memberships, new InMemoryEventStore());
        var permissionIdentity = new CheckpointIdentity("PermissionProjection",
            EventStreamPattern.ForPattern(tenantId.ToString()));
        await using (var batch = await permissions.BeginAsync(new ProjectionBatchContext(
                         permissionIdentity, ProjectionCheckpoint.Start)))
        {
            await permissions.ApplyAsync(new MemberRegistered(tenantId, memberId, userId));
            await permissions.ApplyAsync(new TeamDefined(tenantId, teamId, "Administrators"));
            await permissions.ApplyAsync(new RoleDefined(tenantId, roleId, "Tenant Administration"));
            await permissions.ApplyAsync(new TeamMemberAssigned(tenantId, teamId, memberId));
            await permissions.ApplyAsync(new TeamRoleAssigned(tenantId, teamId, roleId));
            await permissions.ApplyAsync(new RolePermissionAssigned(tenantId, roleId,
                RbacPermissions.TenantAccess));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var membershipIdentity = new CheckpointIdentity("TenantMembership",
            EventStreamPattern.ForPattern(tenantId.ToString()));
        await using (var batch = await memberships.BeginAsync(new ProjectionBatchContext(
                         membershipIdentity, ProjectionCheckpoint.Start)))
        {
            await memberships.ApplyAsync(new MemberRegistered(tenantId, memberId, userId,
                "firm_staff"));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var allowed = await permissions.IsAllowedAsync(tenantId, userId, memberId,
            RbacPermissions.TenantAccess);

        // Assert
        Assert.False(allowed);
    }

    static ServiceProvider CreateServices(IKvClient client,
        ITenantMembershipDirectoryReader memberships, IDomainEventReader events) =>
        new ServiceCollection()
            .AddSingleton(client)
            .AddSingleton(memberships)
            .AddSingleton(events)
            .BuildServiceProvider();

    static async Task SeedActiveTeamGrantAsync(FitzPermissionAuthorizer permissions,
        Uuid tenantId, Uuid userId, Uuid teamId, Uuid roleId)
    {
        var pattern = EventStreamPattern.ForPattern(tenantId.ToString());
        var identity = new CheckpointIdentity("PermissionProjection", pattern);
        await using var batch = await permissions.BeginAsync(new ProjectionBatchContext(
            identity, ProjectionCheckpoint.Start));
        await permissions.ApplyAsync(new MemberRegistered(tenantId,
            RbacIds.Member(tenantId, userId), userId));
        await permissions.ApplyAsync(new TeamDefined(tenantId, teamId, "Administrators"));
        await permissions.ApplyAsync(new RoleDefined(tenantId, roleId, "Org Admin"));
        await permissions.ApplyAsync(new TeamMemberAssigned(tenantId, teamId,
            RbacIds.Member(tenantId, userId)));
        await permissions.ApplyAsync(new TeamRoleAssigned(tenantId, teamId, roleId));
        await permissions.ApplyAsync(new RolePermissionAssigned(tenantId, roleId,
            RbacPermissions.TenantAccess));
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }

    sealed class PermissionRouteReader(IKvClient client)
        : FitzKvProjectionStore(client, FitzPermissionAuthorizer.Route, "PermissionProjection")
    {
        public async Task<string> GetRouteAsync(Uuid tenantId)
        {
            await using var tx = await BeginReadAsync(tenantId.ToString());
            return tx.Route;
        }
    }

    sealed class ProjectionInterleavingReader(Func<Task> projectRevocation) : IDomainEventReader
    {
        public bool Interleaved { get; private set; }

        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream,
            ulong fromVersion, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern,
            EventCursor after, [EnumeratorCancellation] CancellationToken ct = default)
        {
            await projectRevocation();
            Interleaved = true;
            yield break;
        }
    }
}
