using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Programs;
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
        var validator = new GovernedBoundaryReferenceValidator(activity);

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
}
