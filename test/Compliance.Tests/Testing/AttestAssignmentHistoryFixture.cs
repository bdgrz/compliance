using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Synthetic internal authority fixtures only; never evidence of real professional designation or ratification.</summary>
static class AttestAssignmentHistoryFixture
{
    static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    public static async Task SeedAsync(IServiceProvider provider, Uuid tenant, Uuid canonicalUserId,
        bool revoked = false, string practice = "attest") =>
        _ = await SeedAndReturnAsync(provider, tenant, canonicalUserId, revoked, practice);

    public static async Task<FirmStaffMemberView> SeedAndReturnAsync(IServiceProvider provider, Uuid tenant,
        Uuid canonicalUserId, bool revoked = false, string practice = "attest")
    {
        // Explicit synthetic internal evidence exercises retained history; it grants no public acceptance authority.
        var administrator = Uuid.CreateVersion4();
        var client = ActorReference.ForMember(RbacIds.Member(tenant, administrator), "Synthetic client administrator");
        var staff = new FirmStaffMemberView(Uuid.CreateVersion4(), canonicalUserId, practice, "Synthetic assignee", true,
            1, ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Synthetic operator"), Now);
        var partner = staff with { StaffMemberId = Uuid.CreateVersion4(), UserId = Uuid.CreateVersion4() };
        var engagement = Uuid.CreateVersion4();
        var acknowledgement = Uuid.CreateVersion4();
        await ProgramManagementServices.SeedAsync(provider, new IndependenceLedger(tenant), ledger =>
        {
            Assert.True(ledger.CreateEngagement(Uuid.CreateVersion4(), engagement, 0,
                new ServiceEngagementDraftContent(practice, "Synthetic scope", new DateOnly(2026, 1, 1),
                    new DateOnly(2026, 12, 31), staff.StaffMemberId), staff, client, Now).IsSuccess);
            Assert.True(ledger.AcknowledgeManagement(Uuid.CreateVersion4(), new AcknowledgeEngagementManagement(
                tenant, engagement, acknowledgement, 1, 1, [], "I retain management responsibility"),
                administrator, client, Now).IsSuccess);
            var proof = new VerifiedEngagementAcceptance(tenant, engagement, 1, Uuid.CreateVersion4(),
                partner.StaffMemberId, partner.UserId, "Synthetic verified authority ONLY", null,
                acknowledgement, null, null, [staff], partner, 1, Now);
            var rules = new IndependenceRuleVersionView(1, new IndependenceRuleContent(12,
                [new IndependenceServiceRuleContent("readiness", "conditionally_compatible", "impairing")],
                "Synthetic ratified test rules ONLY"), staff.Actor, Now, true);
            Assert.True(ledger.AcceptEngagement(Uuid.CreateVersion4(), 2, proof, rules, Now).IsSuccess);
            if (revoked)
                Assert.True(ledger.CloseEngagement(Uuid.CreateVersion4(), engagement, 3,
                    "Synthetic client closure", client, Now).IsSuccess);
            return Result.Success;
        });
        return staff;
    }

}
