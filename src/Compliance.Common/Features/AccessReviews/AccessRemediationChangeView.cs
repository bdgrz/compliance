using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>A provider-side change reported for an item; it is not verification.</summary>
public sealed record AccessRemediationChangeView(Uuid ChangeId, string Reference,
    string Description, DateTimeOffset ChangedAt, ActorReference RecordedBy,
    DateTimeOffset RecordedAt);
