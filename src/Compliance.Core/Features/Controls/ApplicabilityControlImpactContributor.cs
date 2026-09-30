using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>
///     Reports the governed applications and system instances named by the current approved
///     version and the pending successor. Applicability is owned by the Control record itself, so
///     the aggregate stream is the authoritative, lag-free source.
/// </summary>
public sealed class ApplicabilityControlImpactContributor : IControlImpactContributor
{
    public string Context => "applicability";

    public ValueTask<Result<ControlImpactContribution>> ContributeAsync(ControlDraft subject,
        IReadOnlyList<ControlChange> changes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(subject);
        var current = Governed(subject.ApprovedVersion?.Content);
        var proposed = subject.HasOpenDraft ? Governed(subject.CurrentContent) : null;
        var records = new Dictionary<Uuid, ControlAffectedRecord>();
        foreach (var reference in current)
        {
            var reason = proposed is null
                ? "Retirement ends this Control's future applicability to the record."
                : proposed.Any(item => item.GovernedRecordId == reference.GovernedRecordId)
                    ? "The successor keeps applicability to the record."
                    : "The successor removes applicability to the record.";
            records.TryAdd(reference.GovernedRecordId!.Value, Record(subject, reference, reason,
                subject.ApprovedVersion?.VersionId));
        }
        foreach (var reference in proposed ?? [])
            records.TryAdd(reference.GovernedRecordId!.Value, Record(subject, reference,
                "The successor adds applicability to the record.", subject.DraftVersionId));
        return ValueTask.FromResult(Result<ControlImpactContribution>.Success(
            new ControlImpactContribution(Context, "complete", "authoritative_source",
                [.. records.Values], true)));
    }

    ControlAffectedRecord Record(ControlDraft subject, ControlApplicabilityReference reference,
        string reason, Uuid? versionId) => new(subject.TenantId, Context,
        reference.SubjectType, reference.GovernedRecordId!.Value, versionId, reason);

    static ControlApplicabilityReference[] Governed(ControlDraftContent? content) =>
        [.. (content?.Applicability ?? []).Where(static reference =>
            reference is { Unresolved: false, GovernedRecordId: { } id } && id != Uuid.Empty)];
}
