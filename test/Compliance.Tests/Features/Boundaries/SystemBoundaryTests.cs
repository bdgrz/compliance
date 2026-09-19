using Bdgrz.Compliance.Features.Boundaries;
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
    public void ShouldRequireExactVersionAndRevisionGivenDraftChange()
    {
        // Arrange
        var boundary = new SystemBoundary(TenantId, BoundaryId);

        // Act
        var first = Content();

        // Assert
        Assert.True(boundary.Create(ProgramId, VersionId, first, AuthorId, "Lead A", Now).IsSuccess);

        var second = first with { Statement = "Revised scope" };
        Assert.True(boundary.Revise(VersionId, 1, second, AuthorId, "Lead B",
            Now.AddMinutes(1)).IsSuccess);
        var stale = boundary.Revise(VersionId, 1, first, AuthorId, "Lead A", Now);
        var wrongVersion = boundary.Revise(Uuid.CreateVersion4(), 2, first,
            AuthorId, "Lead A", Now);

        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(stale.Error).Kind);
        Assert.Contains("revision: 2", Assert.IsType<RequestError>(stale.Error).Message);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(wrongVersion.Error).Kind);
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
        Assert.Equal(RequestErrorKind.Forbidden,
            Assert.IsType<RequestError>(selfReview.Error).Kind);
        Assert.True(boundary.Review(VersionId, 1, decisionId,
            "accept", "Scope is justified", reviewer, "Reviewer", Now).IsSuccess);
        var selfApproval = boundary.Approve(VersionId, 1, Uuid.CreateVersion4(), decisionId,
            new DateOnly(2027, 1, 1), "Approved", "digest", AuthorId, "Author", Now);
        Assert.Equal(RequestErrorKind.Forbidden,
            Assert.IsType<RequestError>(selfApproval.Error).Kind);

        Assert.True(boundary.Revise(VersionId, 1, Content() with
        {
            Statement = "Changed after review",
        }, AuthorId, "Author", Now.AddMinutes(1)).IsSuccess);
        var staleApproval = boundary.Approve(VersionId, 2, Uuid.CreateVersion4(), decisionId,
            new DateOnly(2027, 1, 1), "Approved", "digest", reviewer, "Reviewer", Now);
        Assert.Equal(RequestErrorKind.Conflict,
            Assert.IsType<RequestError>(staleApproval.Error).Kind);
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
        Assert.True(boundary.Review(VersionId, 1, decisionId, "accept",
            "Reviewed", reviewer, "Reviewer", Now).IsSuccess);
        Assert.True(boundary.Approve(VersionId, 1, Uuid.CreateVersion4(), decisionId,
            new DateOnly(2027, 1, 1), "Approved", "digest", reviewer, "Reviewer", Now).IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict,
            Assert.IsType<RequestError>(boundary.Revise(VersionId, 1,
                Content(), AuthorId, "Author", Now).Error).Kind);

        var successorId = Uuid.CreateVersion4();
        Assert.True(boundary.ProposeSuccessor(VersionId, successorId,
            Content() with { Statement = "Successor" }, AuthorId, "Author", Now).IsSuccess);
        var nextDecisionId = Uuid.CreateVersion4();
        Assert.True(boundary.Review(successorId, 1, nextDecisionId, "accept",
            "Reviewed", reviewer, "Reviewer", Now).IsSuccess);
        var overlap = boundary.Approve(successorId, 1, Uuid.CreateVersion4(), nextDecisionId,
            new DateOnly(2027, 1, 1), "Approved", "digest", reviewer, "Reviewer", Now);
        Assert.Equal(RequestErrorKind.Validation,
            Assert.IsType<RequestError>(overlap.Error).Kind);
        Assert.True(boundary.Approve(successorId, 1, Uuid.CreateVersion4(), nextDecisionId,
            new DateOnly(2027, 2, 1), "Approved", "digest", reviewer, "Reviewer", Now).IsSuccess);
    }

    [Fact]
    public void ShouldAllowDiscardOnlyGivenNeverReviewedDraft()
    {
        // Arrange
        var boundary = new SystemBoundary(TenantId, BoundaryId);
        Assert.True(boundary.Create(ProgramId, VersionId, Content(), AuthorId,
            "Author", Now).IsSuccess);
        var reviewedBy = Uuid.CreateVersion4();
        Assert.True(boundary.Review(VersionId, 1, Uuid.CreateVersion4(),
            "request_changes", "Revise the scope", reviewedBy, "Reviewer", Now).IsSuccess);

        // Act
        var denied = boundary.DiscardDraft(VersionId, 1, "Withdraw",
            AuthorId, "Author", Now);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(denied.Error).Kind);
        Assert.Equal(2, new AggregateScenario<SystemBoundary>(boundary).PendingEvents.Count);

        var unused = new SystemBoundary(TenantId, Uuid.CreateVersion4());
        var unusedVersion = Uuid.CreateVersion4();
        Assert.True(unused.Create(ProgramId, unusedVersion, Content(),
            AuthorId, "Author", Now).IsSuccess);
        Assert.True(unused.DiscardDraft(unusedVersion, 1, "Not the right scope",
            AuthorId, "Author", Now).IsSuccess);
        Assert.Equal(2, new AggregateScenario<SystemBoundary>(unused).PendingEvents.Count);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(
            unused.Revise(unusedVersion, 1, Content(), AuthorId, "Author", Now).Error).Kind);
    }

    static BoundaryContent Content() => new("The in-scope service and its dependencies.",
        "readiness", ["security"],
        [new BoundaryScopeEntry(EntryId, "inclusion", "service",
            "Service A", null, "Compliance lead",
            "The service handles customer work.", true)]);
}
