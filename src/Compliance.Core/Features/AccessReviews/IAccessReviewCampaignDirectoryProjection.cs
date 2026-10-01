using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

public interface IAccessReviewCampaignDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
