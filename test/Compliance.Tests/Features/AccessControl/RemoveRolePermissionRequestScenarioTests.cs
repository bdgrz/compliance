using System.Security.Claims;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class RemoveRolePermissionRequestScenarioTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid RoleId = Uuid.CreateVersion4();
    const string Permission = "controls.read";

    [Fact]
    public async Task ShouldRemoveAnAssignedPermissionGivenTenantRbacManagePermission()
    {
        // Arrange
        await using var provider = BuildProvider(allowed: true);
        await Seed(provider);

        await RequestScenario.For(provider)
            .GivenActor(BdgrzActor())
            // Act
            .When(new RemoveRolePermission(TenantId, RoleId, Permission))
            // Assert
            .ExpectAuthorized()
            .ExpectHandled()
            .ExpectSuccess();
    }

    [Fact]
    public async Task ShouldDenyGivenAnActorWithoutTheTenantRbacManagePermission()
    {
        // Arrange
        await using var provider = BuildProvider(allowed: false);
        await Seed(provider);

        await RequestScenario.For(provider)
            .GivenActor(BdgrzActor())
            // Act
            .When(new RemoveRolePermission(TenantId, RoleId, Permission))
            // Assert
            .ExpectDenied(RequestErrorKind.Forbidden)
            .ExpectNotHandled();
    }

    static async Task Seed(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        await executor.ExecuteAsync(
            new RolePermission(TenantId, RoleId, Permission),
            rolePermission => AggregateOutcome.Commit(rolePermission.Assign()),
            new RequestDispatchContext(RequestActor.System),
            CancellationToken.None);
    }

    static ServiceProvider BuildProvider(bool allowed)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddSingleton<IPermissionAuthorizer>(new FakePermissionAuthorizer(allowed));
        services.AddSingleton<ITenantActivity, ActiveTenant>();
        services.AddSingleton<ITenantMembershipDirectoryReader, AlwaysMemberDirectory>();
        services.AddPortia()
            .AddRequestHandler<RemoveRolePermissionHandler>()
            .AddRequestAuthorizer<RbacManagementAuthorizer>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    static ClaimsPrincipal BdgrzActor() => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())], "BdgrzSession"));

    sealed class FakePermissionAuthorizer(bool allowed) : IPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(
            Uuid tenantId,
            Uuid userId,
            Uuid memberId,
            string permission,
            CancellationToken ct = default) => ValueTask.FromResult(allowed);
    }
}
