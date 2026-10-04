using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>
///     An accountable work item linked to its exact source record: control_occurrence,
///     occurrence_review, corrective_action, evidence_request, risk_treatment_action, or
///     risk_treatment_action_review. It has no completion state of its own; completing the source
///     through <c>ActionPath</c> removes it. <c>Responsible</c> is the source holder;
///     <c>AssigneeMemberId</c> is who is accountable now, or null for unclaimed team work.
///     <c>EscalatedBy</c> is member or system when escalated.
/// </summary>
public sealed record WorkQueueItemView(Uuid WorkItemId, string Kind, Uuid SourceId,
    Uuid? ControlId, Uuid? FindingId, string Summary, string Reason, DateOnly? DueOn,
    bool Overdue, string? Materiality, string NextAction, string ActionPath,
    OperatingHolder Responsible, Uuid? AssigneeMemberId, long AssignmentRevision, bool Escalated,
    string? EscalatedBy, DateTimeOffset CreatedAt);
