using System.Security.Claims;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class RoleCleanupReactorTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid RoleId = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldRemoveEveryPermissionAndTeamAssignmentOfTheDeletedRole()
    {
        var firstTeam = Uuid.CreateVersion4();
        var secondTeam = Uuid.CreateVersion4();
        var permissions = new FakeRolePermissionDirectoryReader(
            new RolePermissionView(RoleId, "controls.read"), new RolePermissionView(RoleId, "controls.manage"));
        var teams = new FakeRoleTeamDirectoryReader(
            new RoleTeamView(RoleId, firstTeam), new RoleTeamView(RoleId, secondTeam));
        var bus = new RecordingRequestBus();
        var reactor = new RoleCleanupReactor(new InMemoryProjectionCheckpointStore(), bus, permissions, teams);
        var context = new FakeReactorContext(new RoleDeleted(TenantId, RoleId));

        await reactor.HandleAsync(context, CancellationToken.None);

        Assert.Equal(4, bus.Dispatched.Count);
        Assert.Contains(bus.Dispatched, request =>
            request is RemoveRolePermission removal && removal.Permission == "controls.read");
        Assert.Contains(bus.Dispatched, request =>
            request is RemoveRolePermission removal && removal.Permission == "controls.manage");
        Assert.Contains(bus.Dispatched, request =>
            request is RemoveTeamRole removal && removal.TeamId == firstTeam);
        Assert.Contains(bus.Dispatched, request =>
            request is RemoveTeamRole removal && removal.TeamId == secondTeam);
    }

    [Fact]
    public async Task ShouldDispatchNothingGivenAnAlreadyEmptyRole()
    {
        var permissions = new FakeRolePermissionDirectoryReader();
        var teams = new FakeRoleTeamDirectoryReader();
        var bus = new RecordingRequestBus();
        var reactor = new RoleCleanupReactor(new InMemoryProjectionCheckpointStore(), bus, permissions, teams);
        var context = new FakeReactorContext(new RoleDeleted(TenantId, RoleId));

        await reactor.HandleAsync(context, CancellationToken.None);

        Assert.Empty(bus.Dispatched);
    }

    sealed class FakeRolePermissionDirectoryReader(params RolePermissionView[] items) : IRolePermissionDirectoryReader
    {
        public ValueTask<Page<RolePermissionView>> ListAsync(
            Uuid tenantId,
            Uuid roleId,
            int? limit,
            string? cursor,
            string? search,
            bool descending,
            CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<RolePermissionView>(items, null));
    }

    sealed class FakeRoleTeamDirectoryReader(params RoleTeamView[] items) : IRoleTeamDirectoryReader
    {
        public ValueTask<Page<RoleTeamView>> ListAsync(
            Uuid tenantId,
            Uuid roleId,
            int? limit,
            string? cursor,
            string? search,
            bool descending,
            CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<RoleTeamView>(items, null));
    }

    sealed class RecordingRequestBus : IRequestBus
    {
        public List<IRequestBase> Dispatched { get; } = [];

        public RequestDispatchContext CreateContext(ClaimsPrincipal actor, RequestMetadata? metadata = null) =>
            new(actor, metadata: metadata);

        public ValueTask<Result> AuthorizeAsync(
            IRequestBase request,
            RequestDispatchContext context,
            CancellationToken ct = default) => throw new NotSupportedException();

        public ValueTask<Result> DispatchAsync(IRequest request, RequestDispatchContext context, CancellationToken ct = default)
        {
            Dispatched.Add(request);
            return ValueTask.FromResult(Result.Success);
        }

        public ValueTask<Result<TOut>> DispatchAsync<TOut>(
            IRequest<TOut> request,
            RequestDispatchContext context,
            CancellationToken ct = default) => throw new NotSupportedException();

        public IAsyncEnumerable<TOut> DispatchStreamAsync<TOut>(
            IStreamRequest<TOut> request,
            RequestDispatchContext context,
            CancellationToken ct = default) => throw new NotSupportedException();
    }

    sealed class FakeReactorContext(RoleDeleted trigger) : IReactorContext<RoleDeleted>
    {
        public RoleDeleted Trigger { get; } = trigger;
        public DomainEventRecord Source { get; } = new(
            new EventStreamAddress(TenantId.ToString(), "rbac-roles", RoleId.ToString()),
            trigger,
            0,
            EventCursor.Start);
        public ClaimsPrincipal Actor => RequestActor.System;
        public Uuid ExecutionId { get; } = Uuid.CreateVersion4();
        public Uuid CorrelationId { get; } = Uuid.CreateVersion4();
        public Uuid CauseId { get; } = Uuid.CreateVersion4();
        public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;
    }
}
