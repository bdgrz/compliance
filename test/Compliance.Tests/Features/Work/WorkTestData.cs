using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Tests.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Work;

/// <summary>Seeds corrective actions owned by one member, each due on the given dates.</summary>
static class WorkTestData
{
    public static async Task<FindingView> AddActionsAsync(OperationsFixture fixture,
        Uuid ownerMemberId, params DateOnly[] dueDates)
    {
        var registration = await fixture.AsAsync(fixture.LeadUserId,
            fixture.Raise(fixture.LeadMemberId));
        FindingView? finding = null;
        var revision = registration.Revision;
        foreach (var dueOn in dueDates)
        {
            finding = await fixture.AsAsync(fixture.LeadUserId, new AddCorrectiveAction(
                fixture.TenantId, fixture.ProgramId, registration.FindingId, revision,
                $"Fix item due {dueOn:yyyy-MM-dd}.", ownerMemberId, dueOn));
            revision = finding.Revision;
        }
        return finding!;
    }
}
