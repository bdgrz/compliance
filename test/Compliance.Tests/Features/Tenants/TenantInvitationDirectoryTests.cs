using System.Security.Claims;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class TenantInvitationDirectoryTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid InvitedBy = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldReplacePendingInvitationAndHideTokenGivenReissueAndReplay()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var directory = new FitzTenantInvitationDirectory(client);
        var aggregate = new TenantInvitation(TenantId, "invitee@example.com");
        var firstHash = new string('A', 64);
        var secondHash = new string('B', 64);
        Assert.True(aggregate.Invite("client_personnel", false, firstHash,
            Now.AddDays(1), Now, InvitedBy,
            BuiltInRbac.ComplianceParticipationRole).IsSuccess);
        Assert.True(aggregate.Invite("client_personnel", false, secondHash,
            Now.AddDays(7), Now, InvitedBy,
            BuiltInRbac.ComplianceManagementRole).IsSuccess);
        var events = new AggregateScenario<TenantInvitation>(aggregate).PendingEvents;
        var identity = new CheckpointIdentity("TenantInvitationDirectory",
            EventStreamPattern.ForPattern(TenantId.ToString()));
        await using (var batch = await directory.BeginAsync(
            new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            foreach (var domainEvent in events)
                await directory.ApplyAsync(domainEvent);
            await directory.ApplyAsync(events[1]);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var page = await directory.ListAsync(TenantId, 50, null);
        var otherTenant = await directory.ListAsync(Uuid.CreateVersion4(), 50, null);

        // Assert
        var invitation = Assert.Single(page.Items);
        Assert.Equal(Now.AddDays(7), invitation.ExpiresAt);
        Assert.Equal(BuiltInRbac.ComplianceManagementRole, invitation.BuiltInRole);
        Assert.Empty(otherTenant.Items);
        var stored = JsonSerializer.Serialize(invitation,
            ComplianceCoreJsonContext.Default.TenantInvitationDirectoryEntry);
        Assert.DoesNotContain("token", stored, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(firstHash, stored, StringComparison.Ordinal);
        Assert.DoesNotContain(secondHash, stored, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldRemainPendingActivationUntilMemberAndRoleProjectGivenAcceptance()
    {
        // Arrange
        var userId = Uuid.CreateVersion4();
        var entry = new TenantInvitationDirectoryEntry(TenantId, "invitee@example.com",
            "client_personnel", false, BuiltInRbac.ComplianceManagementRole,
            Now.AddDays(7), InvitedBy, userId);
        var reader = new FakeInvitationDirectory(entry);
        var membership = new FakeMembershipDirectory();
        var access = new FakeMemberAccessReader();
        var clock = new FixedTimeProvider(Now);
        var handler = new ListTenantInvitationsHandler(reader, membership, access, clock);
        var request = new ListTenantInvitations(TenantId, EmailAddress: "Invitee@Example.com");

        // Act
        var accepted = await handler.HandleAsync(new RequestContext<ListTenantInvitations>(
            request, Actor()), CancellationToken.None);

        // Assert
        Assert.Equal("accepted_pending_activation", Assert.Single(accepted.Value.Items).Status);

        membership.IsMember = true;
        var memberOnly = await handler.HandleAsync(new RequestContext<ListTenantInvitations>(
            request, Actor()), CancellationToken.None);
        Assert.Equal("accepted_pending_activation", Assert.Single(memberOnly.Value.Items).Status);

        access.Edges = [new MemberAccessEdge(
            BuiltInRbac.PowerUsersTeamId(TenantId),
            BuiltInRbac.ComplianceManagementRoleId(TenantId),
            [RbacPermissions.TenantAccess, RbacPermissions.ProgramManage])];
        var active = await handler.HandleAsync(new RequestContext<ListTenantInvitations>(
            request with { ExpectedStatus = "active" }, Actor()), CancellationToken.None);
        Assert.Equal("active", Assert.Single(active.Value.Items).Status);
    }

    [Fact]
    public async Task ShouldReturnConflictGivenExpectedInvitationNotProjected()
    {
        // Arrange
        var handler = new ListTenantInvitationsHandler(new FakeInvitationDirectory(null),
            new FakeMembershipDirectory(), new FakeMemberAccessReader(), new FixedTimeProvider(Now));
        var request = new ListTenantInvitations(TenantId, EmailAddress: "invitee@example.com",
            ExpectedStatus: "pending");

        // Act
        var result = await handler.HandleAsync(new RequestContext<ListTenantInvitations>(
            request, Actor()), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
    }

    [Fact]
    public async Task ShouldShowExpiredInvitationGivenClockPassesExpiration()
    {
        // Arrange
        var entry = new TenantInvitationDirectoryEntry(TenantId, "invitee@example.com",
            "client_personnel", false, BuiltInRbac.ComplianceParticipationRole,
            Now, InvitedBy, null);
        var handler = new ListTenantInvitationsHandler(new FakeInvitationDirectory(entry),
            new FakeMembershipDirectory(), new FakeMemberAccessReader(), new FixedTimeProvider(Now));

        // Act
        var result = await handler.HandleAsync(new RequestContext<ListTenantInvitations>(
            new ListTenantInvitations(TenantId), Actor()), CancellationToken.None);

        // Assert
        Assert.Equal("expired", Assert.Single(result.Value.Items).Status);
    }

    static ClaimsPrincipal Actor() => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())],
        "BdgrzSession"));

    sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    sealed class FakeInvitationDirectory(TenantInvitationDirectoryEntry? entry)
        : ITenantInvitationDirectoryReader
    {
        public ValueTask<TenantInvitationDirectoryEntry?> GetAsync(Uuid tenantId,
            string emailAddress, CancellationToken ct = default) =>
            ValueTask.FromResult(entry is not null && entry.TenantId == tenantId &&
                entry.EmailAddress == emailAddress ? entry : null);

        public ValueTask<Page<TenantInvitationDirectoryEntry>> ListAsync(Uuid tenantId,
            int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<TenantInvitationDirectoryEntry>(
                entry is not null && entry.TenantId == tenantId ? [entry] : [], null));
    }

    sealed class FakeMembershipDirectory : ITenantMembershipDirectoryReader
    {
        public bool IsMember { get; set; }

        public ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId,
            CancellationToken ct = default) => ValueTask.FromResult(IsMember);

        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit,
            string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }

    sealed class FakeMemberAccessReader : IMemberAccessReader
    {
        public IReadOnlyList<MemberAccessEdge> Edges { get; set; } = [];

        public ValueTask<IReadOnlyList<MemberAccessEdge>> ReadAsync(Uuid tenantId, Uuid memberId,
            CancellationToken ct = default) => ValueTask.FromResult(Edges);
    }
}
