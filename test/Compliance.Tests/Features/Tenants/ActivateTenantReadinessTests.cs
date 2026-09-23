using System.Globalization;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class ActivateTenantReadinessTests
{
    [Fact]
    public async Task ShouldCheckpointActivationGivenRejectedSlugWithoutAdministratorProjection()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var creatorId = Uuid.CreateVersion4();
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddSingleton<ITenantMembershipDirectoryReader>(new Memberships());
        services.AddSingleton<IPermissionAuthorizer>(new Permissions());
        services.AddPortia()
            .AddRequestHandler<ActivateTenantHandler>()
            .AddRequestAuthorizer<ActivateTenantAuthorizer>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var tenant = new Tenant(tenantId);
        Assert.True(tenant.Register(creatorId, "Rejected", "rejected",
            creatorIsAdministrator: true).IsSuccess);
        Assert.True(tenant.RejectSlug("rejected").IsSuccess);
        await writer.SaveAsync(tenant, new RequestDispatchContext(RequestActor.System));

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>()
            .SendAsync(new ActivateTenant(tenantId, creatorId), RequestActor.System);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False((await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new Tenant(tenantId))).IsActive);
    }

    [Fact]
    public async Task ShouldKeepCreatorProvisioningGivenUnmaterializedAdministratorGrant()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var creatorId = Uuid.CreateVersion4();
        var memberId = RbacIds.Member(tenantId, creatorId);
        var memberships = new Memberships();
        var permissions = new Permissions();
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddSingleton<ITenantMembershipDirectoryReader>(memberships);
        services.AddSingleton<IPermissionAuthorizer>(permissions);
        services.AddPortia()
            .AddRequestHandler<ActivateTenantHandler>()
            .AddRequestAuthorizer<ActivateTenantAuthorizer>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var tenant = new Tenant(tenantId);
        Assert.True(tenant.Register(creatorId, "Acme", "acme",
            creatorIsAdministrator: true).IsSuccess);
        Assert.True(tenant.ConfirmSlug("acme").IsSuccess);
        await writer.SaveAsync(tenant, new RequestDispatchContext(RequestActor.System));
        var member = new Member(tenantId, creatorId);
        Assert.True(member.Register().IsSuccess);
        await writer.SaveAsync(member, new RequestDispatchContext(RequestActor.System));
        var teamMember = new TeamMember(tenantId, BuiltInRbac.AdministratorsTeamId(tenantId), memberId);
        Assert.True(teamMember.Assign().IsSuccess);
        await writer.SaveAsync(teamMember, new RequestDispatchContext(RequestActor.System));

        // Act: event streams exist, but the two authorization projections have not caught up.
        var before = await bus.SendAsync(new ActivateTenant(tenantId, creatorId), RequestActor.System);
        memberships.Ready = true;
        permissions.Ready = true;
        var after = await bus.SendAsync(new ActivateTenant(tenantId, creatorId), RequestActor.System);

        // Assert
        Assert.False(before.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, before.Error.Kind);
        Assert.True(before.Error.IsTransient);
        Assert.True(after.IsSuccess);
        var hydrated = await reader.HydrateAsync(new Tenant(tenantId));
        Assert.True(hydrated.IsActive);
    }

    sealed class Memberships : ITenantMembershipDirectoryReader
    {
        public bool Ready { get; set; }

        public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId,
            CancellationToken ct = default) => ValueTask.FromResult<TenantMembershipView?>(
                Ready ? new TenantMembershipView(userId,
                    Uuid.Parse(tenantId, CultureInfo.InvariantCulture), "client_personnel") : null);

        public ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId,
            CancellationToken ct = default) => ValueTask.FromResult(Ready);

        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit,
            string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }

    sealed class Permissions : IPermissionAuthorizer
    {
        public bool Ready { get; set; }

        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            string permission,
            CancellationToken ct = default) => ValueTask.FromResult(Ready);
    }
}
