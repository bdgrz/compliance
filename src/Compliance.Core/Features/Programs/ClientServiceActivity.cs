using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public interface IClientServiceActivity
{
    ValueTask<bool> IsActiveAsync(Uuid tenantId, Uuid serviceId,
        CancellationToken ct = default);
}

sealed class EventSourcedClientServiceActivity(IAggregateReader reader) : IClientServiceActivity
{
    public async ValueTask<bool> IsActiveAsync(Uuid tenantId, Uuid serviceId,
        CancellationToken ct = default)
    {
        var service = await reader.HydrateAsync(new ClientService(tenantId, serviceId), ct)
            .ConfigureAwait(false);
        return service.IsActive;
    }
}
