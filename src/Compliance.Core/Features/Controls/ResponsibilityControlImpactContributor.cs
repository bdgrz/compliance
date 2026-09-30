using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>
///     Reports unrevoked responsibilities on the current approved version and the pending exact
///     revision. Control responsibilities are recorded on the Control stream, so the aggregate is
///     the authoritative, lag-free source. Existing assignments are never rewritten by approval.
/// </summary>
public sealed class ResponsibilityControlImpactContributor : IControlImpactContributor
{
    public string Context => "responsibilities";

    public ValueTask<Result<ControlImpactContribution>> ContributeAsync(ControlDraft subject,
        IReadOnlyList<ControlChange> changes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(subject);
        var records = new List<ControlAffectedRecord>();
        if (subject.ApprovedVersion is { } current)
            records.AddRange(subject.RetainedResponsibilities(current.VersionId, current.Revision)
                .Select(assignment => new ControlAffectedRecord(subject.TenantId, Context,
                    "responsibility_assignment", assignment.AssignmentId, current.VersionId,
                    "Retained " + assignment.Type + " responsibility on the current approved version.")));
        if (subject.PendingTargetId is { } target)
            records.AddRange(subject.RetainedResponsibilities(target, subject.Revision)
                .Select(assignment => new ControlAffectedRecord(subject.TenantId, Context,
                    "responsibility_assignment", assignment.AssignmentId, target,
                    "Assigned " + assignment.Type + " responsibility on the pending revision.")));
        return ValueTask.FromResult(Result<ControlImpactContribution>.Success(
            new ControlImpactContribution(Context, "complete", "authoritative_source",
                records, true)));
    }
}
