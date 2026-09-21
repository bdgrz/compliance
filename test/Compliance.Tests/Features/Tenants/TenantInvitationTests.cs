using System.Security.Cryptography;
using System.Text;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class TenantInvitationTests
{
    [Fact]
    public void ShouldAcceptOnceGivenCurrentUnexpiredToken()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var operatorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var invitation = new TenantInvitation(tenantId, " ADMIN@EXAMPLE.COM ");
        var scenario = new AggregateScenario<TenantInvitation>(invitation);
        var token = "first-token";

        // Act
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        // Assert
        Assert.True(invitation.Invite("client_personnel", true, hash, now.AddDays(7), now, operatorId).IsSuccess);
        Assert.False(invitation.Accept(userId, "wrong-token", now).IsSuccess);
        Assert.False(invitation.Accept(userId, token, now.AddDays(7)).IsSuccess);
        Assert.True(invitation.Accept(userId, token, now).IsSuccess);
        Assert.True(invitation.Accept(userId, token, now).IsSuccess);
        Assert.Single(scenario.PendingEvents.OfType<TenantInvitationAccepted>());
        Assert.Equal("admin@example.com", scenario.PendingEvents.OfType<TenantInvitationAccepted>().Single().EmailAddress);
        Assert.False(invitation.Invite("client_personnel", true, hash, now.AddDays(7), now, operatorId).IsSuccess);
    }

    [Fact]
    public void ShouldRejectAdministratorGrantGivenFirmStaffInvitation()
    {
        // Arrange
        var invitation = new TenantInvitation(Uuid.CreateVersion4(), "staff@example.com");

        // Act
        var result = invitation.Invite("firm_staff", true, new string('A', 64),
            DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow, Uuid.CreateVersion4());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error.Kind);
    }

    [Fact]
    public void ShouldCarrySelectedRoleGivenClientPersonnelInvitation()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var invitation = new TenantInvitation(tenantId, "member@example.com");
        var now = DateTimeOffset.UtcNow;
        var token = "selected-role-token";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        // Act
        var invited = invitation.Invite("client_personnel", false, hash,
            now.AddDays(7), now, Uuid.CreateVersion4(),
            BuiltInRbac.ComplianceManagementRole);
        var accepted = invitation.Accept(Uuid.CreateVersion4(), token, now);
        var events = new AggregateScenario<TenantInvitation>(invitation).PendingEvents;

        // Assert
        Assert.True(invited.IsSuccess);
        Assert.True(accepted.IsSuccess);
        Assert.Equal(BuiltInRbac.ComplianceManagementRole,
            Assert.IsType<TenantMemberInvited>(events[0]).BuiltInRole);
        Assert.Equal(BuiltInRbac.ComplianceManagementRole,
            Assert.IsType<TenantInvitationAccepted>(events[1]).BuiltInRole);
    }

    [Fact]
    public void ShouldRejectRoleGivenUnsupportedValueOrFirmStaff()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var hash = new string('A', 64);
        var invitedBy = Uuid.CreateVersion4();
        var client = new TenantInvitation(tenantId, "client@example.com");
        var firm = new TenantInvitation(tenantId, "firm@example.com");

        // Act
        var unknown = client.Invite("client_personnel", false, hash,
            now.AddDays(7), now, invitedBy, "unknown_role");
        var firmRole = firm.Invite("firm_staff", false, hash,
            now.AddDays(7), now, invitedBy, BuiltInRbac.ComplianceParticipationRole);

        // Assert
        Assert.Equal(RequestErrorKind.Validation,
            Assert.IsType<RequestError>(unknown.Error).Kind);
        Assert.Equal(RequestErrorKind.Validation,
            Assert.IsType<RequestError>(firmRole.Error).Kind);
        Assert.Empty(new AggregateScenario<TenantInvitation>(client).PendingEvents);
        Assert.Empty(new AggregateScenario<TenantInvitation>(firm).PendingEvents);
    }

    [Fact]
    public void ShouldReplacePendingRoleAndTokenGivenInvitationReissue()
    {
        // Arrange
        var invitation = new TenantInvitation(Uuid.CreateVersion4(), "member@example.com");
        var now = DateTimeOffset.UtcNow;
        var firstToken = "first-token";
        var secondToken = "second-token";
        var invitedBy = Uuid.CreateVersion4();
        static string Hash(string token) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        Assert.True(invitation.Invite("client_personnel", false, Hash(firstToken),
            now.AddDays(7), now, invitedBy, BuiltInRbac.ComplianceParticipationRole).IsSuccess);
        Assert.True(invitation.Invite("client_personnel", false, Hash(secondToken),
            now.AddDays(7), now, invitedBy, BuiltInRbac.ComplianceManagementRole).IsSuccess);

        // Act
        var stale = invitation.Accept(Uuid.CreateVersion4(), firstToken, now);
        var accepted = invitation.Accept(Uuid.CreateVersion4(), secondToken, now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(stale.Error).Kind);
        Assert.True(accepted.IsSuccess);
        Assert.Equal(BuiltInRbac.ComplianceManagementRole,
            Assert.Single(new AggregateScenario<TenantInvitation>(invitation).PendingEvents
                .OfType<TenantInvitationAccepted>()).BuiltInRole);
    }

    [Fact]
    public void ShouldPersistDeliveryOutcomeAndAllowNewAttemptGivenFailure()
    {
        // Arrange
        var invitation = new TenantInvitation(Uuid.CreateVersion4(), "member@example.com");
        var now = DateTimeOffset.UtcNow;
        var invitedBy = Uuid.CreateVersion4();
        var firstAttempt = Uuid.CreateVersion4();
        var secondAttempt = Uuid.CreateVersion4();

        // Act
        Assert.True(invitation.Invite("client_personnel", false, new string('A', 64),
            now.AddDays(7), now, invitedBy, deliveryAttemptId: firstAttempt).IsSuccess);
        var failed = invitation.RecordDeliveryFailure(firstAttempt, "delivery_failed", now);
        var replayedFailure = invitation.RecordDeliveryFailure(firstAttempt, "delivery_failed",
            now);
        var staleSent = invitation.RecordDeliverySent(firstAttempt, now);
        Assert.True(invitation.Invite("client_personnel", false, new string('B', 64),
            now.AddDays(7), now, invitedBy, deliveryAttemptId: secondAttempt).IsSuccess);
        var sent = invitation.RecordDeliverySent(secondAttempt, now.AddMinutes(1));
        var replayedSent = invitation.RecordDeliverySent(secondAttempt, now.AddMinutes(1));

        // Assert
        Assert.True(failed.IsSuccess);
        Assert.True(replayedFailure.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(staleSent.Error).Kind);
        Assert.True(sent.IsSuccess);
        Assert.True(replayedSent.IsSuccess);
        var events = new AggregateScenario<TenantInvitation>(invitation).PendingEvents;
        Assert.Equal(2, events.OfType<TenantMemberInvited>().Count());
        Assert.Single(events.OfType<TenantInvitationDeliveryFailed>());
        Assert.Single(events.OfType<TenantInvitationDeliverySent>());
    }

    [Fact]
    public void ShouldPreserveAffiliationAndPurposeGivenPendingInvitationReissue()
    {
        // Arrange
        var invitation = new TenantInvitation(Uuid.CreateVersion4(), "person@example.com");
        var now = DateTimeOffset.UtcNow;
        var hash = new string('A', 64);
        var invitedBy = Uuid.CreateVersion4();
        Assert.True(invitation.Invite("firm_staff", false, hash, now.AddDays(7),
            now, invitedBy).IsSuccess);
        var clientInvitation = new TenantInvitation(Uuid.CreateVersion4(), "client@example.com");
        Assert.True(clientInvitation.Invite("client_personnel", false, hash,
            now.AddDays(7), now, invitedBy).IsSuccess);

        // Act
        var promoted = invitation.Invite("client_personnel", false, hash,
            now.AddDays(7), now, invitedBy, BuiltInRbac.ComplianceManagementRole);
        var administrator = clientInvitation.Invite("client_personnel", true, hash,
            now.AddDays(7), now, invitedBy);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(promoted.Error).Kind);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(administrator.Error).Kind);
        Assert.Single(new AggregateScenario<TenantInvitation>(invitation).PendingEvents);
        Assert.Single(new AggregateScenario<TenantInvitation>(clientInvitation).PendingEvents);
    }
}
