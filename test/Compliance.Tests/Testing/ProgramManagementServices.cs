using System.Security.Claims;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
/// Composes the smallest provider that runs a program-management request through Portia's real
/// lifecycle: the production <see cref="ProgramManagementAuthorizer"/> over an active tenant and
/// a member actor, an in-memory event store, and the handlers the test registers.
/// </summary>
static class ProgramManagementServices
{
    public static ServiceProvider Build(IPermissionAuthorizer permissions,
        Action<PortiaBuilder> handlers, Action<IServiceCollection>? services = null)
    {
        var collection = new ServiceCollection();
        collection.AddSingleton<IEventStore>(new InMemoryEventStore());
        collection.AddSingleton(permissions);
        collection.AddSingleton<IAccessGrantPermissionAuthorizer>(
            new PermissionBackedAccessGrantPermissionAuthorizer(permissions));
        collection.AddSingleton<IProgramResourceScopeResolver, TestProgramResourceScopeResolver>();
        collection.AddSingleton<ITenantActivity, ActiveTenant>();
        collection.AddSingleton<ITenantMembershipDirectoryReader, AlwaysMemberDirectory>();
        collection.AddSingleton(TimeProvider.System);
        services?.Invoke(collection);
        handlers(collection.AddPortia().AddRequestAuthorizer<ProgramManagementAuthorizer>());
        return collection.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    public static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString()),
            new Claim("email", "author@example.com")], "BdgrzSession"));

    public static IProgramResourceScopeResolver ResourceScopes(Uuid? programId = null,
        bool applicationExists = true) =>
        new TestProgramResourceScopeResolver(programId, applicationExists);

    public static async Task SeedAsync<TAggregate>(IServiceProvider provider, TAggregate aggregate,
        Func<TAggregate, Result> operation) where TAggregate : Aggregate
    {
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IAggregateExecutor>()
            .ExecuteAsync(aggregate, item => AggregateOutcome.Commit(operation(item)),
                new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        Assert.True(result.IsSuccess);
    }

    public static async Task SeedAsync<TAggregate, TOut>(IServiceProvider provider,
        TAggregate aggregate, Func<TAggregate, Result<TOut>> operation) where TAggregate : Aggregate
    {
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IAggregateExecutor>()
            .ExecuteAsync(aggregate, item => AggregateOutcome.Commit(operation(item)),
                new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        Assert.True(result.IsSuccess);
    }

    public static async Task<TAggregate> HydrateAsync<TAggregate>(IServiceProvider provider,
        TAggregate aggregate) where TAggregate : Aggregate
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(aggregate, CancellationToken.None);
    }
}

sealed class TestProgramResourceScopeResolver(Uuid? programId = null, bool applicationExists = true)
    : IProgramResourceScopeResolver
{
    public ValueTask<bool> IsTenantProgramAsync(Uuid tenantId, Uuid requestedProgramId,
        CancellationToken ct = default) => ValueTask.FromResult(true);

    public ValueTask<Uuid?> ResolveProgramIdAsync(Uuid tenantId, IProgramResourceRequest request,
        CancellationToken ct = default) => ValueTask.FromResult(programId);

    public ValueTask<bool> IsTenantApplicationAsync(Uuid tenantId, Uuid applicationId,
        CancellationToken ct = default) => ValueTask.FromResult(applicationExists);
}
