using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

[Discriminator("bdgrz.provider.review.recorded", 1)]
public sealed record ProviderReviewRecorded(Uuid TenantId, Uuid ReviewId, Uuid ProviderId, Uuid RequestId,
    ProviderReviewContent Content, long ProviderRevision, string? ProviderMateriality,
    long? AssuranceReportRevision, ActorReference Actor, DateTimeOffset RecordedAt) : DomainEvent;
