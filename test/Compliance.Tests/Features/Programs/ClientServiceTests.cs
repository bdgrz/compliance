using System.Security.Claims;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Programs;

public sealed class ClientServiceTests
{
    [Fact]
    public void ShouldPreserveActorSnapshotGivenChangedDisplayAndLegacyReplay()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var serviceId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        var service = new ClientService(tenantId, serviceId);
        Assert.True(service.Create(programId, "Payroll", "Process payroll", "Operations",
            actorId, "Original display", now).IsSuccess);
        Assert.True(service.Revise(1, "Payroll", "Monthly payroll", "Operations",
            actorId, "Changed display", now.AddMinutes(1)).IsSuccess);
        Assert.True(service.Retire(2, "Service ended", actorId, "Final display",
            now.AddMinutes(2)).IsSuccess);

        // Act
        var events = new AggregateScenario<ClientService>(service).PendingEvents;
        var created = Assert.IsType<ClientServiceCreated>(events[0]);
        var revised = Assert.IsType<ClientServiceRevised>(events[1]);
        var retired = Assert.IsType<ClientServiceRetired>(events[2]);
        var legacyCreated = JsonSerializer.Deserialize($$"""
            {"tenant_id":"{{tenantId}}","service_id":"{{serviceId}}","name":"Payroll",
             "purpose":"Process payroll","owner_reference":"Operations",
             "actor_member_id":"{{actorId}}","actor_display":"Original display",
             "changed_at":"{{now:O}}","program_id":"{{programId}}"}
            """, ComplianceCoreJsonContext.Default.ClientServiceCreated);
        var legacyRevised = JsonSerializer.Deserialize($$"""
            {"tenant_id":"{{tenantId}}","service_id":"{{serviceId}}","revision":2,
             "name":"Payroll","purpose":"Monthly payroll","owner_reference":"Operations",
             "actor_member_id":"{{actorId}}","actor_display":"Changed display",
             "changed_at":"{{now.AddMinutes(1):O}}"}
            """, ComplianceCoreJsonContext.Default.ClientServiceRevised);
        var legacyRetired = JsonSerializer.Deserialize($$"""
            {"tenant_id":"{{tenantId}}","service_id":"{{serviceId}}","revision":3,
             "rationale":"Service ended","actor_member_id":"{{actorId}}",
             "actor_display":"Final display","changed_at":"{{now.AddMinutes(2):O}}"}
            """, ComplianceCoreJsonContext.Default.ClientServiceRetired);
        var legacyCurrent = JsonSerializer.Deserialize($$"""
            {"tenant_id":"{{tenantId}}","service_id":"{{serviceId}}","revision":3,
             "name":"Payroll","purpose":"Monthly payroll","owner_reference":"Operations",
             "status":"retired","last_changed_by_member_id":"{{actorId}}",
             "last_changed_by_display":"Final display",
             "last_changed_at":"{{now.AddMinutes(2):O}}","program_id":"{{programId}}"}
            """, ComplianceCoreJsonContext.Default.ClientServiceView);
        var legacyHistory = JsonSerializer.Deserialize($$"""
            {"service_id":"{{serviceId}}","revision":1,"name":"Payroll",
             "purpose":"Process payroll","owner_reference":"Operations","status":"active",
             "retirement_rationale":null,"actor_member_id":"{{actorId}}",
             "actor_display":"Original display","changed_at":"{{now:O}}",
             "program_id":"{{programId}}"}
            """, ComplianceCoreJsonContext.Default.ClientServiceRevisionView);

        // Assert
        Assert.Equal(ActorReference.ForMember(actorId, "Original display"), created.StoredActor);
        Assert.Equal(ActorReference.ForMember(actorId, "Changed display"), revised.StoredActor);
        Assert.Equal(ActorReference.ForMember(actorId, "Final display"), retired.StoredActor);
        Assert.Equal(created.StoredActor, legacyCreated?.Actor);
        Assert.Equal(revised.StoredActor, legacyRevised?.Actor);
        Assert.Equal(retired.StoredActor, legacyRetired?.Actor);
        Assert.Null(legacyCreated?.StoredActor);
        Assert.Null(legacyRevised?.StoredActor);
        Assert.Null(legacyRetired?.StoredActor);
        Assert.Equal(retired.Actor, legacyCurrent?.LastChangedBy);
        Assert.Equal(created.Actor, legacyHistory?.Actor);
        Assert.Equal(3, service.Revision);
        Assert.False(service.IsActive);
    }

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
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var serviceId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
        await using var fixture = new StoreFixture();
        var activity = new EventSourcedClientServiceActivity(fixture.Repository);

        // Act
        var service = new ClientService(tenantId, serviceId);

        // Assert
        Assert.False(await activity.IsActiveAsync(tenantId, programId, serviceId));

        Assert.True(service.Create(programId, "Payroll", "Process payroll", "Operations",
            actorId, "Owner", now).IsSuccess);
        await fixture.Repository.SaveAsync(service, new ExecutionContext(), CancellationToken.None);
        Assert.True(await activity.IsActiveAsync(tenantId, programId, serviceId));
        Assert.False(await activity.IsActiveAsync(Uuid.CreateVersion4(), programId, serviceId));
        Assert.False(await activity.IsActiveAsync(tenantId, Uuid.CreateVersion4(), serviceId));

        service = await fixture.Repository.HydrateAsync(new ClientService(tenantId, serviceId),
            CancellationToken.None);
        Assert.True(service.Retire(1, "Service ended", actorId, "Owner", now.AddDays(1)).IsSuccess);
        await fixture.Repository.SaveAsync(service, new ExecutionContext(), CancellationToken.None);
        Assert.False(await activity.IsActiveAsync(tenantId, programId, serviceId));
    }

    [Fact]
    public void ShouldRejectChangedCreateGivenExistingServiceAndPreserveReplay()
    {
        // Arrange
        var service = new ClientService(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var programId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
        Assert.True(service.Create(programId, "Payroll", "Process payroll", "Operations",
            actorId, "Owner", now).IsSuccess);
        Assert.True(service.Revise(1, "Payroll", "Monthly payroll", "Operations",
            actorId, "Owner", now.AddMinutes(1)).IsSuccess);

        // Act
        var replay = service.Create(programId, " Payroll ", "Process payroll", "Operations",
            actorId, "Owner", now.AddMinutes(2));
        var changed = service.Create(programId, "Payroll", "Different purpose", "Operations",
            actorId, "Owner", now.AddMinutes(2));
        var otherProgram = service.Create(Uuid.CreateVersion4(), "Payroll", "Process payroll",
            "Operations", actorId, "Owner", now.AddMinutes(2));

        // Assert
        Assert.True(replay.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(changed.Error).Kind);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(otherProgram.Error).Kind);
        Assert.Equal(2, new AggregateScenario<ClientService>(service).PendingEvents.Count);
    }

    [Fact]
    public void ShouldRejectStaleRevisionAndInvalidRetirementGivenServiceHistory()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var serviceId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

        // Act
        var service = new ClientService(tenantId, serviceId);


        // Assert
        Assert.True(service.Create(programId, "Payroll", "Process payroll", "Operations",
            actorId, "Owner", now).IsSuccess);
        var stale = Assert.IsType<RequestError>(service.Revise(0,
            "Payroll", "Changed", "Operations", actorId, "Owner", now).Error);
        Assert.Equal(RequestErrorKind.Conflict, stale.Kind);
        Assert.Contains("revision: 1", stale.Message, StringComparison.Ordinal);
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
