using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     A linked, time-bounded acceptance owned elsewhere: a risk_acceptance (R1-07) or a
///     separation-of-duties waiver. State is active or expired when read. It never closes the
///     finding or counts as remediation.
/// </summary>
public sealed record FindingAcceptanceView(string Kind, Uuid RecordId, Uuid? DecisionId,
    DateTimeOffset ExpiresAt, ActorReference LinkedBy, DateTimeOffset LinkedAt,
    string State = "active");
