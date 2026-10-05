using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

public interface IPolicyCampaignWorkItemProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
