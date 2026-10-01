using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     Why an active control cannot operate as planned: missing_plan, pending_approval,
///     plan_for_superseded_version, owner_inactive, backup_owner_inactive, reviewer_inactive, or
///     missed_occurrence.
/// </summary>
public sealed record ControlOperatingBlockerView(Uuid ControlId, string Identifier,
    Uuid? ControlVersionId, string Kind, string Explanation, Uuid? OccurrenceId = null);
