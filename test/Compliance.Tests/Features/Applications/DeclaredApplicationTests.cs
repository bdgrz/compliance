using Bdgrz.Compliance.Features.Applications;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class DeclaredApplicationTests
{
    [Fact]
    public void ShouldPreserveHistoricalApplicationRevisionGivenLegacyInstanceEvent()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var instanceId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);
        var application = new DeclaredApplication(tenantId, applicationId);

        new AggregateScenario<DeclaredApplication>(application).Given(
            DomainEventSeed.Attach(new ApplicationDeclared(tenantId, applicationId,
                "Payroll", "Run payroll", "Operations", actorId, "Manager", now),
                applicationId, 1),
            DomainEventSeed.Attach(new ApplicationRevised(tenantId, applicationId, 2,
                "Payroll", "Run monthly payroll", "Operations", actorId, "Manager",
                now.AddMinutes(1)), applicationId, 2),
            DomainEventSeed.Attach(new SystemInstanceDeclared(tenantId, applicationId,
                instanceId, 3, "Production", "production", null, "payroll-prod",
                actorId, "Manager", now.AddMinutes(2)), applicationId, 3));

        // Act
        var createReplay = application.Declare("Payroll", "Run payroll", "Operations",
            actorId, "Manager", now.AddMinutes(3));
        var stale = application.Revise(2, "Payroll", "Stale edit", null,
            actorId, "Manager", now.AddMinutes(3));
        var current = application.Revise(3, "Payroll", "Current edit", null,
            actorId, "Manager", now.AddMinutes(4));

        // Assert
        Assert.True(createReplay.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(stale.Error).Kind);
        Assert.Contains("revision: 3", Assert.IsType<RequestError>(stale.Error).Message,
            StringComparison.Ordinal);
        Assert.True(current.IsSuccess);
        Assert.Equal(4, application.Revision);
        Assert.Equal(4, Assert.IsType<ApplicationRevised>(Assert.Single(
            new AggregateScenario<DeclaredApplication>(application).PendingEvents)).Revision);
    }

    [Fact]
    public void ShouldValidateRequiredFieldsGivenApplicationDeclaration()
    {
        // Arrange
        var application = new DeclaredApplication(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;

        // Act
        var missingPurpose = application.Declare("Payroll", " ", null, actorId, "Manager", now);
        var created = application.Declare("Payroll", "Run payroll", null, actorId, "Manager", now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(missingPurpose.Error).Kind);
        Assert.True(created.IsSuccess);
        Assert.Single(new AggregateScenario<DeclaredApplication>(application).PendingEvents);
    }

    [Fact]
    public void ShouldPreserveDeclaredClassificationGivenReplayAndRevision()
    {
        // Arrange
        var application = new DeclaredApplication(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;

        // Act
        var declared = application.Declare("Payroll", "Run payroll", "Finance", actorId,
            "Manager", now, " Internal ");
        var replay = application.Declare("Payroll", "Run payroll", "Finance", actorId,
            "Manager", now, " Internal ");
        var revised = application.Revise(1, "Payroll", "Run payroll", "Finance", actorId,
            "Manager", now.AddMinutes(1), " Restricted ");
        var oversized = application.Revise(2, "Payroll", "Run payroll", "Finance", actorId,
            "Manager", now.AddMinutes(2), new string('x', 201));

        // Assert
        Assert.True(declared.IsSuccess);
        Assert.True(replay.IsSuccess);
        Assert.True(revised.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(oversized.Error).Kind);
        Assert.Collection(new AggregateScenario<DeclaredApplication>(application).PendingEvents,
            ev => Assert.Equal("Internal", Assert.IsType<ApplicationDeclared>(ev).Classification),
            ev => Assert.Equal("Restricted", Assert.IsType<ApplicationRevised>(ev).Classification));
    }
}
