using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>
///     A not_met step. A material deviation routes to the finding <c>FindingId</c> and requires a
///     retest; a minor deviation needs a disposition of corrected or accepted_with_waiver. Status
///     is pending_disposition, dispositioned, pending_routing, or routed_to_finding.
/// </summary>
public sealed record EvaluationDeviationView(Uuid DeviationId, Uuid StepId, int Round,
    string Classification, string Description, string Status, Uuid? FindingId,
    string? Disposition, string? DispositionRationale, Uuid? WaiverId,
    ActorReference DetectedBy, DateTimeOffset DetectedAt);
