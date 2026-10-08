using System.Globalization;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Remediation;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

static class FindingClosureWork
{
    public const string Kind = "finding_closure_review";

    public static WorkCandidate? Candidate(FindingClosureWorkState finding)
    {
        if (finding.Closed || finding.Actions.Length == 0 ||
            finding.Actions.Any(static action => action.Status != RemediationLedger.Completed))
            return null;
        var excluded = finding.Actions.Select(static action => action.OwnerMemberId).ToHashSet();
        excluded.Add(finding.OwnerMemberId);
        foreach (var action in finding.Actions)
            if (action.CompletedByMemberId is { } completer)
                excluded.Add(completer);
        var identity = Uuid.CreateVersion5(finding.FindingId,
            $"finding-closure\n{finding.Revision.ToString(CultureInfo.InvariantCulture)}");
        return new WorkCandidate(WorkCandidate.IdFor(identity, Kind), Kind, finding.FindingId,
            null, finding.FindingId, $"Verify and close finding: {finding.Title}",
            $"Completed corrective work at finding revision {finding.Revision} awaits independent verification.",
            finding.DueOn, RemediationLedger.Materiality(finding.Severity), "close",
            $"/api/v1/tenants/{finding.TenantId}/programs/{finding.ProgramId}/findings/{finding.FindingId}/closures",
            new OperatingHolder(OperatingAuthority.ProgramManagerHolder, finding.ProgramId),
            null, excluded, finding.ChangedAt);
    }

    public static WorkCandidate? Candidate(FindingView finding) => Candidate(new FindingClosureWorkState(
        finding.TenantId, finding.ProgramId, finding.FindingId, finding.Revision, finding.Title,
        finding.Severity, finding.OwnerMemberId, finding.DueOn, finding.CorrectiveActions.ToArray(),
        finding.Closure is not null, finding.History[^1].At));
}
