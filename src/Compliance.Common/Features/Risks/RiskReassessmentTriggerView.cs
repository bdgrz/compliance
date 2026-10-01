using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     A durable M0-D10 reassessment trigger. <c>TriggerKind</c> is <c>method_changed</c> or
///     <c>boundary_changed</c>; <c>SourceReference</c> names the exact changed record. Status is
///     evaluated when read: open until an inherent assessment is recorded at or after
///     <c>RaisedAt</c>, then resolved.
/// </summary>
public sealed record RiskReassessmentTriggerView(Uuid TriggerId, Uuid RiskId,
    string TriggerKind, string SourceReference, DateTimeOffset RaisedAt,
    string Status = "open");
