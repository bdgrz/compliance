using System.Security.Claims;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class TenantManagerInvariantTests
{
    [Fact]
    public async Task ShouldKeepOneActiveAdministratorGivenConcurrentMutualSuspension()
    {
        // Arrange
        await using var tenant = await Tenant.CreateAsync(administrators: 2);
        var (first, second) = (tenant.Administrators[0], tenant.Administrators[1]);
        tenant.HoldGuardReads(2);

        // Act
        var results = await Task.WhenAll(
            tenant.SuspendAsync(actor: first, target: second).AsTask(),
            tenant.SuspendAsync(actor: second, target: first).AsTask());
        var firstSuspended = await tenant.IsSuspendedAsync(first);
        var secondSuspended = await tenant.IsSuspendedAsync(second);

        // Assert
        Assert.Single(results, result => result.IsSuccess);
        var denied = Assert.Single(results, result => !result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(denied.Error).Kind);
        Assert.False(Assert.IsType<RequestError>(denied.Error).IsTransient);
        Assert.True(firstSuspended ^ secondSuspended, "Exactly one administrator must stay active.");
    }

    [Fact]
    public async Task ShouldDenySuspensionBySuspendedAdministratorGivenSequentialSuspension()
    {
        // Arrange
        await using var tenant = await Tenant.CreateAsync(administrators: 2);
        var (first, second) = (tenant.Administrators[0], tenant.Administrators[1]);

        // Act
        var self = await tenant.SuspendAsync(actor: first, target: first);
        var suspended = await tenant.SuspendAsync(actor: first, target: second);
        var retaliation = await tenant.SuspendAsync(actor: second, target: first);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(self.Error).Kind);
        Assert.True(suspended.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(retaliation.Error).Kind);
        Assert.False(await tenant.IsSuspendedAsync(first));
        Assert.True(await tenant.IsSuspendedAsync(second));
    }

    [Fact]
    public async Task ShouldRestoreWitnessGivenReinstatedAdministrator()
    {
        // Arrange
        await using var tenant = await Tenant.CreateAsync(administrators: 2);
        var (first, second) = (tenant.Administrators[0], tenant.Administrators[1]);
        Assert.True((await tenant.SuspendAsync(actor: first, target: second)).IsSuccess);

        // Act
        var reinstated = await tenant.ReinstateAsync(actor: first, target: second);
        var reversed = await tenant.SuspendAsync(actor: second, target: first);

        // Assert
        Assert.True(reinstated.IsSuccess);
        Assert.True(reversed.IsSuccess);
        Assert.True(await tenant.IsSuspendedAsync(first));
        Assert.False(await tenant.IsSuspendedAsync(second));
    }

    [Fact]
    public async Task ShouldRejectRemovingLastAdministratorGivenAdministratorsTeam()
    {
        // Arrange
        await using var tenant = await Tenant.CreateAsync(administrators: 1);
        var only = tenant.Administrators[0];

        // Act
        var removed = await tenant.RemoveFromAdministratorsAsync(actor: only, target: only);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(removed.Error).Kind);
        Assert.True(await tenant.IsAdministratorAsync(only));
    }

    [Fact]
    public async Task ShouldRemoveAdministratorGivenAnotherActiveAdministrator()
    {
        // Arrange
        await using var tenant = await Tenant.CreateAsync(administrators: 2);
        var (first, second) = (tenant.Administrators[0], tenant.Administrators[1]);

        // Act
        var removed = await tenant.RemoveFromAdministratorsAsync(actor: first, target: second);
        var lastRemoval = await tenant.RemoveFromAdministratorsAsync(actor: first, target: first);
        var reassigned = await tenant.AssignToAdministratorsAsync(actor: first, target: second);
        var nowRemovable = await tenant.RemoveFromAdministratorsAsync(actor: second, target: first);

        // Assert
        Assert.True(removed.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(lastRemoval.Error).Kind);
        Assert.True(reassigned.IsSuccess);
        Assert.True(nowRemovable.IsSuccess);
        Assert.False(await tenant.IsAdministratorAsync(first));
        Assert.True(await tenant.IsAdministratorAsync(second));
    }

    [Fact]
    public async Task ShouldRejectDismantlingBuiltInAdministrationGivenRoleOrPermissionRemoval()
    {
        // Arrange
        await using var tenant = await Tenant.CreateAsync(administrators: 1);
        var actor = tenant.Administrators[0];

        // Act
        var role = await tenant.SendAsync(actor, new RemoveTeamRole(tenant.TenantId,
            BuiltInRbac.AdministratorsTeamId(tenant.TenantId),
            BuiltInRbac.TenantAdministrationRoleId(tenant.TenantId)));
        var manage = await tenant.SendAsync(actor, new RemoveRolePermission(tenant.TenantId,
            BuiltInRbac.TenantAdministrationRoleId(tenant.TenantId), RbacPermissions.TenantRbacManage));
        var access = await tenant.SendAsync(actor, new RemoveRolePermission(tenant.TenantId,
            BuiltInRbac.TenantAdministrationRoleId(tenant.TenantId), RbacPermissions.TenantAccess));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(role.Error).Kind);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(manage.Error).Kind);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(access.Error).Kind);
    }

    sealed class Tenant : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly GatedReader _reader;

        Tenant(ServiceProvider provider, GatedReader reader, IReadOnlyList<Uuid> administrators)
        {
            _provider = provider;
            _reader = reader;
            Administrators = administrators;
        }

        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public IReadOnlyList<Uuid> Administrators { get; }

        public static async Task<Tenant> CreateAsync(int administrators)
        {
            var services = new ServiceCollection();
            var store = new InMemoryEventStore();
            services.AddSingleton<IEventStore>(store);
            services.AddSingleton<IDomainEventReader>(store);
            services.AddSingleton(TimeProvider.System);
            services.AddPortia();
            var provider = services.BuildServiceProvider();
            var users = Enumerable.Range(0, administrators).Select(_ => Uuid.CreateVersion4()).ToArray();
            var tenant = new Tenant(provider,
                new GatedReader(provider.GetRequiredService<IAggregateReader>()), users);
            foreach (var user in users)
            {
                await tenant.SeedAsync(new Member(tenant.TenantId, user), member => member.Register());
                await tenant.SeedAsync(new TeamMember(tenant.TenantId,
                    BuiltInRbac.AdministratorsTeamId(tenant.TenantId), RbacIds.Member(tenant.TenantId, user)),
                    member => member.Assign());
            }
            return tenant;
        }

        public void HoldGuardReads(int count) => _reader.Hold(count);

        public ValueTask<Result> SuspendAsync(Uuid actor, Uuid target) =>
            SendAsync(actor, new SuspendMember(TenantId, target, "Mutual suspension test."));

        public ValueTask<Result> ReinstateAsync(Uuid actor, Uuid target) =>
            SendAsync(actor, new ReinstateMember(TenantId, target));

        public ValueTask<Result> RemoveFromAdministratorsAsync(Uuid actor, Uuid target) =>
            SendAsync(actor, new RemoveTeamMember(TenantId, BuiltInRbac.AdministratorsTeamId(TenantId),
                RbacIds.Member(TenantId, target)));

        public ValueTask<Result> AssignToAdministratorsAsync(Uuid actor, Uuid target) =>
            SendAsync(actor, new AssignTeamMember(TenantId, BuiltInRbac.AdministratorsTeamId(TenantId),
                RbacIds.Member(TenantId, target)));

        public async ValueTask<Result> SendAsync<TRequest>(Uuid actor, TRequest request)
            where TRequest : IRequest
        {
            var executor = new AggregateExecutor(_reader, _provider.GetRequiredService<IAggregateWriter>());
            var managers = new TenantManagerInvariant(_reader, executor, new AdministratorsDirectory(this));
            return request switch
            {
                SuspendMember suspend => await new SuspendMemberHandler(executor, TimeProvider.System,
                    managers, _reader).HandleAsync(new RequestContext<SuspendMember>(suspend, Actor(actor)),
                    CancellationToken.None),
                ReinstateMember reinstate => await new ReinstateMemberHandler(executor,
                    TimeProvider.System, managers).HandleAsync(
                    new RequestContext<ReinstateMember>(reinstate, Actor(actor)), CancellationToken.None),
                RemoveTeamMember remove => await new RemoveTeamMemberHandler(executor, managers)
                    .HandleAsync(new RequestContext<RemoveTeamMember>(remove, Actor(actor)),
                        CancellationToken.None),
                AssignTeamMember assign => await new AssignTeamMemberHandler(executor, managers)
                    .HandleAsync(new RequestContext<AssignTeamMember>(assign, Actor(actor)),
                        CancellationToken.None),
                RemoveTeamRole role => await new RemoveTeamRoleHandler(executor).HandleAsync(
                    new RequestContext<RemoveTeamRole>(role, Actor(actor)), CancellationToken.None),
                RemoveRolePermission permission => await new RemoveRolePermissionHandler(executor)
                    .HandleAsync(new RequestContext<RemoveRolePermission>(permission, Actor(actor)),
                        CancellationToken.None),
                _ => throw new ArgumentException("Unsupported request.", nameof(request)),
            };
        }

        public async Task<bool> IsSuspendedAsync(Uuid user) =>
            (await _provider.GetRequiredService<IAggregateReader>()
                .HydrateAsync(new Member(TenantId, user))).IsSuspended;

        public async Task<bool> IsAdministratorAsync(Uuid user) =>
            (await _provider.GetRequiredService<IAggregateReader>().HydrateAsync(new TeamMember(TenantId,
                BuiltInRbac.AdministratorsTeamId(TenantId), RbacIds.Member(TenantId, user)))).IsAssigned;

        async Task SeedAsync<TAggregate>(TAggregate aggregate, Func<TAggregate, Result> operation)
            where TAggregate : Aggregate
        {
            var result = await _provider.GetRequiredService<IAggregateExecutor>().ExecuteAsync(aggregate,
                item => AggregateOutcome.Commit(operation(item)),
                new RequestDispatchContext(RequestActor.System), CancellationToken.None);
            Assert.True(result.IsSuccess);
        }

        static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "BdgrzSession"));

        public ValueTask DisposeAsync() => _provider.DisposeAsync();

        /// <summary>A stale directory: it lists every seeded administrator regardless of removals.</summary>
        sealed class AdministratorsDirectory(Tenant tenant) : ITeamMemberDirectoryReader
        {
            public ValueTask<Page<TeamMemberView>> ListAsync(Uuid tenantId, Uuid teamId, int? limit,
                string? cursor, string? search, bool descending, CancellationToken ct = default) =>
                ValueTask.FromResult(new Page<TeamMemberView>(
                    teamId == BuiltInRbac.AdministratorsTeamId(tenant.TenantId)
                        ? [.. tenant.Administrators.Select(user =>
                            new TeamMemberView(teamId, RbacIds.Member(tenant.TenantId, user)))]
                        : [], null));
        }
    }

    /// <summary>Holds the first guard reads until all of them have read, forcing an interleaving.</summary>
    sealed class GatedReader(IAggregateReader inner) : IAggregateReader
    {
        readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int _remaining;

        public void Hold(int count) => Volatile.Write(ref _remaining, count);

        public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            var hydrated = await inner.HydrateAsync(aggregate, ct);
            if (aggregate is TenantManagerGuard && Volatile.Read(ref _remaining) > 0)
            {
                if (Interlocked.Decrement(ref _remaining) == 0)
                    _released.TrySetResult();
                await _released.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
            }
            return hydrated;
        }
    }
}
