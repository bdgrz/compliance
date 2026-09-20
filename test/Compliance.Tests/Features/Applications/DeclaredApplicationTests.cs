using Bdgrz.Compliance.Features.Applications;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class DeclaredApplicationTests
{
    [Fact]
    public void ShouldPreserveDeclaredEventsGivenRetryAndStaleRevision()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var instanceId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);
        var application = new DeclaredApplication(tenantId, applicationId);

        // Act
        var created = application.Declare(" Payroll ", "Run payroll", "Operations",
            actorId, "Manager", now);
        var revised = application.Revise(1, "Payroll", "Run monthly payroll", "Operations",
            actorId, "Manager", now.AddMinutes(1));
        var instance = application.DeclareInstance(2, instanceId, "Production", "production",
            null, "payroll-prod", actorId, "Manager", now.AddMinutes(2));
        var createReplay = application.Declare("Payroll", "Run payroll", "Operations",
            actorId, "Manager", now.AddMinutes(3));
        var replay = application.DeclareInstance(2, instanceId, " Production ", "production",
            null, " payroll-prod ", actorId, "Manager", now.AddMinutes(3));
        var stale = application.Revise(2, "Payroll", "Stale edit", null,
            actorId, "Manager", now.AddMinutes(3));
        var collision = application.DeclareInstance(3, instanceId, "Different", "production",
            null, "payroll-prod", actorId, "Manager", now.AddMinutes(3));

        // Assert
        Assert.True(created.IsSuccess);
        Assert.True(revised.IsSuccess);
        Assert.True(instance.IsSuccess);
        Assert.True(createReplay.IsSuccess);
        Assert.True(replay.IsSuccess);
        Assert.Equal(instance.Value.SystemInstanceId, replay.Value.SystemInstanceId);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(stale.Error).Kind);
        Assert.Contains("revision: 3", Assert.IsType<RequestError>(stale.Error).Message,
            StringComparison.Ordinal);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(collision.Error).Kind);
        Assert.Equal(3, application.Revision);
        Assert.Collection(new AggregateScenario<DeclaredApplication>(application).PendingEvents,
            ev => Assert.Equal(tenantId, Assert.IsType<ApplicationDeclared>(ev).TenantId),
            ev => Assert.Equal(2, Assert.IsType<ApplicationRevised>(ev).Revision),
            ev => Assert.Equal(3, Assert.IsType<SystemInstanceDeclared>(ev).ApplicationRevision));
    }

    [Fact]
    public void ShouldValidateRequiredFieldsAndParentGivenDeclarations()
    {
        // Arrange
        var application = new DeclaredApplication(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;

        // Act
        var missingPurpose = application.Declare("Payroll", " ", null, actorId, "Manager", now);
        var missingApplication = application.DeclareInstance(1, Uuid.CreateVersion4(), "Prod",
            "production", null, null, actorId, "Manager", now);
        var created = application.Declare("Payroll", "Run payroll", null, actorId, "Manager", now);
        var missingKind = application.DeclareInstance(1, Uuid.CreateVersion4(), "Prod",
            " ", null, null, actorId, "Manager", now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(missingPurpose.Error).Kind);
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(missingApplication.Error).Kind);
        Assert.True(created.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(missingKind.Error).Kind);
        Assert.Single(new AggregateScenario<DeclaredApplication>(application).PendingEvents);
    }
}
