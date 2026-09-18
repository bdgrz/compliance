using System.Security.Claims;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class DeleteRoleRequestScenarioTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid RoleId = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldDeleteADefinedRoleGivenTenantRbacManagePermission()
    {
        await using var provider = BuildProvider(allowed: true);
        await Seed(provider);

        await RequestScenario.For(provider)
            .GivenActor(BdgrzActor())
            .When(new DeleteRole(TenantId, RoleId))
            .ExpectAuthorized()
            .ExpectHandled()
            .ExpectSuccess();
    }

    [Fact]
    public async Task ShouldDenyGivenAnActorWithoutTheTenantRbacManagePermission()
    {
        await using var provider = BuildProvider(allowed: false);
        await Seed(provider);

        await RequestScenario.For(provider)
            .GivenActor(BdgrzActor())
            .When(new DeleteRole(TenantId, RoleId))
            .ExpectDenied(RequestErrorKind.Forbidden)
            .ExpectNotHandled();
    }

    static async Task Seed(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        await executor.ExecuteAsync(
            new Role(TenantId, RoleId),
            role => AggregateOutcome.Commit(role.Define("Reviewer")),
            new RequestDispatchContext(RequestActor.System),
            CancellationToken.None);
    }

    static ServiceProvider BuildProvider(bool allowed)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddSingleton<IPermissionAuthorizer>(new FakePermissionAuthorizer(allowed));
        services.AddPortia()
            .AddRequestHandler<DeleteRoleHandler>()
            .AddRequestAuthorizer<RbacManagementAuthorizer>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    static ClaimsPrincipal BdgrzActor() => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())], "BdgrzSession"));

    sealed class FakePermissionAuthorizer(bool allowed) : IPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(
            Uuid tenantId,
            Uuid memberId,
            string permission,
            CancellationToken ct = default) => ValueTask.FromResult(allowed);
    }
}
