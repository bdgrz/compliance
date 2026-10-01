using Bdgrz.Compliance.Features.Risks;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>
///     Reports proposed and accepted risk treatment assertions that name the Control. The
///     program's risk governance ledger is the authoritative, lag-free source. An assertion names
///     an exact control version, so approval never silently carries it to a successor.
/// </summary>
public sealed class RiskTreatmentControlImpactContributor(IAggregateReader reader)
    : IControlImpactContributor
{
    public string Context => "risk_treatments";

    public async ValueTask<Result<ControlImpactContribution>> ContributeAsync(
        ControlDraft subject, IReadOnlyList<ControlChange> changes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(subject);
        var ledger = await reader.HydrateAsync(new RiskGovernanceLedger(subject.TenantId,
            subject.ProgramId), ct).ConfigureAwait(false);
        var retirement = !subject.HasOpenDraft;
        var records = ledger.TreatmentsForControl(subject.Id)
            .Select(treatment => new ControlAffectedRecord(subject.TenantId, Context,
                "risk_control_treatment", treatment.TreatmentId, treatment.ControlVersionId,
                (treatment.Status == "accepted"
                    ? "An accepted treatment of risk " + treatment.RiskId
                    : "A proposed treatment of risk " + treatment.RiskId) +
                (retirement
                    ? " relies on this Control; retiring it removes the treatment basis for the residual assessment."
                    : " names this Control version; assert and review the successor version to keep it.")))
            .ToArray();
        return ControlImpactRecords.Contribution(Context, records);
    }
}
