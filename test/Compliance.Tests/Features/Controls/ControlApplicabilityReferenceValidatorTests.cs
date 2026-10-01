using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Controls;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Controls;

public sealed class ControlApplicabilityReferenceValidatorTests
{
    [Fact]
    public async Task ShouldAcceptSameTenantApplicationAndInstanceGivenGovernedApplicability()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var instanceId = Uuid.CreateVersion4();
        var activity = new ApplicationActivity(tenantId, applicationId, instanceId);
        var validator = new GovernedControlApplicabilityReferenceValidator(activity);

        // Act
        var application = await validator.ValidateAsync(tenantId,
            Content(new ControlApplicabilityReference(Uuid.CreateVersion4(), "application",
                "GitHub", applicationId, "Access control operates here.", false)));
        var instance = await validator.ValidateAsync(tenantId,
            Content(new ControlApplicabilityReference(Uuid.CreateVersion4(), "system_instance",
                "GitHub production", instanceId, "This is the reviewed instance.", false)));
        var foreignApplication = await validator.ValidateAsync(Uuid.CreateVersion4(),
            Content(new ControlApplicabilityReference(Uuid.CreateVersion4(), "application",
                "GitHub", applicationId, "Access control operates here.", false)));
        var foreignInstance = await validator.ValidateAsync(Uuid.CreateVersion4(),
            Content(new ControlApplicabilityReference(Uuid.CreateVersion4(), "system_instance",
                "GitHub production", instanceId, "This is the reviewed instance.", false)));
        activity.InstanceProjected = false;
        var laggedInstance = await validator.ValidateAsync(tenantId,
            Content(new ControlApplicabilityReference(Uuid.CreateVersion4(), "system_instance",
                "GitHub production", instanceId, "This is the reviewed instance.", false)));

        // Assert
        Assert.True(application.IsSuccess);
        Assert.True(instance.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict,
            Assert.IsType<RequestError>(foreignApplication.Error).Kind);
        Assert.Equal(RequestErrorKind.Conflict,
            Assert.IsType<RequestError>(foreignInstance.Error).Kind);
        Assert.Equal(RequestErrorKind.Conflict,
            Assert.IsType<RequestError>(laggedInstance.Error).Kind);
    }

    [Fact]
    public async Task ShouldRequireAnOwningInventoryGivenGovernedRiskOrProcess()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var validator = new GovernedControlApplicabilityReferenceValidator(
            new ApplicationActivity(tenantId, Uuid.CreateVersion4(), Uuid.CreateVersion4()));

        // Act
        var governedRisk = await validator.ValidateAsync(tenantId,
            Content(new ControlApplicabilityReference(Uuid.CreateVersion4(), "risk",
                "Privileged access", Uuid.CreateVersion4(), "This control reduces exposure.", false)));
        var unresolvedRisk = await validator.ValidateAsync(tenantId,
            Content(new ControlApplicabilityReference(Uuid.CreateVersion4(), "risk",
                "Privileged access", null, "Treatment is not approved yet.", true)));

        // Assert
        Assert.Equal(RequestErrorKind.Validation,
            Assert.IsType<RequestError>(governedRisk.Error).Kind);
        Assert.True(unresolvedRisk.IsSuccess);
    }

    [Fact]
    public async Task ShouldAcceptOnlyRecordedSameTenantCommitmentGivenGovernedCommitmentReference()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var commitmentId = Uuid.CreateVersion4();
        var validator = new GovernedControlApplicabilityReferenceValidator(
            new ApplicationActivity(tenantId, Uuid.CreateVersion4(), Uuid.CreateVersion4()),
            new CommitmentActivity(tenantId, commitmentId));

        // Act
        var recorded = await validator.ValidateAsync(tenantId,
            Content(new ControlApplicabilityReference(Uuid.CreateVersion4(), "commitment",
                "SC-01", commitmentId, "The control fulfils this commitment.", false)));
        var foreign = await validator.ValidateAsync(Uuid.CreateVersion4(),
            Content(new ControlApplicabilityReference(Uuid.CreateVersion4(), "commitment",
                "SC-01", commitmentId, "The control fulfils this commitment.", false)));

        // Assert
        Assert.True(recorded.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(foreign.Error).Kind);
    }

    sealed class CommitmentActivity(Uuid tenantId, Uuid commitmentId)
        : Bdgrz.Compliance.Features.Commitments.ICommitmentReferenceReader
    {
        public ValueTask<bool> IsRecordedAsync(Uuid requestedTenantId, Uuid requestedId,
            CancellationToken ct = default) =>
            ValueTask.FromResult(requestedTenantId == tenantId && requestedId == commitmentId);
    }

    static ControlDraftContent Content(ControlApplicabilityReference reference) => new(
        "Access review", "Review access", "Management reviews access",
        "The owner reviews the access list quarterly.", ["Dated review record"],
        "Security lead", [reference]);

    sealed class ApplicationActivity(Uuid tenantId, Uuid applicationId, Uuid instanceId)
        : IApplicationInventoryActivity
    {
        public bool InstanceProjected { get; set; } = true;

        public ValueTask<bool> IsDeclaredAsync(Uuid requestedTenantId,
            Uuid requestedApplicationId, CancellationToken ct = default) => ValueTask.FromResult(
            requestedTenantId == tenantId && requestedApplicationId == applicationId);

        public ValueTask<SystemInstanceReferenceState> GetInstanceStateAsync(
            Uuid requestedTenantId, Uuid requestedInstanceId, CancellationToken ct = default) =>
            ValueTask.FromResult(requestedTenantId != tenantId || requestedInstanceId != instanceId
                ? SystemInstanceReferenceState.Missing
                : InstanceProjected
                    ? SystemInstanceReferenceState.Declared
                    : SystemInstanceReferenceState.Pending);
    }
}
