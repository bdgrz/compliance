using System.Security.Claims;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class TenantRbacBootstrapReactorTests
{
    [Fact]
    public async Task ShouldLeaveInventoryGrantToDedicatedReactorGivenRegisteredTenant()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var ownerId = Uuid.CreateVersion4();
        var bus = new RecordingRequestBus();
        var reactor = new TenantRbacBootstrapReactor(
            new InMemoryProjectionCheckpointStore(), bus);
        var context = new Context(new TenantRegistered(tenantId, ownerId, "Acme", "acme"));

        // Act
        await reactor.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.DoesNotContain(bus.Dispatched, request => request is AssignRolePermission
        {
            Permission: RbacPermissions.ApplicationInventoryManage,
        });
    }

    [Fact]
    public async Task ShouldBootstrapCreatorGivenVerifiedSelfServiceRegistration()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var creatorId = Uuid.CreateVersion4();
        var bus = new RecordingRequestBus();
        var reactor = new TenantRbacBootstrapReactor(new InMemoryProjectionCheckpointStore(), bus);
        var context = new Context(new TenantRegistered(tenantId, creatorId, "Acme", "acme",
            "Acme LLC", CreatorIsAdministrator: true));

        // Act
        await reactor.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.Contains(bus.Dispatched, request => request is RegisterMember
        {
            UserId: var userId, Affiliation: "client_personnel",
        } && userId == creatorId);
        Assert.Contains(bus.Dispatched, request => request is AssignTeamMember
        {
            TeamId: var teamId, MemberId: var memberId,
        } && teamId == BuiltInRbac.AdministratorsTeamId(tenantId) &&
            memberId == RbacIds.Member(tenantId, creatorId));
    }

    [Fact]
    public async Task ShouldPreserveInvitationBootstrapGivenLegacyRegistration()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var operatorId = Uuid.CreateVersion4();
        var bus = new RecordingRequestBus();
        var reactor = new TenantRbacBootstrapReactor(new InMemoryProjectionCheckpointStore(), bus);
        var context = new Context(new TenantRegistered(tenantId, operatorId, "Acme", "acme",
            "Acme LLC", "admin@example.com"));

        // Act
        await reactor.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.DoesNotContain(bus.Dispatched, request => request is RegisterMember);
        Assert.DoesNotContain(bus.Dispatched, request => request is AssignTeamMember);
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
