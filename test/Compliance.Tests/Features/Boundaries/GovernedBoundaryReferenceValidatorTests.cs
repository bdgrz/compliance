using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Boundaries;

public sealed class GovernedBoundaryReferenceValidatorTests
{
    [Fact]
    public async Task GovernedServiceMustBeActiveInTheRequestTenant()
    {
        var tenantId = Uuid.CreateVersion4();
        var serviceId = Uuid.CreateVersion4();
        var directory = new ServiceDirectory(tenantId, serviceId);
        var validator = new GovernedBoundaryReferenceValidator(directory);
        var content = Content("service", serviceId);

        Assert.True((await validator.ValidateAsync(tenantId, content)).IsSuccess);
        Assert.False((await validator.ValidateAsync(Uuid.CreateVersion4(), content)).IsSuccess);
        directory.Status = "retired";
        Assert.False((await validator.ValidateAsync(tenantId, content)).IsSuccess);
        Assert.False((await validator.ValidateAsync(tenantId, Content("provider", serviceId))).IsSuccess);
    }

    static BoundaryContent Content(string subjectType, Uuid serviceId) =>
        new("Scope", "readiness", ["security"],
        [new BoundaryScopeEntry(Uuid.CreateVersion4(), "inclusion", subjectType,
            "Payroll", serviceId, "Operations", "In scope", false)]);

    sealed class ServiceDirectory(Uuid tenantId, Uuid serviceId) : IClientServiceDirectoryReader
    {
        public string Status { get; set; } = "active";

        public ValueTask<ClientServiceView?> GetAsync(Uuid requestedTenantId, Uuid requestedServiceId,
            CancellationToken ct = default) => ValueTask.FromResult<ClientServiceView?>(
                requestedTenantId == tenantId && requestedServiceId == serviceId
                    ? new ClientServiceView(tenantId, serviceId, 1, "Payroll", "Payroll processing",
                        "Operations", Status, Uuid.CreateVersion4(), "Owner", DateTimeOffset.UtcNow)
                    : null);

        public ValueTask<Page<ClientServiceView>> ListAsync(Uuid requestedTenantId, int limit,
            string? cursor, CancellationToken ct = default) => throw new NotSupportedException();

        public ValueTask<Page<ClientServiceRevisionView>?> ListRevisionsAsync(Uuid requestedTenantId,
            Uuid requestedServiceId, int limit, string? cursor, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
