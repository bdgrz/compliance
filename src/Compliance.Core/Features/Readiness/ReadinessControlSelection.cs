using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Versioning;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>Reconstructs a control interval from lifecycle decisions known at the assessment instant.</summary>
static class ReadinessControlSelection
{
    public static ControlVersionView? EffectiveVersionAt(ControlDraft control, DateTimeOffset asOf)
    {
        var date = DateOnly.FromDateTime(asOf.UtcDateTime);
        var approvals = control.ReadApprovedVersionHistory()
            .Where(version => version.ApprovedAt <= asOf).ToArray();
        var retirements = control.ReadRetirementHistory()
            .Where(retirement => retirement.DecidedAt <= asOf).ToArray();
        foreach (var version in approvals.Reverse())
        {
            var successor = approvals.SingleOrDefault(candidate =>
                candidate.PredecessorVersionId == version.VersionId);
            var retirement = retirements.SingleOrDefault(candidate => candidate.VersionId == version.VersionId);
            var until = retirement?.EffectiveUntil ?? successor?.EffectiveFrom;
            if (new EffectiveInterval(version.EffectiveFrom, until).Contains(date))
                return version with
                {
                    EffectiveUntil = until,
                    Status = retirement is not null ? "retired" : successor is not null ? "superseded" : "approved",
                };
        }
        return null;
    }
}
