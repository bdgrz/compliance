using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

sealed class CurrentRatifiedIndependenceRulesReader(IAggregateReader reader,
    ProfessionalDutyAuthorityReader duties)
{
    public async ValueTask<IndependenceRuleVersionView?> ReadActiveAsync(CancellationToken ct)
    {
        var rules = await reader.HydrateAsync(new IndependenceRuleCatalog(), ct).ConfigureAwait(false);
        var ratifications = await reader.HydrateAsync(new IndependenceRuleRatificationCatalog(), ct)
            .ConfigureAwait(false);
        if (ratifications.Active is not { } active)
            return null;
        var currentDuty = await duties.ReadCurrentAsync(active.UserId, FirmProfessionalDuty.RuleRatifier, null, ct)
            .ConfigureAwait(false);
        if (currentDuty is null || currentDuty.Staff.StaffMemberId != active.StaffMemberId ||
            currentDuty.Staff.Revision != active.DirectoryStaffRevision ||
            currentDuty.Designation.UserId != active.UserId ||
            currentDuty.Designation.Duty != FirmProfessionalDuty.RuleRatifier ||
            currentDuty.Designation.TenantId is not null ||
            currentDuty.Designation.DesignationId != active.DutyDesignationId ||
            active.DutyRevision < currentDuty.ContinuityStartRevision ||
            active.DutyRevision > currentDuty.Designation.Revision)
            return null;
        var draft = rules.Versions.SingleOrDefault(item => item.Version == active.RuleVersion);
        if (draft is null)
            return null;
        var projected = IndependenceRuleVersionProjection.ApplyRatification(draft, active);
        return projected.IsRatified ? projected : null;
    }
}
