using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>
///     A joiner, mover, or leaver observed by comparing accepted roster versions (M0-D06). An
///     observation is open compliance work; it never grants or revokes platform access.
///     <c>ChangedFields</c> names the unrestricted terms a mover changed and never includes the
///     restricted manager chain. <c>Status</c> is <c>open</c> until an attributed
///     <c>Resolution</c> closes it as <c>resolved</c> or <c>dismissed</c>.
/// </summary>
public sealed record WorkforceObservationView(Uuid TenantId, Uuid ObservationId, string Kind,
    string Status, Uuid RelationshipId, Uuid PersonId, string SourceWorkerId,
    long RelationshipRevision, DateOnly EffectiveDate, IReadOnlyList<string> ChangedFields,
    ActorReference ObservedFrom, DateTimeOffset ObservedAt,
    WorkforceObservationResolutionView? Resolution = null);
