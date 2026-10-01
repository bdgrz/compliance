using Bdgrz.Compliance.Features.ControlMappings;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>
///     Reports the program's active and pending criterion mappings that name the Control. The
///     program mapping ledger is the authoritative, lag-free source. Approval never remaps them:
///     a mapping to a superseded or retired version must be remapped or retired explicitly.
/// </summary>
public sealed class MappingControlImpactContributor(IAggregateReader reader)
    : IControlImpactContributor
{
    public string Context => "mappings";

    public async ValueTask<Result<ControlImpactContribution>> ContributeAsync(
        ControlDraft subject, IReadOnlyList<ControlChange> changes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(subject);
        var ledger = await reader.HydrateAsync(new ControlCriterionMappingLedger(
            subject.TenantId, subject.ProgramId), ct).ConfigureAwait(false);
        var retirement = !subject.HasOpenDraft;
        var records = ledger.ReadAll()
            .Where(mapping => mapping.ControlId == subject.Id &&
                mapping.Status is "active" or "pending")
            .Select(mapping => new ControlAffectedRecord(subject.TenantId, Context,
                "control_criterion_mapping", mapping.MappingId,
                mapping.ActiveControlVersionId ?? mapping.Versions[^1].ControlVersionId,
                (mapping.Status == "pending"
                    ? "A pending mapping proposal for " + mapping.CriterionIdentifier
                    : "The accepted mapping to " + mapping.CriterionIdentifier) +
                (retirement
                    ? " names this Control; retire it or map the criterion to another control."
                    : " names this Control; remap it to the successor version after approval.")))
            .ToArray();
        return ControlImpactRecords.Contribution(Context, records);
    }
}
