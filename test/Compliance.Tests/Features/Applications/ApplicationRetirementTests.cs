using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationRetirementTests
{
    static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
    static readonly Uuid ActorId = Uuid.CreateVersion4();

    [Fact]
    public void ShouldRetireWithMergeAndBlockLaterChangesGivenActiveApplication()
    {
        // Arrange
        var application = Application();
        var successorId = Uuid.CreateVersion4();

        // Act
        var retired = application.Retire(1, Now.AddDays(7), " Duplicate of Payroll ",
            successorId, ActorId, "Manager", Now);
        var revise = application.Revise(2, "Payroll", "Still here", null, ActorId,
            "Manager", Now);
        var again = application.Retire(2, Now, "Again", null, ActorId, "Manager", Now);

        // Assert
        Assert.Null(retired);
        Assert.True(application.IsRetired);
        Assert.Equal(2, application.Revision);
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(revise).Code);
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(again).Code);
        var ev = Assert.IsType<ApplicationRetired>(
            new AggregateScenario<DeclaredApplication>(application).PendingEvents[^1]);
        Assert.Equal("Duplicate of Payroll", ev.Reason);
        Assert.Equal(successorId, ev.MergedIntoApplicationId);
        Assert.Equal(Now.AddDays(7), ev.EffectiveAt);
    }

    [Fact]
    public void ShouldRejectRetirementGivenSelfMergeMissingReasonOrStaleRevision()
    {
        // Arrange
        var application = Application();

        // Act
        var selfMerge = application.Retire(1, Now, "Dup", application.Id, ActorId, "M", Now);
        var noReason = application.Retire(1, Now, " ", null, ActorId, "M", Now);
        var stale = application.Retire(5, Now, "Dup", null, ActorId, "M", Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(selfMerge).Code);
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(noReason).Code);
        Assert.Equal(CommandFailureCode.VersionConflict, Assert.IsType<CommandFailure>(stale).Code);
        Assert.False(application.IsRetired);
    }

    [Fact]
    public void ShouldRetireInstanceOnceGivenCurrentRevision()
    {
        // Arrange
        var instance = new DeclaredSystemInstance(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        Assert.True(instance.Declare(Uuid.CreateVersion4(), "Slack", "saas_tenant", null,
            "T123", ActorId, "Manager", Now).IsSuccess);

        // Act
        var stale = instance.Retire(2, Now, "Closed", ActorId, "Manager", Now);
        var retired = instance.Retire(1, Now, "Workspace closed", ActorId, "Manager", Now);
        var again = instance.Retire(2, Now, "Closed", ActorId, "Manager", Now);

        // Assert
        Assert.Equal(CommandFailureCode.VersionConflict, Assert.IsType<CommandFailure>(stale).Code);
        Assert.Null(retired);
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(again).Code);
        Assert.Equal(2, instance.Revision);
        var ev = Assert.IsType<SystemInstanceRetired>(
            new AggregateScenario<DeclaredSystemInstance>(instance).PendingEvents[^1]);
        Assert.Equal(2, ev.Revision);
        Assert.Equal(instance.ApplicationId, ev.ApplicationId);
    }

    static DeclaredApplication Application()
    {
        var application = new DeclaredApplication(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        Assert.True(application.Declare("Payroll", "Run payroll", null, ActorId, "Manager",
            Now.AddDays(-1)).IsSuccess);
        return application;
    }
}
