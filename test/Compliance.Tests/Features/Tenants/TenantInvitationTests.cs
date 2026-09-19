using System.Security.Cryptography;
using System.Text;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class TenantInvitationTests
{
    [Fact]
    public void ShouldRequireCurrentTokenBeforeExpiryAndAcceptOnce()
    {
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var operatorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var invitation = new TenantInvitation(tenantId, " ADMIN@EXAMPLE.COM ");
        var scenario = new AggregateScenario<TenantInvitation>(invitation);
        var token = "first-token";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

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
    public void FirmStaffInvitationCannotGrantAdministrator()
    {
        var invitation = new TenantInvitation(Uuid.CreateVersion4(), "staff@example.com");
        var result = invitation.Invite("firm_staff", true, new string('A', 64),
            DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow, Uuid.CreateVersion4());

        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error.Kind);
    }
}
