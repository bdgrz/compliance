using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

public interface ICampaignDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
