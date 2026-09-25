using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

sealed class EventSourcedClientServiceActivity(IAggregateReader reader) : IClientServiceActivity
{
    public async ValueTask<bool> IsActiveAsync(Uuid tenantId, Uuid programId, Uuid serviceId,
        CancellationToken ct = default)
    {
        var service = await reader.HydrateAsync(new ClientService(tenantId, serviceId), ct)
            .ConfigureAwait(false);
        return service.IsActive && service.ProgramId == programId;
    }
}
