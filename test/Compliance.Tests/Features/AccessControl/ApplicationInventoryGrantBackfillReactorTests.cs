using System.Security.Claims;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class ApplicationInventoryGrantBackfillReactorTests
{
    [Fact]
    public async Task ShouldBackfillOnlyInventoryGrantsGivenHistoricalTenantRegistration()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var bus = new RecordingRequestBus();
        var reactor = new ApplicationInventoryGrantBackfillReactor(
            new InMemoryProjectionCheckpointStore(), bus);
        var context = new Context(new TenantRegistered(tenantId, Uuid.CreateVersion4(), "Acme", "acme"));

        // Act
        await reactor.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(2, bus.Dispatched.Count);
        Assert.All(bus.Dispatched, request =>
        {
            var grant = Assert.IsType<AssignRolePermission>(request);
            Assert.Equal(tenantId, grant.TenantId);
            Assert.Equal(RbacPermissions.ApplicationInventoryManage, grant.Permission);
        });
        Assert.Contains(bus.Dispatched, request => request is AssignRolePermission
        {
            RoleId: var roleId,
        } && roleId == BuiltInRbac.TenantAdministrationRoleId(tenantId));
        Assert.Contains(bus.Dispatched, request => request is AssignRolePermission
        {
            RoleId: var roleId,
        } && roleId == BuiltInRbac.ComplianceManagementRoleId(tenantId));
    }

    sealed class RecordingRequestBus : IRequestBus
    {
        public List<IRequestBase> Dispatched { get; } = [];

        public RequestDispatchContext CreateContext(ClaimsPrincipal actor,
            RequestMetadata? metadata = null) => new(actor, metadata: metadata);

        public ValueTask<Result> AuthorizeAsync(IRequestBase request,
            RequestDispatchContext context, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public ValueTask<Result> DispatchAsync(IRequest request,
            RequestDispatchContext context, CancellationToken ct = default)
        {
            Dispatched.Add(request);
            return ValueTask.FromResult(Result.Success);
        }

        public ValueTask<Result<TOut>> DispatchAsync<TOut>(IRequest<TOut> request,
            RequestDispatchContext context, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<TOut> DispatchStreamAsync<TOut>(IStreamRequest<TOut> request,
            RequestDispatchContext context, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    sealed class Context(TenantRegistered trigger) : IReactorContext<TenantRegistered>
    {
        public TenantRegistered Trigger { get; } = trigger;
        public DomainEventRecord Source { get; } = new(
            new EventStreamAddress(trigger.TenantId.ToString(), "tenants", trigger.TenantId.ToString()),
            trigger, 0, EventCursor.Start);
        public ClaimsPrincipal Actor => RequestActor.System;
        public Uuid ExecutionId { get; } = Uuid.CreateVersion4();
        public Uuid CorrelationId { get; } = Uuid.CreateVersion4();
        public Uuid CauseId { get; } = Uuid.CreateVersion4();
        public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;
    }
}
