using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Responsibilities;

public sealed class ResponsibilitySetTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid MemberId = Uuid.CreateVersion4();
    static readonly Uuid AdminId = Uuid.CreateVersion4();
    static readonly ResponsibilityScope Scope = new("boundary", Uuid.CreateVersion4(),
        Uuid.CreateVersion4(), 2);
    static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldRejectConflictingAssignmentGivenNoApprovedWaiver()
    {
        // Arrange
        var set = new ResponsibilitySet(TenantId, Scope);
        Assert.Null(set.Assign(Uuid.CreateVersion4(), MemberId, ResponsibilityType.ControlOwner,
            AdminId, "Admin", Now, Now, null, []));

        // Act
        var conflict = set.Assign(Uuid.CreateVersion4(), MemberId, ResponsibilityType.AssignedReviewer,
            AdminId, "Admin", Now, Now, null, []);

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(conflict).Code);
        Assert.Single(set.ReadAssignments());
    }

    [Fact]
    public void ShouldRecordExactWaiverGivenApprovedConflictException()
    {
        // Arrange
        var set = new ResponsibilitySet(TenantId, Scope);
        Assert.Null(set.Assign(Uuid.CreateVersion4(), MemberId, ResponsibilityType.ControlOwner,
            AdminId, "Admin", Now.AddDays(-2), Now.AddDays(-2), null, []));
        var waiverScope = new SeparationOfDutiesWaiverScope(Scope.RecordType, Scope.RecordId,
            Scope.VersionId, Scope.Revision, SeparationOfDutiesActions.Review);
        var waiver = ApprovedWaiver(waiverScope, MemberId, AdminId, Uuid.CreateVersion4());
        var assignmentId = Uuid.CreateVersion4();

        // Act
        var failure = set.Assign(assignmentId, MemberId, ResponsibilityType.AssignedReviewer,
            AdminId, "Admin", Now, Now, null, [waiver]);

        // Assert
        Assert.Null(failure);
        var assignment = Assert.Single(set.ReadAssignments(), item => item.AssignmentId == assignmentId);
        Assert.Equal([waiver.Id], assignment.SeparationOfDutiesWaiverIds);
    }

    [Fact]
    public void ShouldRejectWrongScopeOrExpiredWaiverGivenConflictingAssignment()
    {
        // Arrange
        var set = new ResponsibilitySet(TenantId, Scope);
        Assert.Null(set.Assign(Uuid.CreateVersion4(), MemberId, ResponsibilityType.ControlOwner,
            AdminId, "Admin", Now.AddDays(-2), Now.AddDays(-2), null, []));
        var wrongScope = new SeparationOfDutiesWaiverScope(Scope.RecordType, Scope.RecordId,
            Scope.VersionId, Scope.Revision + 1, SeparationOfDutiesActions.Review);
        var expiredScope = new SeparationOfDutiesWaiverScope(Scope.RecordType, Scope.RecordId,
            Scope.VersionId, Scope.Revision, SeparationOfDutiesActions.Review);
        var wrong = ApprovedWaiver(wrongScope, MemberId, AdminId, Uuid.CreateVersion4());
        var expired = ApprovedWaiver(expiredScope, MemberId, AdminId, Uuid.CreateVersion4(),
            expiresAt: Now.AddDays(-1));

        // Act
        var wrongResult = set.Assign(Uuid.CreateVersion4(), MemberId,
            ResponsibilityType.AssignedReviewer, AdminId, "Admin", Now, Now, null, [wrong]);
        var expiredResult = set.Assign(Uuid.CreateVersion4(), MemberId,
            ResponsibilityType.AssignedReviewer, AdminId, "Admin", Now, Now, null, [expired]);

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(wrongResult).Code);
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(expiredResult).Code);
        Assert.Single(set.ReadAssignments());
    }

    [Fact]
    public void ShouldPreserveRevocationAndAllowLaterReviewerGivenOwnerResponsibilityEnded()
    {
        // Arrange
        var set = new ResponsibilitySet(TenantId, Scope);
        var ownerId = Uuid.CreateVersion4();
        Assert.Null(set.Assign(ownerId, MemberId, ResponsibilityType.ControlOwner,
            AdminId, "Admin", Now.AddDays(-2), Now.AddDays(-2), null, []));

        // Act
        var revokedAt = Now.AddDays(-1);
        var revocation = set.Revoke(ownerId, AdminId, "Admin", revokedAt, "Work completed");
        var laterAssignment = set.Assign(Uuid.CreateVersion4(), MemberId,
            ResponsibilityType.AssignedReviewer, AdminId, "Admin", Now, Now, null, []);

        // Assert
        Assert.Null(revocation);
        Assert.Null(laterAssignment);
        var history = Assert.Single(set.ReadAssignments(), item => item.AssignmentId == ownerId);
        Assert.Equal(revokedAt, history.RevokedAt);
        Assert.Equal("Work completed", history.RevocationReason);
    }

    static SeparationOfDutiesWaiver ApprovedWaiver(SeparationOfDutiesWaiverScope scope,
        Uuid beneficiaryId, Uuid requesterId, Uuid approverId, DateTimeOffset? expiresAt = null)
    {
        var waiver = new SeparationOfDutiesWaiver(TenantId, Uuid.CreateVersion4());
        var approvedAt = expiresAt is { } expiry && expiry <= Now
            ? expiry.AddHours(-1) : Now.AddDays(-1);
        Assert.Null(waiver.Record(scope, beneficiaryId, requesterId, "Requester",
            "Small team exception", Now.AddDays(-3), expiresAt ?? Now.AddDays(2)));
        Assert.Null(waiver.Approve(approverId, "Approver", approvedAt));
        return waiver;
    }
}
