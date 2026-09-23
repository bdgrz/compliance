using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>Seeds configured operators once; concurrent API and worker starts share one stream.</summary>
public sealed class PlatformOperatorRosterBootstrap(IServiceScopeFactory scopes,
    PlatformOperatorAuthority configuration) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var roster = await reader.HydrateAsync(new PlatformOperatorRoster(), cancellationToken)
            .ConfigureAwait(false);
        if (roster.IsInitialized)
            return;
        if (configuration.BootstrapUserIds.Count == 0)
        {
            if (configuration.DeveloperAuthentication)
                return;
            throw new InvalidOperationException(
                "Configure PlatformOperators:UserIds to seed the initial operator roster.");
        }

        try
        {
            var result = await services.GetRequiredService<IRequestBus>().SendAsync(
                new SeedPlatformOperatorRoster([.. configuration.BootstrapUserIds]),
                RequestActor.System, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess)
                throw new InvalidOperationException($"Operator roster bootstrap failed: {result.Error.Message}");
        }
        catch (EventStreamConcurrencyException)
        {
            // Another host may have seeded the fixed stream at the same time.
            roster = await reader.HydrateAsync(new PlatformOperatorRoster(), cancellationToken)
                .ConfigureAwait(false);
            if (!roster.IsInitialized)
                throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
