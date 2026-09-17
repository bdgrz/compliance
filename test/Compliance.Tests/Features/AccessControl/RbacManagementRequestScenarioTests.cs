using System.Security.Claims;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

/// <summary>
///     Runs <see cref="RegisterMember" /> through Portia's real composed lifecycle to confirm
///     <see cref="RbacManagementAuthorizer" /> is actually wired to it via
///     <see cref="IRbacManagementRequest" />, not just correct in isolation.
/// </summary>
public sealed class RbacManagementRequestScenarioTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldAllowASystemActorRegardlessOfPermissions()
    {
        await using var provider = BuildProvider(allowed: false);

        await RequestScenario.For(provider)
            .GivenActor(RequestActor.System)
            .When(new RegisterMember(TenantId, Uuid.CreateVersion4()))
            .ExpectAuthorized()
            .ExpectHandled()
            .ExpectSuccess();
    }

    [Fact]
    public async Task ShouldDenyAnActorWithoutTheTenantRbacManagePermission()
    {
        await using var provider = BuildProvider(allowed: false);

        await RequestScenario.For(provider)
            .GivenActor(BdgrzActor())
            .When(new RegisterMember(TenantId, Uuid.CreateVersion4()))
            .ExpectDenied(RequestErrorKind.Forbidden)
            .ExpectNotHandled();
    }

    [Fact]
    public async Task ShouldHandleAnActorWithTheTenantRbacManagePermission()
    {
        await using var provider = BuildProvider(allowed: true);

        await RequestScenario.For(provider)
            .GivenActor(BdgrzActor())
            .When(new RegisterMember(TenantId, Uuid.CreateVersion4()))
            .ExpectAuthorized()
            .ExpectHandled()
            .ExpectSuccess();
    }

    static ServiceProvider BuildProvider(bool allowed)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddSingleton<IPermissionAuthorizer>(new FakePermissionAuthorizer(allowed));
        services.AddPortia()
            .AddRequestHandler<RegisterMemberHandler>()
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
