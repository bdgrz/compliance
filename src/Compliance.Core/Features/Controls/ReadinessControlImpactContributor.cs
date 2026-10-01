using Bdgrz.Compliance.Features.Readiness;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>
///     Reports whether the program's latest readiness assessment relied on any version of the
///     Control. Recorded assessments are immutable, so the decision never rewrites them; it makes
///     the latest result stale until readiness is reassessed. The readiness ledger is the
///     authoritative, lag-free source.
/// </summary>
public sealed class ReadinessControlImpactContributor(IAggregateReader reader)
    : IControlImpactContributor
{
    public string Context => "readiness";

    public async ValueTask<Result<ControlImpactContribution>> ContributeAsync(
        ControlDraft subject, IReadOnlyList<ControlChange> changes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(subject);
        var ledger = await reader.HydrateAsync(new ReadinessLedger(subject.TenantId,
            subject.ProgramId), ct).ConfigureAwait(false);
        var records = new List<ControlAffectedRecord>();
        if (ledger.Summaries() is [var latest, ..] && ledger.Read(latest.AssessmentId) is { } assessment)
        {
            var versions = subject.ReadVersions().Select(static version => version.VersionId)
                .ToHashSet();
            var relied = assessment.Findings.SelectMany(static finding => finding.Sources)
                .Where(source => source.Kind == "control_version" && versions.Contains(source.Id))
                .Select(static source => source.Id)
                .Distinct()
                .ToArray();
            if (relied.Length > 0)
                records.Add(new ControlAffectedRecord(subject.TenantId, Context,
                    "readiness_assessment", assessment.AssessmentId, relied[0],
                    "The latest readiness assessment relied on this Control; reassess readiness after the decision."));
        }
        return ControlImpactRecords.Contribution(Context, records);
    }
}
