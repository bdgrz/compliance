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
    public async Task ShouldBecomeActiveGivenMembershipWithoutCurrentRoleGrant()
    {
        // Arrange
        var userId = Uuid.CreateVersion4();
        var entry = new TenantInvitationDirectoryEntry(TenantId, "invitee@example.com",
            "client_personnel", false, BuiltInRbac.ComplianceManagementRole,
            Now.AddDays(7), InvitedBy, userId);
        var reader = new FakeInvitationDirectory(entry);
        var membership = new FakeMembershipDirectory();
        var clock = new FixedTimeProvider(Now);
        var handler = new ListTenantInvitationsHandler(reader, membership, clock);
        var request = new ListTenantInvitations(TenantId, EmailAddress: "Invitee@Example.com");

        // Act
        var accepted = await handler.HandleAsync(new RequestContext<ListTenantInvitations>(
            request, Actor()), CancellationToken.None);

        // Assert
        Assert.Equal("accepted_pending_activation", Assert.Single(accepted.Value.Items).Status);

        membership.IsMember = true;
        var active = await handler.HandleAsync(new RequestContext<ListTenantInvitations>(
            request, Actor()), CancellationToken.None);
        Assert.Equal("active", Assert.Single(active.Value.Items).Status);
    }

    [Fact]
    public async Task ShouldProjectDeliveryFailureAndRecoveryGivenReissue()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var directory = new FitzTenantInvitationDirectory(client);
        var now = Now;
        var firstAttempt = Uuid.CreateVersion4();
        var secondAttempt = Uuid.CreateVersion4();
        var invited = new TenantMemberInvited(TenantId, "invitee@example.com",
            "client_personnel", false, new string('A', 64), now.AddDays(7), InvitedBy,
            BuiltInRbac.ComplianceParticipationRole, firstAttempt);
        var identity = new CheckpointIdentity("TenantInvitationDirectory",
            EventStreamPattern.ForPattern(TenantId.ToString()));

        // Act
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(invited);
            await directory.ApplyAsync(new TenantInvitationDeliveryFailed(TenantId,
                invited.EmailAddress, firstAttempt, "delivery_failed", now));
            await directory.ApplyAsync(invited);
            await directory.ApplyAsync(new TenantMemberInvited(TenantId,
                invited.EmailAddress, invited.Affiliation, invited.Administrator,
                new string('B', 64), now.AddDays(7), InvitedBy,
                BuiltInRbac.ComplianceManagementRole, secondAttempt));
            await directory.ApplyAsync(new TenantInvitationDeliverySent(TenantId,
                invited.EmailAddress, secondAttempt, now.AddMinutes(1)));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var page = await directory.ListAsync(TenantId, 50, null);

        // Assert
        var current = Assert.Single(page.Items);
        Assert.Equal("delivered", current.DeliveryStatus);
        Assert.Equal(BuiltInRbac.ComplianceManagementRole, current.BuiltInRole);
        var stored = JsonSerializer.Serialize(current,
            ComplianceCoreJsonContext.Default.TenantInvitationDirectoryEntry);
        Assert.DoesNotContain("token", stored, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(new string('A', 64), stored, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('B', 64), stored, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldReturnEmptyGivenInvitationNotProjected()
    {
        // Arrange
        var handler = new ListTenantInvitationsHandler(new FakeInvitationDirectory(null),
            new FakeMembershipDirectory(), new FixedTimeProvider(Now));
        var request = new ListTenantInvitations(TenantId, EmailAddress: "invitee@example.com");

        // Act
        var result = await handler.HandleAsync(new RequestContext<ListTenantInvitations>(
            request, Actor()), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Items);
    }

    [Fact]
    public async Task ShouldExposeCurrentProjectionGivenReissueStillPending()
    {
        // Arrange
        var prior = new TenantInvitationDirectoryEntry(TenantId, "invitee@example.com",
            "client_personnel", false, BuiltInRbac.ComplianceParticipationRole,
            Now.AddDays(1), InvitedBy, null);
        var directory = new FakeInvitationDirectory(prior);
        var handler = new ListTenantInvitationsHandler(directory,
            new FakeMembershipDirectory(), new FixedTimeProvider(Now));
        var request = new ListTenantInvitations(TenantId, EmailAddress: prior.EmailAddress);

        // Act
        var beforeProjection = await handler.HandleAsync(new RequestContext<ListTenantInvitations>(
            request, Actor()), CancellationToken.None);
        directory.Entry = prior with
        {
            BuiltInRole = BuiltInRbac.ComplianceManagementRole,
            ExpiresAt = Now.AddDays(7),
        };
        var afterProjection = await handler.HandleAsync(new RequestContext<ListTenantInvitations>(
            request, Actor()), CancellationToken.None);

        // Assert
        var stale = Assert.Single(beforeProjection.Value.Items);
        var current = Assert.Single(afterProjection.Value.Items);
        Assert.Equal("pending", stale.Status);
        Assert.Equal("pending", current.Status);
        Assert.Equal(BuiltInRbac.ComplianceParticipationRole, stale.BuiltInRole);
        Assert.Equal(BuiltInRbac.ComplianceManagementRole, current.BuiltInRole);
    }

    [Fact]
    public async Task ShouldShowExpiredInvitationGivenClockPassesExpiration()
    {
        // Arrange
        var entry = new TenantInvitationDirectoryEntry(TenantId, "invitee@example.com",
            "client_personnel", false, BuiltInRbac.ComplianceParticipationRole,
            Now, InvitedBy, null);
        var handler = new ListTenantInvitationsHandler(new FakeInvitationDirectory(entry),
            new FakeMembershipDirectory(), new FixedTimeProvider(Now));

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
        public TenantInvitationDirectoryEntry? Entry { get; set; } = entry;

        public ValueTask<TenantInvitationDirectoryEntry?> GetAsync(Uuid tenantId,
            string emailAddress, CancellationToken ct = default) =>
            ValueTask.FromResult(Entry is not null && Entry.TenantId == tenantId &&
                Entry.EmailAddress == emailAddress ? Entry : null);

        public ValueTask<Page<TenantInvitationDirectoryEntry>> ListAsync(Uuid tenantId,
            int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<TenantInvitationDirectoryEntry>(
                Entry is not null && Entry.TenantId == tenantId ? [Entry] : [], null));
    }

    sealed class FakeMembershipDirectory : ITenantMembershipDirectoryReader
    {
        public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId,
            CancellationToken ct = default) => ValueTask.FromResult<TenantMembershipView?>(IsMember
            ? new TenantMembershipView(userId,
                Uuid.Parse(tenantId, System.Globalization.CultureInfo.InvariantCulture)) : null);

        public bool IsMember { get; set; }

        public ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId,
            CancellationToken ct = default) => ValueTask.FromResult(IsMember);

        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit,
            string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }

}
