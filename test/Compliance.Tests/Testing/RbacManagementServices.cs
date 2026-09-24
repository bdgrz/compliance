using System.Security.Claims;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
/// Composes the smallest provider that runs a tenant RBAC request through Portia's real
/// lifecycle: the production <see cref="RbacManagementAuthorizer"/> over an active tenant and a
/// member actor, an in-memory event store, and the handler the test registers.
/// </summary>
static class RbacManagementServices
{
    public static ServiceProvider Build(bool allowed, Action<PortiaBuilder> handlers)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddSingleton<IPermissionAuthorizer>(new RecordingPermissionAuthorizer(allowed));
        services.AddSingleton<ITenantActivity, ActiveTenant>();
        services.AddSingleton<ITenantMembershipDirectoryReader, AlwaysMemberDirectory>();
        handlers(services.AddPortia().AddRequestAuthorizer<RbacManagementAuthorizer>());
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    public static ClaimsPrincipal Actor() => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())],
        "BdgrzSession"));

    public static async Task SeedAsync<TAggregate>(IServiceProvider provider, TAggregate aggregate,
        Func<TAggregate, Result> operation) where TAggregate : Aggregate
    {
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IAggregateExecutor>()
            .ExecuteAsync(aggregate, item => AggregateOutcome.Commit(operation(item)),
                new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        Assert.True(result.IsSuccess);
    }
}
