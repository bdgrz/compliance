using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class EngagementManagementAcknowledgementTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid User = Uuid.CreateVersion4();
    static readonly ActorReference Actor = ActorReference.ForMember(RbacIds.Member(Tenant, User), "Synthetic client administrator");
    static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldRetainPersonalAcknowledgementGivenExactDraftAndCompleteServiceFacts()
    {
        // Arrange
        var (ledger, engagement) = Draft();
        var request = new AcknowledgeEngagementManagement(Tenant, engagement, Uuid.CreateVersion4(),
            1, 1, [], "I retain responsibility for management decisions and oversight.");
        var requestId = Uuid.CreateVersion4();

        // Act
        var result = ledger.AcknowledgeManagement(requestId, request, User, Actor, Now);
        var events = new AggregateScenario<IndependenceLedger>(ledger).PendingEvents.ToArray();
        var replay = new AggregateScenario<IndependenceLedger>(new IndependenceLedger(Tenant)).Given(events).Aggregate;
        var retry = replay.AcknowledgeManagement(requestId, request, User, Actor, Now.AddDays(1));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.Equal(Now, retry.Value!.RecordedAt);
        Assert.Equal(User, Assert.Single(replay.ManagementAcknowledgements(engagement)).UserId);
        Assert.Equal(2, replay.Sequence);
        Assert.False(replay.Engagement(engagement)!.ProfessionalAccessGranted);
    }

    [Theory]
    [InlineData("revision")]
    [InlineData("facts")]
    [InlineData("identity")]
    public void ShouldRefuseAcknowledgementGivenStaleFactsOrMisattributedMember(string condition)
    {
        // Arrange
        var (ledger, engagement) = Draft();
        var request = new AcknowledgeEngagementManagement(Tenant, engagement, Uuid.CreateVersion4(),
            1, condition == "revision" ? 2 : 1, condition == "facts" ? [Uuid.CreateVersion4()] : [], "I retain management responsibility.");

        // Act
        var result = ledger.AcknowledgeManagement(Uuid.CreateVersion4(), request,
            condition == "identity" ? Uuid.CreateVersion4() : User, Actor, Now);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(ledger.ManagementAcknowledgements(engagement));
        Assert.Equal(1, ledger.Sequence);
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("user")]
    [InlineData("facts")]
    [InlineData("revision")]
    public void ShouldRejectMisboundAcknowledgementGivenCorruptedReplay(string corruption)
    {
        // Arrange
        var (ledger, engagement) = Draft();
        var request = new AcknowledgeEngagementManagement(Tenant, engagement, Uuid.CreateVersion4(),
            1, 1, [], "I retain management responsibility.");
        Assert.True(ledger.AcknowledgeManagement(Uuid.CreateVersion4(), request, User, Actor, Now).IsSuccess);
        var events = new AggregateScenario<IndependenceLedger>(ledger).PendingEvents.ToArray();
        var recorded = Assert.IsType<EngagementManagementAcknowledged>(events[1]);
        var ack = recorded.Acknowledgement;
        ack = corruption switch
        {
            "actor" => ack with { Actor = ActorReference.ForPlatformOperator(User, "Synthetic operator") },
            "user" => ack with { UserId = Uuid.CreateVersion4() },
            "facts" => ack with { CompleteServiceRecordIds = [Uuid.CreateVersion4()] },
            _ => ack with { EngagementRevision = 2 }
        };

        // Act
        var replay = () => new AggregateScenario<IndependenceLedger>(new IndependenceLedger(Tenant)).Given(
            events[0], recorded with { Acknowledgement = ack });

        // Assert
        Assert.Throws<InvalidOperationException>(replay);
    }

    [Fact]
    public void ShouldRejectOldAcknowledgementIntentGivenNewServiceRecordedAfterDraft()
    {
        // Arrange
        var (ledger, engagement) = Draft();
        var serviceId = Uuid.CreateVersion4();
        Assert.True(ledger.RecordService(Uuid.CreateVersion4(), serviceId, 1,
            new NonattestServiceContent(Uuid.CreateVersion4(), "readiness", new DateOnly(2026, 1, 1), null,
                [Uuid.CreateVersion4()], false, "Synthetic source"), Actor, Now).IsSuccess);
        var incomplete = new AcknowledgeEngagementManagement(Tenant, engagement, Uuid.CreateVersion4(),
            2, 1, [], "I retain management responsibility.");

        // Act
        var result = ledger.AcknowledgeManagement(Uuid.CreateVersion4(), incomplete, User, Actor, Now);
        var complete = ledger.AcknowledgeManagement(Uuid.CreateVersion4(), incomplete with
        { CompleteServiceRecordIds = [serviceId] }, User, Actor, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.True(complete.IsSuccess);
        Assert.Equal(serviceId, Assert.Single(complete.Value!.CompleteServiceRecordIds));
    }

    static (IndependenceLedger Ledger, Uuid Engagement) Draft()
    {
        var ledger = new IndependenceLedger(Tenant);
        var staff = new FirmStaffMemberView(Uuid.CreateVersion4(), Uuid.CreateVersion4(), "attest",
            "Synthetic directory source", true, 1, ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Synthetic operator"), Now);
        var engagement = Uuid.CreateVersion4();
        Assert.True(ledger.CreateEngagement(Uuid.CreateVersion4(), engagement, 0,
            new ServiceEngagementDraftContent("attest", "Synthetic scope", new DateOnly(2026, 1, 1), null, staff.StaffMemberId),
            staff, Actor, Now).IsSuccess);
        return (ledger, engagement);
    }
}
