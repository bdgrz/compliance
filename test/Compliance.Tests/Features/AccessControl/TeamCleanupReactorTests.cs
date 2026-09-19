using System.Security.Claims;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class TeamCleanupReactorTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid TeamId = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldRemoveMembersGivenDeletedTeam()
    {
        // Arrange
        var firstMember = Uuid.CreateVersion4();
        var secondMember = Uuid.CreateVersion4();
        var members = new FakeTeamMemberDirectoryReader(
            new TeamMemberView(TeamId, firstMember), new TeamMemberView(TeamId, secondMember));
        var bus = new RecordingRequestBus();
        var reactor = new TeamCleanupReactor(new InMemoryProjectionCheckpointStore(), bus, members);
        var context = new FakeReactorContext(new TeamDeleted(TenantId, TeamId));

        // Act
        await reactor.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(2, bus.Dispatched.Count);
        Assert.Contains(bus.Dispatched, request =>
            request is RemoveTeamMember removal && removal.MemberId == firstMember);
        Assert.Contains(bus.Dispatched, request =>
            request is RemoveTeamMember removal && removal.MemberId == secondMember);
        Assert.All(bus.Dispatched, request =>
        {
            var removal = Assert.IsType<RemoveTeamMember>(request);
            Assert.Equal(TenantId, removal.TenantId);
            Assert.Equal(TeamId, removal.TeamId);
        });
    }

    [Fact]
    public async Task ShouldDispatchNothingGivenAnAlreadyEmptyTeam()
    {
        // Arrange
        var members = new FakeTeamMemberDirectoryReader();
        var bus = new RecordingRequestBus();
        var reactor = new TeamCleanupReactor(new InMemoryProjectionCheckpointStore(), bus, members);
        var context = new FakeReactorContext(new TeamDeleted(TenantId, TeamId));

        // Act
        await reactor.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.Empty(bus.Dispatched);
    }

    sealed class FakeTeamMemberDirectoryReader(params TeamMemberView[] members) : ITeamMemberDirectoryReader
    {
        public ValueTask<Page<TeamMemberView>> ListAsync(
            Uuid tenantId,
            Uuid teamId,
            int? limit,
            string? cursor,
            string? search,
            bool descending,
            CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<TeamMemberView>(members, null));
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

    sealed class FakeReactorContext(TeamDeleted trigger) : IReactorContext<TeamDeleted>
    {
        public TeamDeleted Trigger { get; } = trigger;
        public DomainEventRecord Source { get; } = new(
            new EventStreamAddress(TenantId.ToString(), "rbac-teams", TeamId.ToString()),
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
