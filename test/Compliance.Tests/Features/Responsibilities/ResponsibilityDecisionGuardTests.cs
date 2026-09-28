using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Responsibilities;

public sealed class ResponsibilityDecisionGuardTests
{
    static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldRejectNonDecisionResponsibilityGivenDecisionGuard()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var scope = new ResponsibilityScope("boundary", Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), 2);
        var set = new ResponsibilitySet(tenantId, scope);

        // Act
        var result = ResponsibilityDecisionGuard.Validate(set, scope, Uuid.CreateVersion4(),
            ResponsibilityType.ControlOwner, Now, false, null);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent,
            Assert.IsType<CommandFailure>(result).Code);
    }

    [Fact]
    public void ShouldRejectSelfReviewGivenActiveWorkResponsibilityWithoutWaiver()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var memberId = Uuid.CreateVersion4();
        var scope = new ResponsibilityScope("boundary", Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), 2);
        var set = new ResponsibilitySet(tenantId, scope);
        Assert.Null(set.Assign(Uuid.CreateVersion4(), memberId,
            ResponsibilityType.ControlOwner, Uuid.CreateVersion4(), "Admin",
            Now.AddDays(-1), Now.AddDays(-1), null, []));

        // Act
        var result = ResponsibilityDecisionGuard.Validate(set, scope, memberId,
            ResponsibilityType.AssignedReviewer, Now, false, null);

        // Assert
        Assert.Equal(CommandFailureCode.ActorProhibited,
            Assert.IsType<CommandFailure>(result).Code);
    }

    [Fact]
    public void ShouldAllowSelfReviewGivenActiveExactWaiverForResponsibilityConflict()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var memberId = Uuid.CreateVersion4();
        var admin = Uuid.CreateVersion4();
        var scope = new ResponsibilityScope("boundary", Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), 2);
        var set = new ResponsibilitySet(tenantId, scope);
        Assert.Null(set.Assign(Uuid.CreateVersion4(), memberId,
            ResponsibilityType.ControlOwner, admin, "Admin", Now.AddDays(-1),
            Now.AddDays(-1), null, []));
        var waiver = CreateApprovedWaiver(tenantId, memberId, admin,
            new SeparationOfDutiesWaiverScope(scope.RecordType, scope.RecordId,
                scope.VersionId, scope.Revision, SeparationOfDutiesActions.Review));

        // Act
        var result = ResponsibilityDecisionGuard.Validate(set, scope, memberId,
            ResponsibilityType.AssignedReviewer, Now, false, waiver);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ShouldRejectWaiverGivenDifferentActionOrNonConflict()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var memberId = Uuid.CreateVersion4();
        var admin = Uuid.CreateVersion4();
        var scope = new ResponsibilityScope("boundary", Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), 2);
        var set = new ResponsibilitySet(tenantId, scope);
        Assert.Null(set.Assign(Uuid.CreateVersion4(), memberId,
            ResponsibilityType.ControlOwner, admin, "Admin", Now.AddDays(-1),
            Now.AddDays(-1), null, []));
        var wrongActionWaiver = CreateApprovedWaiver(tenantId, memberId, admin,
            new SeparationOfDutiesWaiverScope(scope.RecordType, scope.RecordId,
                scope.VersionId, scope.Revision, SeparationOfDutiesActions.Approve));
        var conflict = ResponsibilityDecisionGuard.Validate(set, scope, memberId,
            ResponsibilityType.AssignedReviewer, Now, false, wrongActionWaiver);

        var clearSet = new ResponsibilitySet(tenantId, scope);
        var irrelevantWaiver = CreateApprovedWaiver(tenantId, memberId, admin,
            new SeparationOfDutiesWaiverScope(scope.RecordType, scope.RecordId,
                scope.VersionId, scope.Revision, SeparationOfDutiesActions.Review));

        // Act
        var irrelevant = ResponsibilityDecisionGuard.Validate(clearSet, scope, memberId,
            ResponsibilityType.AssignedReviewer, Now, false, irrelevantWaiver);

        // Assert
        Assert.Equal(CommandFailureCode.ActorProhibited,
            Assert.IsType<CommandFailure>(conflict).Code);
        Assert.Equal(CommandFailureCode.ActorProhibited,
            Assert.IsType<CommandFailure>(irrelevant).Code);
    }

    static SeparationOfDutiesWaiver CreateApprovedWaiver(Uuid tenantId, Uuid memberId,
        Uuid adminId, SeparationOfDutiesWaiverScope scope)
    {
        var waiver = new SeparationOfDutiesWaiver(tenantId, Uuid.CreateVersion4());
        Assert.Null(waiver.Record(scope, memberId, adminId, "Requester", "Small team",
            Now.AddDays(-2), Now.AddDays(1)));
        Assert.Null(waiver.Approve(Uuid.CreateVersion4(), "Approver", Now.AddDays(-1)));
        return waiver;
    }
}
