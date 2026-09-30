using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.TechnologyInventory;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Boundaries;

public sealed class GovernedBoundaryReferenceValidatorTests
{
    [Fact]
    public async Task ShouldAcceptOnlyActiveSameProgramServiceGivenGovernedBoundaryReference()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var serviceId = Uuid.CreateVersion4();
        var activity = new ServiceActivity(tenantId, programId, serviceId);
        var validator = new GovernedBoundaryReferenceValidator(activity,
            new ApplicationActivity(tenantId, Uuid.CreateVersion4(), Uuid.CreateVersion4()));

        // Act
        var content = Content("service", serviceId);


        // Assert
        Assert.True((await validator.ValidateAsync(tenantId, programId, content)).IsSuccess);
        Assert.False((await validator.ValidateAsync(Uuid.CreateVersion4(), programId, content)).IsSuccess);
        Assert.False((await validator.ValidateAsync(tenantId, Uuid.CreateVersion4(), content)).IsSuccess);
        activity.Active = false;
        Assert.False((await validator.ValidateAsync(tenantId, programId, content)).IsSuccess);
        Assert.False((await validator.ValidateAsync(tenantId, programId,
            Content("provider", serviceId))).IsSuccess);
    }

    [Fact]
    public async Task ShouldAcceptSameTenantApplicationAndInstanceGivenGovernedBoundaryReferences()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var instanceId = Uuid.CreateVersion4();
        var applications = new ApplicationActivity(tenantId, applicationId, instanceId);
        var validator = new GovernedBoundaryReferenceValidator(
            new ServiceActivity(tenantId, programId, Uuid.CreateVersion4()), applications);

        // Act
        var application = await validator.ValidateAsync(tenantId, programId,
            Content("application", applicationId));
        var instance = await validator.ValidateAsync(tenantId, programId,
            Content("system_instance", instanceId));
        var foreignApplication = await validator.ValidateAsync(Uuid.CreateVersion4(), programId,
            Content("application", applicationId));
        var foreignInstance = await validator.ValidateAsync(Uuid.CreateVersion4(), programId,
            Content("system_instance", instanceId));
        var wrongApplicationId = await validator.ValidateAsync(tenantId, programId,
            Content("application", instanceId));
        var wrongInstanceId = await validator.ValidateAsync(tenantId, programId,
            Content("system_instance", applicationId));
        applications.InstanceProjected = false;
        var lag = await validator.ValidateAsync(tenantId, programId,
            Content("system_instance", instanceId));

        // Assert
        Assert.True(application.IsSuccess);
        Assert.True(instance.IsSuccess);
        Assert.False(foreignApplication.IsSuccess);
        Assert.False(foreignInstance.IsSuccess);
        Assert.False(wrongApplicationId.IsSuccess);
        Assert.False(wrongInstanceId.IsSuccess);
        Assert.False(lag.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, lag.Error.Kind);
        Assert.True(lag.Error.IsTransient);
        Assert.False(foreignInstance.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldResolveActiveInventoryRecordsGivenTechnologyScopeReferences()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var assetId = Uuid.CreateVersion4();
        var validator = new GovernedBoundaryReferenceValidator(
            new ServiceActivity(tenantId, programId, Uuid.CreateVersion4()),
            new ApplicationActivity(tenantId, Uuid.CreateVersion4(), Uuid.CreateVersion4()),
            new InventoryActivity(tenantId, "information", assetId));

        // Act
        var active = await validator.ValidateAsync(tenantId, programId,
            Content("information", assetId));
        var foreign = await validator.ValidateAsync(Uuid.CreateVersion4(), programId,
            Content("information", assetId));
        var wrongType = await validator.ValidateAsync(tenantId, programId,
            Content("data_flow", assetId));

        // Assert
        Assert.True(active.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, foreign.Error!.Kind);
        Assert.Equal(RequestErrorKind.Conflict, wrongType.Error!.Kind);
    }

    sealed class InventoryActivity(Uuid tenantId, string subjectType, Uuid recordId)
        : ITechnologyInventoryActivity
    {
        public ValueTask<bool> IsActiveAsync(Uuid tenant, string subject, Uuid id,
            CancellationToken ct = default) =>
            ValueTask.FromResult(tenant == tenantId && subject == subjectType && id == recordId);
    }

    static BoundaryContent Content(string subjectType, Uuid serviceId) =>
        new("Scope", "readiness", ["security"],
        [new BoundaryScopeEntry(Uuid.CreateVersion4(), "inclusion", subjectType,
            "Payroll", serviceId, "Operations", "In scope", false)]);

    sealed class ServiceActivity(Uuid tenantId, Uuid programId, Uuid serviceId) : IClientServiceActivity
    {
        public bool Active { get; set; } = true;

        public ValueTask<bool> IsActiveAsync(Uuid requestedTenantId, Uuid requestedProgramId,
            Uuid requestedServiceId,
            CancellationToken ct = default) => ValueTask.FromResult(
            Active && requestedTenantId == tenantId && requestedProgramId == programId &&
            requestedServiceId == serviceId);
    }

    sealed class ApplicationActivity(Uuid tenantId, Uuid applicationId, Uuid instanceId)
        : IApplicationInventoryActivity
    {
        public bool InstanceProjected { get; set; } = true;

        public ValueTask<bool> IsDeclaredAsync(Uuid requestedTenantId,
            Uuid requestedApplicationId, CancellationToken ct = default) =>
            ValueTask.FromResult(requestedTenantId == tenantId &&
                requestedApplicationId == applicationId);

        public ValueTask<SystemInstanceReferenceState> GetInstanceStateAsync(
            Uuid requestedTenantId, Uuid requestedInstanceId, CancellationToken ct = default) =>
            ValueTask.FromResult(requestedTenantId != tenantId || requestedInstanceId != instanceId
                ? SystemInstanceReferenceState.Missing
                : InstanceProjected
                    ? SystemInstanceReferenceState.Declared
                    : SystemInstanceReferenceState.Pending);
    }
}
