using System.Text.Json;
using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Policies;

public sealed class PolicyLifecycleTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid AuthorId = Uuid.CreateVersion4();
    static readonly Uuid ReviewerId = Uuid.CreateVersion4();
    static readonly Uuid ApproverId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
    static readonly DateOnly January = new(2027, 1, 1);

    static PolicyContent Content(string title = "Access Control Policy", string? body = "Body",
        IReadOnlyList<PolicyApplicabilityReference>? applicability = null,
        string audience = PolicyAudience.CoreSecurity, IReadOnlyList<string>? teams = null,
        int cadence = 12, string? owner = "Security lead") =>
        new(title, "Govern access", audience, teams, cadence, body, null, owner,
            applicability ?? [new("system_instance", "AWS", Uuid.CreateVersion5(TenantId, "aws"))]);

    static ActorReference Actor(Uuid memberId) =>
        ActorReference.ForMember(memberId, memberId.ToString());

    static Policy Draft(PolicyContent? content = null)
    {
        var policy = new Policy(TenantId, Policy.IdFor(TenantId, ProgramId, "POL-AC"));
        Assert.True(policy.Create(ProgramId, Uuid.CreateVersion4(), " pol-ac ",
            content ?? Content(), Actor(AuthorId), AuthorId, Now).IsSuccess);
        return policy;
    }

    static Uuid Accept(Policy policy, Uuid reviewer, DateTimeOffset? at = null)
    {
        var decisionId = Uuid.CreateVersion4();
        Assert.Null(policy.Review(ProgramId, policy.Revision, decisionId, "accept", "Reviewed",
            Actor(reviewer), reviewer, at ?? Now.AddMinutes(1)));
        return decisionId;
    }

    static void Approve(Policy policy, DateOnly effectiveFrom, string? digest = null,
        bool major = true, DateTimeOffset? at = null)
    {
        var review = Accept(policy, ReviewerId, at);
        Assert.Null(policy.Approve(ProgramId, policy.Revision, Uuid.CreateVersion4(), review,
            effectiveFrom, major, "Approved", digest, Actor(ApproverId), ApproverId,
            at ?? Now.AddMinutes(2)));
    }

    [Fact]
    public void ShouldCreateImmutableVersionGivenIndependentReviewAndApproval()
    {
        // Arrange
        var policy = Draft();

        // Act
        Approve(policy, January);

        // Assert
        var version = Assert.IsType<PolicyVersionView>(policy.CurrentVersion);
        Assert.Equal(1, version.Version);
        Assert.True(version.Major);
        Assert.Equal("approved", version.Status);
        Assert.Equal(Policy.Hash(version.Content), version.ContentSha256);
        Assert.Equal("POL-AC", version.Identifier);
        Assert.Equal("approved", policy.Status);
        Assert.Null(policy.PendingStatus);
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(
            policy.Revise(ProgramId, policy.Revision, Content("Changed"), Actor(AuthorId),
                AuthorId, Now.AddMinutes(3))).Code);
        Assert.Equal(["review", "approval"], policy.ReadDecisions().Select(static d => d.Kind));
    }

    [Fact]
    public void ShouldRejectReviewGivenReviewerAuthoredRevision()
    {
        // Arrange
        var policy = Draft();
        Assert.Null(policy.Revise(ProgramId, 1, Content("Edited"), Actor(ReviewerId), ReviewerId,
            Now.AddMinutes(1)));

        // Act
        var selfReview = policy.Review(ProgramId, 2, Uuid.CreateVersion4(), "accept", "Mine",
            Actor(ReviewerId), ReviewerId, Now.AddMinutes(2));
        var originalAuthor = policy.Review(ProgramId, 2, Uuid.CreateVersion4(), "accept",
            "Mine", Actor(AuthorId), AuthorId, Now.AddMinutes(2));

        // Assert
        Assert.Equal(CommandFailureCode.ActorProhibited,
            Assert.IsType<CommandFailure>(selfReview).Code);
        Assert.Equal(CommandFailureCode.ActorProhibited,
            Assert.IsType<CommandFailure>(originalAuthor).Code);
    }

    [Fact]
    public void ShouldRejectApprovalGivenApproverAuthoredOrReviewedRevision()
    {
        // Arrange
        var policy = Draft();
        var review = Accept(policy, ReviewerId);

        // Act
        var byReviewer = policy.Approve(ProgramId, 1, Uuid.CreateVersion4(), review, January,
            true, "Approve", null, Actor(ReviewerId), ReviewerId, Now.AddMinutes(2));
        var byAuthor = policy.Approve(ProgramId, 1, Uuid.CreateVersion4(), review, January,
            true, "Approve", null, Actor(AuthorId), AuthorId, Now.AddMinutes(2));
        var wrongReview = policy.Approve(ProgramId, 1, Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), January, true, "Approve", null, Actor(ApproverId), ApproverId,
            Now.AddMinutes(2));

        // Assert
        Assert.Equal(CommandFailureCode.ActorProhibited, Assert.IsType<CommandFailure>(byReviewer).Code);
        Assert.Equal(CommandFailureCode.ActorProhibited, Assert.IsType<CommandFailure>(byAuthor).Code);
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(wrongReview).Code);
        Assert.Null(policy.CurrentVersion);
        Assert.Equal("awaiting_approval", policy.PendingStatus);
    }

    [Fact]
    public void ShouldAllowAuthorApprovalGivenExactPolicyWaiver()
    {
        // Arrange
        var policy = Draft();
        var review = Accept(policy, ReviewerId);
        var scope = new SeparationOfDutiesWaiverScope(SeparationOfDutiesRecordTypes.Policy,
            policy.Id, policy.Id, 1, SeparationOfDutiesActions.Approve);
        var wrongAction = CreateWaiver(scope with { Action = SeparationOfDutiesActions.Review },
            AuthorId);
        var valid = CreateWaiver(scope, AuthorId);

        // Act
        var denied = policy.Approve(ProgramId, 1, Uuid.CreateVersion4(), review, January, true,
            "Approve", null, Actor(AuthorId), AuthorId, Now.AddMinutes(5), wrongAction);
        var allowed = policy.Approve(ProgramId, 1, Uuid.CreateVersion4(), review, January, true,
            "Approve", null, Actor(AuthorId), AuthorId, Now.AddMinutes(5), valid);

        // Assert
        Assert.Equal(CommandFailureCode.ActorProhibited, Assert.IsType<CommandFailure>(denied).Code);
        Assert.Null(allowed);
        Assert.Equal(valid.Id, policy.CurrentVersion!.SeparationOfDutiesWaiverId);
    }

    [Fact]
    public void ShouldReturnVersionEffectiveOnSelectedDateGivenApprovedSuccessor()
    {
        // Arrange
        var policy = Draft();
        Approve(policy, January);
        Assert.Null(policy.ProposeSuccessor(ProgramId, 1, Content("Access Control Policy v2"),
            Actor(AuthorId), AuthorId, Now.AddMinutes(10)));
        var july = new DateOnly(2027, 7, 1);

        // Act
        Approve(policy, july, "digest", false, Now.AddMinutes(11));

        // Assert
        Assert.Null(policy.EffectiveVersion(January.AddDays(-1)));
        Assert.Equal(1, policy.EffectiveVersion(new DateOnly(2027, 6, 30))!.Version);
        Assert.Equal(2, policy.EffectiveVersion(july)!.Version);
        var first = policy.FindVersion(1)!;
        Assert.Equal("superseded", first.Status);
        Assert.Equal(july, first.EffectiveUntil);
        Assert.Equal("Access Control Policy", first.Content.Title);
        var second = policy.FindVersion(2)!;
        Assert.False(second.Major);
        Assert.Equal(1, second.PredecessorVersion);
        Assert.Equal(2, policy.ReadDecisions().Count(static d => d.Kind == "approval"));
    }

    [Fact]
    public void ShouldRejectSuccessorApprovalGivenMissingDigestOrEarlierEffectiveDate()
    {
        // Arrange
        var policy = Draft();
        Approve(policy, January);
        Assert.Null(policy.ProposeSuccessor(ProgramId, 1, Content("v2"), Actor(AuthorId),
            AuthorId, Now.AddMinutes(10)));
        var review = Accept(policy, ReviewerId, Now.AddMinutes(11));

        // Act
        var missingDigest = policy.Approve(ProgramId, policy.Revision, Uuid.CreateVersion4(),
            review, new DateOnly(2027, 7, 1), true, "Approve", null, Actor(ApproverId),
            ApproverId, Now.AddMinutes(12));
        var backdated = policy.Approve(ProgramId, policy.Revision, Uuid.CreateVersion4(), review,
            January, true, "Approve", "digest", Actor(ApproverId), ApproverId, Now.AddMinutes(12));
        var stale = policy.ProposeSuccessor(ProgramId, 1, Content("v3"), Actor(AuthorId),
            AuthorId, Now.AddMinutes(12));

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(missingDigest).Code);
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(backdated).Code);
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(stale).Code);
        Assert.Equal(1, policy.CurrentVersion!.Version);
    }

    [Fact]
    public void ShouldDeleteOnlyUnusedDraftGivenDiscardRequests()
    {
        // Arrange
        var unused = Draft(Content(applicability: []));
        var related = new Policy(TenantId, Uuid.CreateVersion4());
        Assert.True(related.Create(ProgramId, Uuid.CreateVersion4(), "POL-REL", Content(),
            Actor(AuthorId), AuthorId, Now).IsSuccess);
        var reviewed = new Policy(TenantId, Uuid.CreateVersion4());
        Assert.True(reviewed.Create(ProgramId, Uuid.CreateVersion4(), "POL-REV",
            Content(applicability: []), Actor(AuthorId), AuthorId, Now).IsSuccess);
        Accept(reviewed, ReviewerId);
        var approved = Draft();
        Approve(approved, January);

        // Act
        var deleted = unused.Discard(ProgramId, 1, "Never used", Actor(AuthorId), Now);
        var withRelationships = related.Discard(ProgramId, 1, "Unused", Actor(AuthorId), Now);
        var afterReview = reviewed.Discard(ProgramId, 1, "Unused", Actor(AuthorId), Now);
        var afterApproval = approved.Discard(ProgramId, approved.Revision, "Unused",
            Actor(AuthorId), Now);

        // Assert
        Assert.Null(deleted);
        Assert.False(unused.IsVisible);
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(withRelationships).Code);
        Assert.Equal(CommandFailureCode.VersionConflict, Assert.IsType<CommandFailure>(afterReview).Code);
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(afterApproval).Code);
    }

    [Fact]
    public void ShouldFlagOverdueReviewAndResetItGivenPeriodicReview()
    {
        // Arrange
        var policy = Draft(Content(cadence: 6));
        Approve(policy, January);
        var due = new DateOnly(2027, 3, 30);

        // Act
        var beforeDue = policy.ToView(due);
        var overdue = policy.ToView(due.AddDays(1));
        Assert.Null(policy.ConfirmPeriodicReview(ProgramId, 1, Uuid.CreateVersion4(),
            "Still accurate", Actor(ReviewerId), ReviewerId,
            new DateTimeOffset(2027, 4, 15, 9, 0, 0, TimeSpan.Zero)));
        var afterReview = policy.ToView(due.AddDays(1));

        // Assert
        Assert.Equal(due, beforeDue.NextReviewDueOn);
        Assert.False(beforeDue.ReviewOverdue);
        Assert.True(overdue.ReviewOverdue);
        Assert.False(afterReview.ReviewOverdue);
        Assert.Equal(new DateOnly(2027, 10, 15), afterReview.NextReviewDueOn);
        Assert.Equal(1, afterReview.CurrentVersion);
    }

    [Fact]
    public void ShouldRetireAndKeepHistoryGivenReviewedRetirementProposal()
    {
        // Arrange
        var policy = Draft();
        Approve(policy, January);
        var until = new DateOnly(2027, 9, 1);
        Assert.Null(policy.ProposeRetirement(ProgramId, 1, until, "Replaced by handbook",
            Actor(AuthorId), AuthorId, Now.AddMinutes(10)));
        var selfReview = policy.Review(ProgramId, policy.Revision, Uuid.CreateVersion4(),
            "accept", "Mine", Actor(AuthorId), AuthorId, Now.AddMinutes(11));
        var review = Accept(policy, ReviewerId, Now.AddMinutes(11));

        // Act
        var retired = policy.ApproveRetirement(ProgramId, policy.Revision, Uuid.CreateVersion4(),
            review, "Approved", Actor(ApproverId), ApproverId, Now.AddMinutes(12));

        // Assert
        Assert.Equal(CommandFailureCode.ActorProhibited, Assert.IsType<CommandFailure>(selfReview).Code);
        Assert.Null(retired);
        Assert.Equal("retired", policy.Status);
        var version = policy.FindVersion(1)!;
        Assert.Equal("retired", version.Status);
        Assert.Equal(until, version.EffectiveUntil);
        Assert.Equal(1, policy.EffectiveVersion(new DateOnly(2027, 8, 31))!.Version);
        Assert.Null(policy.EffectiveVersion(until));
        Assert.Null(policy.ToView(until.AddYears(1)).NextReviewDueOn);
    }

    [Fact]
    public void ShouldRejectStaleRevisionGivenConcurrentDraftEdits()
    {
        // Arrange
        var policy = Draft();
        Assert.Null(policy.Revise(ProgramId, 1, Content("First editor"), Actor(AuthorId),
            AuthorId, Now.AddMinutes(1)));

        // Act
        var second = policy.Revise(ProgramId, 1, Content("Second editor"), Actor(ReviewerId),
            ReviewerId, Now.AddMinutes(1));

        // Assert
        Assert.Equal(CommandFailureCode.VersionConflict, Assert.IsType<CommandFailure>(second).Code);
        Assert.Equal("First editor", policy.DraftContent!.Title);
    }

    [Theory]
    [InlineData(PolicyAudience.RoleTargeted, null, 12, "Body")]
    [InlineData(PolicyAudience.CoreSecurity, "Engineering", 12, "Body")]
    [InlineData("everyone", null, 12, "Body")]
    [InlineData(PolicyAudience.CoreSecurity, null, 0, "Body")]
    [InlineData(PolicyAudience.CoreSecurity, null, 12, null)]
    public void ShouldRejectDraftGivenInvalidAudienceCadenceOrContent(string audience,
        string? team, int cadence, string? body)
    {
        // Arrange
        var policy = new Policy(TenantId, Uuid.CreateVersion4());

        // Act
        var result = policy.Create(ProgramId, Uuid.CreateVersion4(), "POL-BAD",
            Content(body: body, audience: audience, teams: team is null ? null : [team],
                cadence: cadence), Actor(AuthorId), AuthorId, Now);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error.Kind);
    }

    [Fact]
    public void ShouldRejectDraftGivenBodyBeyondEventPayloadBound()
    {
        // Arrange
        var policy = new Policy(TenantId, Uuid.CreateVersion4());

        // Act
        var result = policy.Create(ProgramId, Uuid.CreateVersion4(), "POL-BIG",
            Content(body: new string('x', Policy.MaximumBodyLength + 1)), Actor(AuthorId),
            AuthorId, Now);

        // Assert
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void ShouldRejectApprovalGivenNoAccountableOwner()
    {
        // Arrange
        var policy = Draft(Content(owner: null));
        var review = Accept(policy, ReviewerId);

        // Act
        var failure = policy.Approve(ProgramId, 1, Uuid.CreateVersion4(), review, January, true,
            "Approve", null, Actor(ApproverId), ApproverId, Now.AddMinutes(2));

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(failure).Code);
    }

    [Fact]
    public void ShouldReplaySameVersionsAndDecisionsGivenRoundTrippedEvents()
    {
        // Arrange
        var source = Draft();
        Approve(source, January);
        Assert.Null(source.ProposeSuccessor(ProgramId, 1, Content("v2"), Actor(AuthorId),
            AuthorId, Now.AddMinutes(10)));
        Approve(source, new DateOnly(2027, 7, 1), "digest", false, Now.AddMinutes(11));
        var events = new AggregateScenario<Policy>(source).PendingEvents;
        var seeded = events.Select((ev, index) =>
            DomainEventSeed.Attach(RoundTrip(ev), source.Id, (ulong)index + 1)).ToArray();

        // Act
        var replayed = new AggregateScenario<Policy>(new Policy(TenantId, source.Id))
            .Given(seeded).Aggregate;

        // Assert
        Assert.Equal(Json(source.ReadVersions()), Json(replayed.ReadVersions()));
        Assert.Equal(Json(source.ReadDecisions()), Json(replayed.ReadDecisions()));
        Assert.Equal(Json(source.ToView(January)), Json(replayed.ToView(January)));
    }

    static string Json<T>(T value) => JsonSerializer.Serialize(value, typeof(T),
        ComplianceCoreJsonContext.Default);

    static DomainEvent RoundTrip(DomainEvent ev) => (DomainEvent)JsonSerializer.Deserialize(
        JsonSerializer.Serialize(ev, ev.GetType(), ComplianceCoreJsonContext.Default),
        ev.GetType(), ComplianceCoreJsonContext.Default)!;

    static SeparationOfDutiesWaiver CreateWaiver(SeparationOfDutiesWaiverScope scope,
        Uuid beneficiary)
    {
        var waiver = new SeparationOfDutiesWaiver(TenantId, Uuid.CreateVersion4());
        Assert.Null(waiver.Record(scope, beneficiary, Uuid.CreateVersion4(), "Requester",
            "No other qualified approver is available.", Now, Now.AddDays(1)));
        Assert.Null(waiver.Approve(Uuid.CreateVersion4(), "Approver", Now.AddMinutes(1)));
        return waiver;
    }
}
