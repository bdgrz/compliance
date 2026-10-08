using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class ServiceEngagementLifecycleTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly ActorReference Actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Synthetic client administrator");
    static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldAllowOppositePracticePlanningGivenWithdrawnTentativeStaffProposal()
    {
        // Arrange
        var ledger = new IndependenceLedger(Tenant);
        var advisor = Staff("advisory");
        var other = Staff("advisory");
        var advisory = Uuid.CreateVersion4();
        var attest = Uuid.CreateVersion4();
        Assert.True(ledger.CreateEngagement(Uuid.CreateVersion4(), advisory, 0, Content(advisor), advisor, Actor, Now).IsSuccess);
        Assert.True(ledger.ProposeEngagementStaff(Uuid.CreateVersion4(), advisory, 1, other, Actor, Now).IsSuccess);
        Assert.True(ledger.WithdrawEngagementStaffProposal(Uuid.CreateVersion4(), advisory, other.StaffMemberId, 2,
            "Synthetic removal", Actor, Now).IsSuccess);
        var attestLead = Staff("attest");
        Assert.True(ledger.CreateEngagement(Uuid.CreateVersion4(), attest, 3, Content(attestLead), attestLead, Actor, Now).IsSuccess);
        var relinked = other with { Practice = "attest", UserId = Uuid.CreateVersion4() };

        // Act
        var result = ledger.ProposeEngagementStaff(Uuid.CreateVersion4(), attest, 4, relinked, Actor, Now);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(ledger.Engagement(attest)!.ProfessionalAccessGranted);
        Assert.Equal(5, ledger.Sequence);
    }

    [Fact]
    public void ShouldAllowAnotherPracticeDraftGivenSameCanonicalAccountTentativelyProposed()
    {
        // Arrange
        var ledger = new IndependenceLedger(Tenant);
        var advisor = Staff("advisory");
        Assert.True(ledger.CreateEngagement(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 0,
            Content(advisor), advisor, Actor, Now).IsSuccess);
        var attest = Staff("attest") with { UserId = advisor.UserId };

        // Act
        var result = ledger.CreateEngagement(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1,
            Content(attest), attest, Actor, Now);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, ledger.Sequence);
        Assert.Equal(2, new AggregateScenario<IndependenceLedger>(ledger).PendingEvents.Count);
    }

    [Fact]
    public void ShouldRetainOriginalDraftAndTentativeHistoryGivenAmendWithdrawCloseAndReplay()
    {
        // Arrange
        var ledger = new IndependenceLedger(Tenant);
        var lead = Staff("advisory");
        var next = Staff("advisory");
        var engagementId = Uuid.CreateVersion4();
        var requestId = Uuid.CreateVersion4();
        var content = Content(lead);
        Assert.True(ledger.CreateEngagement(requestId, engagementId, 0, content, lead, Actor, Now).IsSuccess);
        Assert.True(ledger.ProposeEngagementStaff(Uuid.CreateVersion4(), engagementId, 1, next, Actor, Now).IsSuccess);
        Assert.True(ledger.AmendEngagement(Uuid.CreateVersion4(), engagementId, 2,
            content with { EngagementLeadStaffMemberId = next.StaffMemberId, Scope = "Revised scope" }, next, Actor, Now).IsSuccess);
        Assert.True(ledger.WithdrawEngagementStaffProposal(Uuid.CreateVersion4(), engagementId, lead.StaffMemberId,
            3, "Lead changed", Actor, Now).IsSuccess);
        Assert.True(ledger.CloseEngagement(Uuid.CreateVersion4(), engagementId, 4, "Canceled draft", Actor, Now).IsSuccess);
        var events = new AggregateScenario<IndependenceLedger>(ledger).PendingEvents.ToArray();
        var hydrated = new AggregateScenario<IndependenceLedger>(new IndependenceLedger(Tenant)).Given(events).Aggregate;

        // Act
        var retry = hydrated.CreateEngagement(requestId, engagementId, 0, content, lead, Actor, Now.AddDays(1));
        var conflict = hydrated.CreateEngagement(requestId, engagementId, 0,
            content with { Scope = "Different intent" }, lead, Actor, Now);
        var reopen = hydrated.AmendEngagement(Uuid.CreateVersion4(), engagementId, 5, content, lead, Actor, Now);
        var history = hydrated.EngagementHistory(engagementId);

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.Equal(1, retry.Value!.Revision);
        Assert.Equal(content.Scope, retry.Value.Content.Scope);
        Assert.Equal(RequestErrorKind.Conflict, conflict.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, reopen.Error?.Kind);
        Assert.Equal(5, history.Count);
        Assert.Equal(content.Scope, history[0].Content.Scope);
        Assert.Equal("Revised scope", history[^1].Content.Scope);
        Assert.Equal("closed", hydrated.Engagement(engagementId)!.Status);
        Assert.All(history, version => Assert.False(version.ProfessionalAccessGranted));
        Assert.All(history[^1].Staff, proposal =>
        {
            Assert.Equal("withdrawn", proposal.ProposalState);
            Assert.False(proposal.IsCurrent);
            Assert.False(proposal.ProfessionalAccessGranted);
        });
        Assert.Empty(new AggregateScenario<IndependenceLedger>(hydrated).PendingEvents);
    }

    [Fact]
    public void ShouldSerializeDraftAndServiceFactsGivenSharedClientSequence()
    {
        // Arrange
        var ledger = new IndependenceLedger(Tenant);
        var lead = Staff("advisory");
        Assert.True(ledger.CreateEngagement(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 0,
            Content(lead), lead, Actor, Now).IsSuccess);
        var service = new NonattestServiceContent(Uuid.CreateVersion4(), "readiness",
            new DateOnly(2026, 1, 1), null, [lead.StaffMemberId], false, "Synthetic client source");

        // Act
        var result = ledger.RecordService(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 0, service, Actor, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(1, ledger.Sequence);
        Assert.Empty(ledger.History().Services);
        Assert.Single(ledger.Engagements);
    }

    [Theory]
    [InlineData(false, "advisory")]
    [InlineData(true, "attest")]
    public void ShouldRefuseTentativeProposalGivenInactiveOrMismatchedDirectoryPractice(bool isActive, string practice)
    {
        // Arrange
        var ledger = new IndependenceLedger(Tenant);
        var lead = Staff("advisory");
        var engagementId = Uuid.CreateVersion4();
        Assert.True(ledger.CreateEngagement(Uuid.CreateVersion4(), engagementId, 0,
            Content(lead), lead, Actor, Now).IsSuccess);
        var invalid = Staff(practice) with { IsActive = isActive };

        // Act
        var result = ledger.ProposeEngagementStaff(Uuid.CreateVersion4(), engagementId, 1, invalid, Actor, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal(1, ledger.Sequence);
        Assert.Single(ledger.Engagement(engagementId)!.Staff);
    }

    [Fact]
    public void ShouldRefuseCrossTenantDraftReplayGivenForeignClientEvents()
    {
        // Arrange
        var source = new IndependenceLedger(Tenant);
        var lead = Staff("advisory");
        Assert.True(source.CreateEngagement(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 0,
            Content(lead), lead, Actor, Now).IsSuccess);
        var events = new AggregateScenario<IndependenceLedger>(source).PendingEvents.ToArray();

        // Act
        var replay = () => new AggregateScenario<IndependenceLedger>(new IndependenceLedger(Uuid.CreateVersion4())).Given(events);

        // Assert
        Assert.Throws<InvalidOperationException>(replay);
    }

    [Theory]
    [InlineData("empty_engagement")]
    [InlineData("initial_closed")]
    [InlineData("staff_actor")]
    [InlineData("staff_time")]
    [InlineData("empty_user")]
    [InlineData("invalid_period")]
    [InlineData("nonmember_actor")]
    [InlineData("missing_lead")]
    public void ShouldRejectCorruptedDraftGivenMalformedReplay(string corruption)
    {
        // Arrange
        var source = new IndependenceLedger(Tenant);
        var lead = Staff("advisory");
        Assert.True(source.CreateEngagement(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 0,
            Content(lead), lead, Actor, Now).IsSuccess);
        var ev = Assert.IsType<ServiceEngagementMutationRecorded>(Assert.Single(
            new AggregateScenario<IndependenceLedger>(source).PendingEvents));
        var view = ev.Engagement;
        view = corruption switch
        {
            "empty_engagement" => view with { EngagementId = Uuid.Empty },
            "initial_closed" => view with { Status = "closed", Staff = [view.Staff[0] with { IsCurrent = false, ProposalState = "withdrawn" }] },
            "staff_actor" => view with { Staff = [view.Staff[0] with { Actor = ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Synthetic operator") }] },
            "staff_time" => view with { Staff = [view.Staff[0] with { RecordedAt = default }] },
            "empty_user" => view with { Staff = [view.Staff[0] with { UserId = Uuid.Empty }] },
            "invalid_period" => view with { Content = view.Content with { PeriodStart = DateOnly.MinValue } },
            "nonmember_actor" => view with { Actor = ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Synthetic operator") },
            _ => view with { Staff = [] }
        };

        // Act
        var replay = () => new AggregateScenario<IndependenceLedger>(new IndependenceLedger(Tenant)).Given(
            ev with { Engagement = view });

        // Assert
        Assert.Throws<InvalidOperationException>(replay);
    }

    [Fact]
    public void ShouldRejectReopeningGivenClosedDraftEventFollowedByCorruptedReplay()
    {
        // Arrange
        var source = new IndependenceLedger(Tenant);
        var lead = Staff("advisory");
        var engagementId = Uuid.CreateVersion4();
        Assert.True(source.CreateEngagement(Uuid.CreateVersion4(), engagementId, 0,
            Content(lead), lead, Actor, Now).IsSuccess);
        Assert.True(source.CloseEngagement(Uuid.CreateVersion4(), engagementId, 1,
            "Canceled draft", Actor, Now).IsSuccess);
        var events = new AggregateScenario<IndependenceLedger>(source).PendingEvents.ToArray();
        var first = Assert.IsType<ServiceEngagementMutationRecorded>(events[0]);
        var invalid = first with
        {
            RequestId = Uuid.CreateVersion4(),
            ExpectedSequence = 2,
            Engagement = first.Engagement with { Revision = 3 }
        };

        // Act
        var replay = () => new AggregateScenario<IndependenceLedger>(new IndependenceLedger(Tenant)).Given(
            events.Append(invalid).ToArray());

        // Assert
        Assert.Throws<InvalidOperationException>(replay);
    }

    static FirmStaffMemberView Staff(string practice) => new(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
        practice, "Synthetic directory source", true, 1,
        ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Synthetic operator"), Now);
    static ServiceEngagementDraftContent Content(FirmStaffMemberView lead) => new(lead.Practice,
        "Synthetic engagement scope", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), lead.StaffMemberId);
}
