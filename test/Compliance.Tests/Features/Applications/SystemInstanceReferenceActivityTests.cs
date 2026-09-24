using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class SystemInstanceReferenceActivityTests
{
    [Fact]
    public async Task ShouldReportTransientConflictGivenCommittedButUnprojectedInstanceReference()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var instanceId = Uuid.CreateVersion4();
        var instance = new DeclaredSystemInstance(tenantId, instanceId);
        Assert.True(instance.Declare(applicationId, "Production", "production", null, null,
            Uuid.CreateVersion4(), "Manager", DateTimeOffset.UtcNow).IsSuccess);
        var directory = new FitzApplicationDirectory(new InMemoryKvClient());
        var activity = new EventSourcedApplicationInventoryActivity(new InstanceReader(instance),
            directory, new LegacySystemInstanceSource(directory, new InMemoryEventStore()));
        var boundaries = new GovernedBoundaryReferenceValidator(new NoServices(), activity);
        var controls = new GovernedControlApplicabilityReferenceValidator(activity);

        // Act
        var boundary = await boundaries.ValidateAsync(tenantId, Uuid.CreateVersion4(),
            new BoundaryContent("Scope", "readiness", ["security"],
            [
                new BoundaryScopeEntry(Uuid.CreateVersion4(), "inclusion", "system_instance",
                    "Production", instanceId, "Operations", "In scope", false),
            ]));
        var control = await controls.ValidateAsync(tenantId, new ControlDraftContent("Title",
            "Objective", "Description", "Narrative", ["Evidence"], Applicability:
            [
                new ControlApplicabilityReference(Uuid.CreateVersion4(), "system_instance",
                    "Production", instanceId, "Applies", false),
            ]));
        var absent = await controls.ValidateAsync(tenantId, new ControlDraftContent("Title",
            "Objective", "Description", "Narrative", ["Evidence"], Applicability:
            [
                new ControlApplicabilityReference(Uuid.CreateVersion4(), "system_instance",
                    "Missing", Uuid.CreateVersion4(), "Applies", false),
            ]));

        // Assert
        foreach (var error in new[] { boundary.Error, control.Error })
        {
            var conflict = Assert.IsType<RequestError>(error);
            Assert.Equal(RequestErrorKind.Conflict, conflict.Kind);
            Assert.True(conflict.IsTransient);
        }
        var missing = Assert.IsType<RequestError>(absent.Error);
        Assert.Equal(RequestErrorKind.Conflict, missing.Kind);
        Assert.False(missing.IsTransient);
    }

    sealed class InstanceReader(DeclaredSystemInstance instance) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult(aggregate is DeclaredSystemInstance && aggregate.Id == instance.Id
                ? (TAggregate)(Aggregate)instance : aggregate);
    }

    sealed class NoServices : IClientServiceActivity
    {
        public ValueTask<bool> IsActiveAsync(Uuid tenantId, Uuid programId, Uuid serviceId,
            CancellationToken ct = default) => ValueTask.FromResult(false);
    }
}
