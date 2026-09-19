using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Hosting;

/// <summary>
/// Prevents a newly reserved top-level route from shadowing an existing organization link.
/// The event-sourced slug owner includes retired slugs, so a lagging projection cannot hide
/// a collision during startup.
/// </summary>
public sealed class ReservedTenantRouteCollisionCheck(IServiceScopeFactory scopes) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        await CheckAsync(scope.ServiceProvider.GetRequiredService<IAggregateReader>(),
            TenantSlugs.ReservedRouteSegments, cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    internal static async Task CheckAsync(IAggregateReader reader, IEnumerable<string> routes,
        CancellationToken ct)
    {
        foreach (var route in routes.Order(StringComparer.Ordinal))
        {
            if (!TenantSlugs.TryNormalizeForLookup(route, out var slug))
                continue;
            var reservation = await reader.HydrateAsync(new TenantSlug(slug, allowReserved: true), ct)
                .ConfigureAwait(false);
            if (reservation.OwningTenantId is not null)
                throw new InvalidOperationException(
                    $"The reserved top-level route '{slug}' is already an organization slug.");
        }
    }
}
