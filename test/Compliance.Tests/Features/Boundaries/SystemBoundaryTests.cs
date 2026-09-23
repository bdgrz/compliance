using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Boundaries;

public sealed class SystemBoundaryTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid BoundaryId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid VersionId = Uuid.CreateVersion4();
    static readonly Uuid EntryId = Uuid.CreateVersion4();
    static readonly Uuid AuthorId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldReturnDomainFailureGivenBoundaryRevisionPrecedence()
    {
        // Arrange
        var boundary = new SystemBoundary(TenantId, BoundaryId);
        var invalid = Content() with { Statement = "" };

        // Act
        var missing = boundary.Revise(VersionId, 1, invalid, AuthorId, "Author", Now);
        Assert.True(boundary.Create(ProgramId, VersionId, Content(), AuthorId,
            "Author", Now).IsSuccess);
        var stale = boundary.Revise(VersionId, 2, invalid, AuthorId, "Author", Now);
        var invalidCurrent = boundary.Revise(VersionId, 1, invalid, AuthorId, "Author", Now);

        // Assert
        AssertFailure(missing, CommandFailureCode.MissingRecord);
        var staleDraft = AssertFailure(stale, CommandFailureCode.VersionConflict);
        Assert.Equal(VersionConflictCode.StaleDraft, staleDraft.Version?.Code);
        Assert.Equal(VersionId, staleDraft.Version?.CurrentVersionId);
        Assert.Equal(1, staleDraft.Version?.CurrentRevision);
        Assert.Contains("statement", AssertFailure(invalidCurrent,
            CommandFailureCode.InvalidContent).Message, StringComparison.Ordinal);
        Assert.Single(new AggregateScenario<SystemBoundary>(boundary).PendingEvents);
    }

    [Fact]
    public void ShouldRequireExactVersionAndRevisionGivenDraftChange()
    {
        // Arrange
        var boundary = new SystemBoundary(TenantId, BoundaryId);

        // Act
        var first = Content();

        // Assert
        Assert.True(boundary.Create(ProgramId, VersionId, first, AuthorId, "Lead A", Now).IsSuccess);

        var second = first with { Statement = "Revised scope" };
        Assert.Null(boundary.Revise(VersionId, 1, second, AuthorId, "Lead B",
            Now.AddMinutes(1)));
        var stale = boundary.Revise(VersionId, 1, first, AuthorId, "Lead A", Now);
        var wrongVersion = boundary.Revise(Uuid.CreateVersion4(), 2, first,
            AuthorId, "Lead A", Now);

        var staleDraft = AssertFailure(stale, CommandFailureCode.VersionConflict);
        Assert.Equal(2, staleDraft.Version?.CurrentRevision);
        Assert.Equal(VersionId, staleDraft.Version?.CurrentVersionId);
        AssertFailure(wrongVersion, CommandFailureCode.VersionConflict);
        Assert.Collection(new AggregateScenario<SystemBoundary>(boundary).PendingEvents,
            ev =>
            {
                Assert.Equal(typeof(BoundaryDraftCreated), ev.GetType());
                Assert.Equal(VersionId, ev.GetType().GetProperty("DraftVersionId")?.GetValue(ev));
            },
            ev =>
            {
                Assert.Equal(typeof(BoundaryDraftRevised), ev.GetType());
                Assert.Equal(2L, ev.GetType().GetProperty("Revision")?.GetValue(ev));
                Assert.Equal("Lead B", ev.GetType().GetProperty("AuthorDisplay")?.GetValue(ev));
            });
    }

    [Fact]
    public void ShouldRequireOwnerAndRationaleGivenScopeEntries()
    {
        // Arrange
        var boundary = new SystemBoundary(TenantId, BoundaryId);
        var invalid = Content() with
        {
            Entries = [new BoundaryScopeEntry(Uuid.CreateVersion4(), "inclusion", "service",
                "Service A", null, "", "", true)],
        };


        // Act
        var result = boundary.Create(ProgramId, VersionId, invalid, AuthorId, "Lead", Now);


        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(new AggregateScenario<SystemBoundary>(boundary).PendingEvents);
    }

    [Fact]
    public void ShouldRequireSecurityCategoryGivenOptionalCategoriesOnly()
    {
        // Arrange
        var boundary = new SystemBoundary(TenantId, BoundaryId);
        var invalid = Content() with { TrustServicesCategories = ["availability", "privacy"] };


        // Act
        var result = boundary.Create(ProgramId, VersionId, invalid, AuthorId, "Lead", Now);


        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Validation, error.Kind);
        Assert.Contains("security", error.Message);
        Assert.Empty(new AggregateScenario<SystemBoundary>(boundary).PendingEvents);
    }

    [Fact]
    public void ShouldRequireSecurityCategoryGivenRevisionOrSuccessor()
    {
        // Arrange
        var boundary = new SystemBoundary(TenantId, BoundaryId);
        var reviewer = Uuid.CreateVersion4();
        var decisionId = Uuid.CreateVersion4();
        var withoutSecurity = Content() with { TrustServicesCategories = ["confidentiality"] };
        Assert.True(boundary.Create(ProgramId, VersionId, Content(), AuthorId, "Author", Now).IsSuccess);


        // Act
        var revision = boundary.Revise(VersionId, 1, withoutSecurity, AuthorId, "Author", Now);
        Assert.Null(boundary.Review(VersionId, 1, decisionId, "accept", "Reviewed",
            reviewer, "Reviewer", Now));
        Assert.Null(boundary.Approve(VersionId, 1, Uuid.CreateVersion4(), decisionId,
            new DateOnly(2027, 1, 1), "Approved", "digest", reviewer, "Reviewer", Now));
        var successor = boundary.ProposeSuccessor(VersionId, Uuid.CreateVersion4(),
            withoutSecurity, AuthorId, "Author", Now);


        // Assert
        AssertFailure(revision, CommandFailureCode.InvalidContent);
        AssertFailure(successor, CommandFailureCode.InvalidContent);
        Assert.Equal(3, new AggregateScenario<SystemBoundary>(boundary).PendingEvents.Count);
    }

    [Fact]
    public void ShouldRejectApprovalGivenLegacyDraftWithoutSecurityCategory()
    {
        // Arrange
        var boundary = new SystemBoundary(TenantId, BoundaryId);
        var reviewer = Uuid.CreateVersion4();
        var decisionId = Uuid.CreateVersion4();
        var legacy = Content() with { TrustServicesCategories = ["availability"] };
        new AggregateScenario<SystemBoundary>(boundary).Given(
            DomainEventSeed.Attach(new BoundaryDraftCreated(TenantId, BoundaryId, ProgramId,
                VersionId, legacy, AuthorId, "Author", Now), BoundaryId, 1),
            DomainEventSeed.Attach(new BoundaryReviewed(TenantId, BoundaryId, VersionId, 1,
                decisionId, "accept", reviewer, "Reviewer", "Reviewed", Now), BoundaryId, 2));


        // Act
        var result = boundary.Approve(VersionId, 1, Uuid.CreateVersion4(), decisionId,
            new DateOnly(2027, 1, 1), "Approved", "digest", reviewer, "Reviewer", Now);


        // Assert
        var error = AssertFailure(result, CommandFailureCode.InvalidContent);
        Assert.Contains("security", error.Message);
        Assert.Empty(new AggregateScenario<SystemBoundary>(boundary).PendingEvents);
    }

    [Fact]
    public void ShouldReturnExistingIdentityGivenReplayedCreate()
    {
        // Arrange
        var boundary = new SystemBoundary(TenantId, BoundaryId);

        // Act
        var content = Content();

        // Assert
        Assert.True(boundary.Create(ProgramId, VersionId, content, AuthorId,
            "Lead", Now).IsSuccess);

        var replay = boundary.Create(ProgramId, VersionId, Content(), AuthorId,
            "Lead", Now.AddMinutes(1));
        var changed = boundary.Create(ProgramId, VersionId,
            content with { Statement = "Other scope" }, AuthorId, "Lead", Now);

        Assert.Equal(VersionId, replay.Value.DraftVersionId);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(changed.Error).Kind);
        Assert.Single(new AggregateScenario<SystemBoundary>(boundary).PendingEvents);
    }

    [Fact]
    public void ShouldPreserveCreateReplayGivenRevisedDraft()
    {
        // Arrange
        var boundary = new SystemBoundary(TenantId, BoundaryId);
        var original = Content();
        Assert.True(boundary.Create(ProgramId, VersionId, original, AuthorId,
            "Lead", Now).IsSuccess);
        Assert.Null(boundary.Revise(VersionId, 1, original with { Statement = "Revised" },
            AuthorId, "Lead", Now.AddMinutes(1)));

        // Act
        var replay = boundary.Create(ProgramId, VersionId, original, AuthorId,
            "Lead", Now.AddMinutes(2));
        var changed = boundary.Create(ProgramId, VersionId,
            original with { Statement = "Other scope" }, AuthorId, "Lead", Now.AddMinutes(2));

        // Assert
        Assert.Equal(VersionId, replay.Value.DraftVersionId);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(changed.Error).Kind);
        Assert.Equal(2, new AggregateScenario<SystemBoundary>(boundary).PendingEvents.Count);
    }

    [Fact]
    public void ShouldBindRevisionAndDenyAuthorApprovalGivenReview()
    {
        // Arrange
        var boundary = new SystemBoundary(TenantId, BoundaryId);
        Assert.True(boundary.Create(ProgramId, VersionId, Content(), AuthorId,
            "Author", Now).IsSuccess);
        var reviewer = Uuid.CreateVersion4();
        var decisionId = Uuid.CreateVersion4();

        // Act
        var selfReview = boundary.Review(VersionId, 1, Uuid.CreateVersion4(),
            "accept", "Looks complete", AuthorId, "Author", Now);

        // Assert
        AssertFailure(selfReview, CommandFailureCode.ActorProhibited,
            "A boundary author cannot review their own draft.");
        Assert.Null(boundary.Review(VersionId, 1, decisionId,
            "accept", "Scope is justified", reviewer, "Reviewer", Now));
        var selfApproval = boundary.Approve(VersionId, 1, Uuid.CreateVersion4(), decisionId,
            new DateOnly(2027, 1, 1), "Approved", "digest", AuthorId, "Author", Now);
        AssertFailure(selfApproval, CommandFailureCode.ActorProhibited,
            "A boundary author cannot approve their own draft.");

        Assert.Null(boundary.Revise(VersionId, 1, Content() with
        {
            Statement = "Changed after review",
        }, AuthorId, "Author", Now.AddMinutes(1)));
        var staleApproval = boundary.Approve(VersionId, 2, Uuid.CreateVersion4(), decisionId,
            new DateOnly(2027, 1, 1), "Approved", "digest", reviewer, "Reviewer", Now);
        AssertFailure(staleApproval, CommandFailureCode.StateConflict,
            "Approval requires the latest accepted review of this draft revision.");
        Assert.Equal(3, new AggregateScenario<SystemBoundary>(boundary).PendingEvents.Count);
    }

    [Fact]
    public void ShouldPreserveApprovedVersionAndRequireLaterDateGivenSuccessor()
    {
        // Arrange
        var boundary = new SystemBoundary(TenantId, BoundaryId);
        var reviewer = Uuid.CreateVersion4();

        // Act
        var decisionId = Uuid.CreateVersion4();

        // Assert
        Assert.True(boundary.Create(ProgramId, VersionId, Content(), AuthorId,
            "Author", Now).IsSuccess);
        Assert.Null(boundary.Review(VersionId, 1, decisionId, "accept",
            "Reviewed", reviewer, "Reviewer", Now));
        Assert.Null(boundary.Approve(VersionId, 1, Uuid.CreateVersion4(), decisionId,
            new DateOnly(2027, 1, 1), "Approved", "digest", reviewer, "Reviewer", Now));
        AssertFailure(boundary.Revise(VersionId, 1, Content(), AuthorId, "Author", Now),
            CommandFailureCode.StateConflict,
            "The approved boundary is immutable. Propose a successor draft.");

        var successorId = Uuid.CreateVersion4();
        Assert.Null(boundary.ProposeSuccessor(VersionId, successorId,
            Content() with { Statement = "Successor" }, AuthorId, "Author", Now));
        var nextDecisionId = Uuid.CreateVersion4();
        Assert.Null(boundary.Review(successorId, 1, nextDecisionId, "accept",
            "Reviewed", reviewer, "Reviewer", Now));
        var overlap = boundary.Approve(successorId, 1, Uuid.CreateVersion4(), nextDecisionId,
            new DateOnly(2027, 1, 1), "Approved", "digest", reviewer, "Reviewer", Now);
        AssertFailure(overlap, CommandFailureCode.InvalidContent,
            "A successor must become effective after the previous approved version.");
        Assert.Null(boundary.Approve(successorId, 1, Uuid.CreateVersion4(), nextDecisionId,
            new DateOnly(2027, 2, 1), "Approved", "digest", reviewer, "Reviewer", Now));

        var staleSuccessor = boundary.ProposeSuccessor(VersionId, Uuid.CreateVersion4(),
            Content() with { Statement = "Stale successor" }, AuthorId, "Author", Now);
        var staleVersion = AssertFailure(staleSuccessor,
            CommandFailureCode.VersionConflict);
        Assert.Equal(successorId, staleVersion.Version?.CurrentVersionId);
    }

    [Fact]
    public void ShouldAllowDiscardOnlyGivenNeverReviewedDraft()
    {
        // Arrange
        var boundary = new SystemBoundary(TenantId, BoundaryId);
        Assert.True(boundary.Create(ProgramId, VersionId, Content(), AuthorId,
            "Author", Now).IsSuccess);
        var reviewedBy = Uuid.CreateVersion4();
        Assert.Null(boundary.Review(VersionId, 1, Uuid.CreateVersion4(),
            "request_changes", "Revise the scope", reviewedBy, "Reviewer", Now));

        // Act
        var denied = boundary.DiscardDraft(VersionId, 1, "Withdraw",
            AuthorId, "Author", Now);

        // Assert
        var referenced = AssertFailure(denied, CommandFailureCode.VersionConflict);
        Assert.Equal(VersionConflictCode.ReferencedDraft, referenced.Version?.Code);
        Assert.Equal(2, new AggregateScenario<SystemBoundary>(boundary).PendingEvents.Count);

        var unused = new SystemBoundary(TenantId, Uuid.CreateVersion4());
        var unusedVersion = Uuid.CreateVersion4();
        Assert.True(unused.Create(ProgramId, unusedVersion, Content(),
            AuthorId, "Author", Now).IsSuccess);
        Assert.Null(unused.DiscardDraft(unusedVersion, 1, "Not the right scope",
            AuthorId, "Author", Now));
        Assert.Equal(2, new AggregateScenario<SystemBoundary>(unused).PendingEvents.Count);
        AssertFailure(unused.Revise(unusedVersion, 1, Content(), AuthorId, "Author", Now),
            CommandFailureCode.StateConflict,
            "The approved boundary is immutable. Propose a successor draft.");
    }

    [Fact]
    public void ShouldPrioritizeVersionConflictGivenReviewedDraftAndOpenSuccessor()
    {
        // Arrange
        var boundary = new SystemBoundary(TenantId, BoundaryId);
        var reviewerId = Uuid.CreateVersion4();
        var reviewId = Uuid.CreateVersion4();
        Assert.True(boundary.Create(ProgramId, VersionId, Content(), AuthorId,
            "Author", Now).IsSuccess);
        Assert.Null(boundary.Review(VersionId, 1, reviewId, "accept", "Reviewed",
            reviewerId, "Reviewer", Now));

        // Act
        var staleDiscard = boundary.DiscardDraft(VersionId, 2, "", AuthorId, "Author", Now);
        var referencedDiscard = boundary.DiscardDraft(VersionId, 1, "", AuthorId,
            "Author", Now);
        Assert.Null(boundary.Approve(VersionId, 1, Uuid.CreateVersion4(), reviewId,
            new DateOnly(2027, 1, 1), "Approved", "digest", reviewerId, "Reviewer", Now));
        var successorId = Uuid.CreateVersion4();
        Assert.Null(boundary.ProposeSuccessor(VersionId, successorId, Content(), AuthorId,
            "Author", Now));
        var staleSuccessor = boundary.ProposeSuccessor(Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), Content() with { Statement = "" }, AuthorId, "Author", Now);

        // Assert
        Assert.Equal(VersionConflictCode.StaleDraft, AssertFailure(staleDiscard,
            CommandFailureCode.VersionConflict).Version?.Code);
        Assert.Equal(VersionConflictCode.ReferencedDraft, AssertFailure(referencedDiscard,
            CommandFailureCode.VersionConflict).Version?.Code);
        var staleVersion = AssertFailure(staleSuccessor,
            CommandFailureCode.VersionConflict);
        Assert.Equal(VersionId, staleVersion.Version?.CurrentVersionId);
        Assert.Equal(4, new AggregateScenario<SystemBoundary>(boundary).PendingEvents.Count);
    }

    static CommandFailure AssertFailure(CommandFailure? actual, CommandFailureCode expected,
        string? message = null)
    {
        var failure = Assert.IsType<CommandFailure>(actual);
        Assert.Equal(expected, failure.Code);
        if (message is not null)
            Assert.Equal(message, failure.Message);
        return failure;
    }

    static BoundaryContent Content() => new("The in-scope service and its dependencies.",
        "readiness", ["security"],
        [new BoundaryScopeEntry(EntryId, "inclusion", "service",
            "Service A", null, "Compliance lead",
            "The service handles customer work.", true)]);
}
