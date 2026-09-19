using System.Security.Claims;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Programs;

public sealed class ClientServiceTests
{
    sealed class ExecutionContext : IExecutionContext
    {
        public ClaimsPrincipal Actor { get; } = new(new ClaimsIdentity());
        public Uuid ExecutionId { get; } = Uuid.CreateVersion4();
        public Uuid CorrelationId { get; } = Uuid.CreateVersion4();
        public Uuid CauseId { get; } = Uuid.CreateVersion4();
        public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;
    }

    [Fact]
    public async Task ShouldReadCurrentActivityFromTenantEventStreamGivenCreateAndRetire()
    {
        var tenantId = Uuid.CreateVersion4();
        var serviceId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
        await using var fixture = new StoreFixture();
        var activity = new EventSourcedClientServiceActivity(fixture.Repository);
        var service = new ClientService(tenantId, serviceId);
        Assert.False(await activity.IsActiveAsync(tenantId, serviceId));

        Assert.True(service.Create("Payroll", "Process payroll", "Operations",
            actorId, "Owner", now).IsSuccess);
        await fixture.Repository.SaveAsync(service, new ExecutionContext(), CancellationToken.None);
        Assert.True(await activity.IsActiveAsync(tenantId, serviceId));
        Assert.False(await activity.IsActiveAsync(Uuid.CreateVersion4(), serviceId));

        service = await fixture.Repository.HydrateAsync(new ClientService(tenantId, serviceId),
            CancellationToken.None);
        Assert.True(service.Retire(1, "Service ended", actorId, "Owner", now.AddDays(1)).IsSuccess);
        await fixture.Repository.SaveAsync(service, new ExecutionContext(), CancellationToken.None);
        Assert.False(await activity.IsActiveAsync(tenantId, serviceId));
    }

    [Fact]
    public void ShouldRejectStaleRevisionAndInvalidRetirementGivenServiceHistory()
    {
        var tenantId = Uuid.CreateVersion4();
        var serviceId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
        var service = new ClientService(tenantId, serviceId);

        Assert.True(service.Create("Payroll", "Process payroll", "Operations",
            actorId, "Owner", now).IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(service.Revise(0,
            "Payroll", "Changed", "Operations", actorId, "Owner", now).Error).Kind);
        Assert.True(service.Revise(1, "Payroll", "Monthly payroll", "Operations",
            actorId, "Owner", now.AddDays(1)).IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(service.Retire(2,
            " ", actorId, "Owner", now).Error).Kind);
        Assert.True(service.Retire(2, "Service ended", actorId, "Owner",
            now.AddDays(2)).IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(service.Revise(3,
            "Payroll", "Changed", "Operations", actorId, "Owner", now).Error).Kind);
        Assert.Collection(new AggregateScenario<ClientService>(service).PendingEvents,
            ev => Assert.IsType<ClientServiceCreated>(ev),
            ev => Assert.Equal(2, Assert.IsType<ClientServiceRevised>(ev).Revision),
            ev => Assert.Equal("Service ended", Assert.IsType<ClientServiceRetired>(ev).Rationale));
    }
}
