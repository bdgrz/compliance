using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class PartnerIndependenceEvaluationTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid ClientUser = Uuid.CreateVersion4();
    static readonly ActorReference ClientActor = ActorReference.ForMember(
        RbacIds.Member(Tenant, ClientUser), "Synthetic client administrator");
    static readonly DateTimeOffset Now = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldRetainConditionalPartnerEvaluationBoundToExactDraftAndCompleteClientHistoryGivenCurrentDuty()
    {
        // Arrange
        var (ledger, request, rules, partner, actor) = Fixture();
        var stale = request with { ExpectedServiceHistoryDigest = new string('0', 64) };

        // Act
        var staleResult = ledger.RecordPartnerIndependenceEvaluation(Uuid.CreateVersion4(), stale,
            rules, partner, actor, Now);
        var requestId = Uuid.CreateVersion4();
        var result = ledger.RecordPartnerIndependenceEvaluation(requestId, request,
            rules, partner, actor, Now);
        var events = new Cntryl.Portia.Testing.AggregateScenario<IndependenceLedger>(ledger)
            .PendingEvents.ToArray();
        var replay = new Cntryl.Portia.Testing.AggregateScenario<IndependenceLedger>(
            new IndependenceLedger(Tenant)).Given(events).Aggregate;

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, staleResult.Error?.Kind);
        Assert.True(result.IsSuccess, result.Error?.Message);
        var evaluation = Assert.Single(replay.PartnerEvaluations);
        Assert.Equal(Tenant, evaluation.TenantId);
        Assert.Equal(request.EngagementId, evaluation.EngagementId);
        Assert.Equal(request.ExpectedEngagementRevision, evaluation.DraftRevision);
        Assert.Equal(rules.Version, evaluation.RuleVersion);
        Assert.Equal(IndependenceSourceDigest.RuleContent(rules.Content), evaluation.RuleContentDigest);
        Assert.Equal(request.ExpectedServiceHistoryDigest, evaluation.ServiceHistoryDigest);
        Assert.Equal("partner_evaluation_required", evaluation.DecisionCode);
        Assert.Equal("conditionally_compatible", evaluation.Outcome);
        Assert.Equal(partner.Staff.StaffMemberId, evaluation.PartnerStaffMemberId);
        Assert.Equal(partner.Designation.DesignationId, evaluation.DutyDesignationId);
        Assert.Equal("firm_staff", evaluation.Actor.Kind);
        Assert.Equal(actor, evaluation.Actor);
        Assert.Equal(4, replay.Sequence);
    }

    static (IndependenceLedger Ledger, RecordPartnerIndependenceEvaluation Request,
        IndependenceRuleVersionView Rules, CurrentProfessionalDuty Partner, ActorReference Actor) Fixture()
    {
        var ledger = new IndependenceLedger(Tenant);
        var lead = Staff("attest");
        var engagementId = Uuid.CreateVersion4();
        Assert.True(ledger.CreateEngagement(Uuid.CreateVersion4(), engagementId, 0,
            new ServiceEngagementDraftContent("attest", "Synthetic scope", new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31), lead.StaffMemberId), lead, ClientActor, Now).IsSuccess);
        var serviceId = Uuid.CreateVersion4();
        Assert.True(ledger.RecordService(Uuid.CreateVersion4(), serviceId, 1,
            new NonattestServiceContent(Uuid.CreateVersion4(), "readiness", new DateOnly(2025, 12, 1), null,
                [Uuid.CreateVersion4()], false, "Synthetic client source"), ClientActor, Now).IsSuccess);
        Assert.True(ledger.AcknowledgeManagement(Uuid.CreateVersion4(), new AcknowledgeEngagementManagement(
            Tenant, engagementId, Uuid.CreateVersion4(), 2, 1, [serviceId],
            "I retain management responsibility."), ClientUser, ClientActor, Now).IsSuccess);

        var rulesContent = new IndependenceRuleContent(12,
            [new IndependenceServiceRuleContent("readiness", "conditionally_compatible", "impairing")],
            "Synthetic policy source");
        var digest = IndependenceSourceDigest.RuleContent(rulesContent);
        var partnerStaff = Staff("attest");
        var actor = new ActorReference("firm_staff", partnerStaff.UserId.ToString(), "Synthetic designated partner");
        var duty = new FirmProfessionalDutyDesignationView(Uuid.CreateVersion4(), partnerStaff.StaffMemberId,
            partnerStaff.UserId, FirmProfessionalDuty.EngagementPartner, Tenant, "Synthetic duty evidence",
            partnerStaff.Revision, true, 1, ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Synthetic registrar"),
            Now.AddMinutes(-2), ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Synthetic registrar"),
            Now.AddMinutes(-2), null);
        var ratification = new IndependenceRuleRatificationView(Uuid.CreateVersion4(), 1, 1, digest,
            "Synthetic ratification evidence", partnerStaff.StaffMemberId, partnerStaff.UserId,
            partnerStaff.Revision, Uuid.CreateVersion4(), 1, actor, Now.AddMinutes(-1));
        var rules = new IndependenceRuleVersionView(1, rulesContent, actor, Now.AddMinutes(-1), true)
        {
            Ratification = ratification
        };
        var partner = new CurrentProfessionalDuty(partnerStaff, duty);
        var request = new RecordPartnerIndependenceEvaluation(Tenant, engagementId, Uuid.CreateVersion4(),
            ledger.Sequence, 1, 1, digest, IndependenceSourceDigest.Services(ledger.History().Services),
            "Synthetic partner evaluation rationale");
        return (ledger, request, rules, partner, actor);
    }

    static FirmStaffMemberView Staff(string practice) => new(Uuid.CreateVersion4(), Uuid.CreateVersion4(), practice,
        "Synthetic current staff evidence", true, 1,
        ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Synthetic registrar"), Now.AddMinutes(-3));
}
