using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria;

[Discriminator("bdgrz.criteria-overlay.entry-revised", 1)]
public sealed record CriteriaTextOverlayEntryRevised(Uuid TenantId, Uuid EditionId, string Identifier,
    Uuid RequestId, long Revision, CriteriaTextOverlayContent Content, ActorReference Actor,
    DateTimeOffset RecordedAt) : DomainEvent;
