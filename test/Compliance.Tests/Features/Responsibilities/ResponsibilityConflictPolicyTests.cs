using Bdgrz.Compliance.Features.Responsibilities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Responsibilities;

public sealed class ResponsibilityConflictPolicyTests
{
    [Fact]
    public void ShouldFindSelfReviewConflictGivenSameMemberOwnsAndReviewsOneRecord()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var memberId = Uuid.CreateVersion4();
        var recordId = Uuid.CreateVersion4();
        var from = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var until = from.AddMonths(1);
        var scope = new ResponsibilityScope("boundary", recordId, Uuid.CreateVersion4(), 1);
        var existing = new ResponsibilityAssignmentView(tenantId, Uuid.CreateVersion4(), memberId,
            ResponsibilityType.ControlOwner, scope, from, Uuid.CreateVersion4(), from, until,
            null, Uuid.Empty, []);
        var proposed = new ResponsibilityAssignmentView(tenantId, Uuid.CreateVersion4(), memberId,
            ResponsibilityType.AssignedReviewer, scope, from, Uuid.CreateVersion4(), from.AddDays(10),
            until.AddDays(10), null, Uuid.Empty, []);

        // Act
        var conflicts = ResponsibilityConflictPolicy.FindConflicts([existing], proposed);

        // Assert
        var conflict = Assert.Single(conflicts);
        Assert.Equal(ResponsibilityConflictKind.SelfReview, conflict.Kind);
        Assert.Equal(existing.AssignmentId, conflict.ExistingAssignmentId);
        Assert.Equal(proposed.AssignmentId, conflict.ProposedAssignmentId);
    }

    [Fact]
    public void ShouldNotFindConflictGivenDifferentMembersOrNonOverlappingIntervals()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var recordId = Uuid.CreateVersion4();
        var from = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var scope = new ResponsibilityScope("boundary", recordId, Uuid.CreateVersion4(), 1);
        var existing = new ResponsibilityAssignmentView(tenantId, Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), ResponsibilityType.ControlOwner, scope, from, Uuid.CreateVersion4(),
            from, from.AddDays(1), null, Uuid.Empty, []);
        var differentMember = new ResponsibilityAssignmentView(tenantId, Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), ResponsibilityType.AssignedReviewer, scope, from, Uuid.CreateVersion4(),
            from, from.AddDays(1), null, Uuid.Empty, []);
        var nonOverlapping = differentMember with
        {
            MemberId = existing.MemberId,
            EffectiveFrom = from.AddDays(1),
            EffectiveUntil = from.AddDays(2),
        };

        // Act
        var otherMemberConflicts = ResponsibilityConflictPolicy.FindConflicts([existing], differentMember);
        var nonOverlappingConflicts = ResponsibilityConflictPolicy.FindConflicts([existing], nonOverlapping);

        // Assert
        Assert.Empty(otherMemberConflicts);
        Assert.Empty(nonOverlappingConflicts);
    }

    [Fact]
    public void ShouldFindSelfApprovalConflictGivenWorkOwnerAndApprover()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var memberId = Uuid.CreateVersion4();
        var scope = new ResponsibilityScope("boundary", Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), 4);
        var existing = CreateAssignment(tenantId, memberId, ResponsibilityType.ControlOwner,
            scope, DateTimeOffset.UtcNow.AddDays(-1), null);
        var proposed = CreateAssignment(tenantId, memberId, ResponsibilityType.PolicyApprover,
            scope, DateTimeOffset.UtcNow, null);

        // Act
        var conflict = Assert.Single(ResponsibilityConflictPolicy.FindConflicts([existing], proposed));

        // Assert
        Assert.Equal(ResponsibilityConflictKind.SelfApproval, conflict.Kind);
        Assert.Equal("approve", conflict.WaiverAction);
    }

    [Fact]
    public void ShouldNotFindConflictGivenReviewerAndApproverWithoutWorkResponsibility()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var memberId = Uuid.CreateVersion4();
        var scope = new ResponsibilityScope("boundary", Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), 2);
        var existing = CreateAssignment(tenantId, memberId, ResponsibilityType.AssignedReviewer,
            scope, DateTimeOffset.UtcNow.AddDays(-1), null);
        var proposed = CreateAssignment(tenantId, memberId, ResponsibilityType.PolicyApprover,
            scope, DateTimeOffset.UtcNow, null);

        // Act
        var conflicts = ResponsibilityConflictPolicy.FindConflicts([existing], proposed);

        // Assert
        Assert.Empty(conflicts);
    }

    [Fact]
    public void ShouldNotFindConflictGivenWorkResponsibilityEndedBeforeReviewerStarts()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var memberId = Uuid.CreateVersion4();
        var scope = new ResponsibilityScope("boundary", Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), 3);
        var from = DateTimeOffset.UtcNow.AddDays(-5);
        var existing = CreateAssignment(tenantId, memberId, ResponsibilityType.ControlOwner,
            scope, from, null) with
        { RevokedAt = from.AddDays(2) };
        var proposed = CreateAssignment(tenantId, memberId, ResponsibilityType.AssignedReviewer,
            scope, from.AddDays(3), null);

        // Act
        var conflicts = ResponsibilityConflictPolicy.FindConflicts([existing], proposed);

        // Assert
        Assert.Empty(conflicts);
    }

    static ResponsibilityAssignmentView CreateAssignment(Uuid tenantId, Uuid memberId,
        ResponsibilityType type, ResponsibilityScope scope,
        DateTimeOffset effectiveFrom, DateTimeOffset? effectiveUntil) =>
        new(tenantId, Uuid.CreateVersion4(), memberId, type, scope,
            effectiveFrom, Uuid.CreateVersion4(), effectiveFrom, effectiveUntil,
            null, Uuid.Empty, []);
}
