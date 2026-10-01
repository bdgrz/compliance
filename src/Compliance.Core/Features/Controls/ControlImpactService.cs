using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>
///     Builds the bounded, digest-stable impact preview for a pending Control successor or
///     retirement from the Control's own stream plus each registered owning-context contributor.
/// </summary>
public sealed class ControlImpactService(IAggregateReader reader,
    IEnumerable<IControlImpactContributor> contributors)
{
    const int MaximumRecordsPerContext = 200;

    /// <summary>
    ///     Contexts that may reference a Control. A context without a registered contributor has
    ///     no owning source in this build, so nothing in it can reference a Control yet; it is
    ///     reported as unlinked rather than silently omitted.
    /// </summary>
    static readonly string[] ExpectedContexts =
    [
        "applicability", "engagements", "evidence", "mappings", "readiness",
        "responsibilities", "risk_treatments", "work",
    ];

    public async ValueTask<Result<ControlImpactPreview>> PreviewAsync(
        PreviewControlImpact request, CancellationToken ct)
    {
        var control = await reader.HydrateAsync(new ControlDraft(request.TenantId,
            request.ControlId), ct).ConfigureAwait(false);
        if (!control.IsVisible || control.ProgramId != request.ProgramId)
            return Result<ControlImpactPreview>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The control was not found."));
        if (control.Revision != request.ExpectedRevision)
            return Result<ControlImpactPreview>.Failure(VersionedRecordRules
                .StaleRevision("control draft", control.Revision).ToRequestError());
        if (control.ApprovedVersion is not { } current || control.PendingTargetId is not { } target)
            return Result<ControlImpactPreview>.Failure(new RequestError(RequestErrorKind.Conflict,
                "Impact preview requires a pending successor or retirement of an approved control."));

        var kind = control.HasOpenDraft ? "successor" : "retirement";
        var changes = kind == "successor"
            ? Compare(current.Content, control.CurrentContent!)
            :
            [
                new ControlChange("status", "retired", null, current.Status,
                    "retired_from_" + control.PendingRetirementEffectiveUntil!.Value
                        .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            ];
        var contributions = new List<ControlImpactContribution>();
        foreach (var contributor in contributors.OrderBy(static item => item.Context,
                     StringComparer.Ordinal))
        {
            var result = await contributor.ContributeAsync(control, changes, ct)
                .ConfigureAwait(false);
            if (!result.IsSuccess)
                return Result<ControlImpactPreview>.Failure(result.Error);
            var contribution = result.Value;
            if (contribution.Context != contributor.Context || contribution.Records is null ||
                contribution.Records.Count > MaximumRecordsPerContext ||
                contribution.Records.Any(record => record.TenantId != request.TenantId ||
                    record.Context != contributor.Context || record.RecordId == Uuid.Empty))
                throw new InvalidOperationException(
                    "A control impact contributor returned an invalid or unbounded result.");
            contributions.Add(contribution with
            {
                Records = [.. contribution.Records
                    .OrderBy(static record => record.RecordType, StringComparer.Ordinal)
                    .ThenBy(static record => record.RecordId.ToString(), StringComparer.Ordinal)],
            });
        }
        if (contributions.Select(static item => item.Context).Distinct(StringComparer.Ordinal)
                .Count() != contributions.Count)
            throw new InvalidOperationException("Control impact contexts must be unique.");
        foreach (var context in ExpectedContexts.Where(name =>
                     contributions.All(item => item.Context != name)))
            contributions.Add(new ControlImpactContribution(context, "unlinked",
                "no_source_in_build", [], true));
        contributions.Sort(static (left, right) =>
            StringComparer.Ordinal.Compare(left.Context, right.Context));
        var pending = contributions.Where(static item => !item.Complete)
            .Select(static item => item.Context).ToArray();
        var preview = new ControlImpactPreview(request.TenantId, request.ProgramId,
            request.ControlId, kind, target, control.Revision, current.VersionId, changes,
            contributions, pending, pending.Length == 0, string.Empty);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(preview,
            ComplianceCoreJsonContext.Default.ControlImpactPreview);
        return Result<ControlImpactPreview>.Success(preview with
        {
            Digest = Convert.ToHexString(SHA256.HashData(bytes)),
        });
    }

    /// <summary>Recomputes the preview and requires it to be complete and unchanged.</summary>
    public async ValueTask<Result> ConfirmAsync(PreviewControlImpact request, string? digest,
        CancellationToken ct)
    {
        var preview = await PreviewAsync(request, ct).ConfigureAwait(false);
        if (!preview.IsSuccess)
            return Result.Failure(preview.Error);
        if (!preview.Value.Complete)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The impact preview is incomplete. Pending or incomplete contexts: " +
                string.Join(", ", preview.Value.PendingContexts)));
        return StringComparer.Ordinal.Equals(digest, preview.Value.Digest)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The impact preview changed. Reload it before deciding."));
    }

    static List<ControlChange> Compare(ControlDraftContent previous, ControlDraftContent proposed)
    {
        var changes = new List<ControlChange>();
        Field(changes, "title", previous.Title, proposed.Title);
        Field(changes, "objective", previous.Objective, proposed.Objective);
        Field(changes, "description", previous.Description, proposed.Description);
        Field(changes, "implementation_narrative", previous.ImplementationNarrative,
            proposed.ImplementationNarrative);
        Field(changes, "owner_reference", previous.OwnerReference, proposed.OwnerReference);
        Field(changes, "provenance", Describe(previous.Provenance), Describe(proposed.Provenance));
        Field(changes, "expected_evidence", string.Join('\n',
            previous.ExpectedEvidenceDescriptions), string.Join('\n',
            proposed.ExpectedEvidenceDescriptions));
        var before = (previous.Applicability ?? []).ToDictionary(static item => item.EntryId);
        var after = (proposed.Applicability ?? []).ToDictionary(static item => item.EntryId);
        foreach (var entryId in before.Keys.Concat(after.Keys).Distinct()
                     .OrderBy(static id => id.ToString(), StringComparer.Ordinal))
        {
            before.TryGetValue(entryId, out var old);
            after.TryGetValue(entryId, out var next);
            if (Equals(old, next))
                continue;
            changes.Add(new ControlChange("applicability",
                old is null ? "added" : next is null ? "removed" : "revised", entryId,
                Describe(old), Describe(next)));
        }
        return changes;
    }

    static void Field(List<ControlChange> changes, string field, string? previous,
        string? proposed)
    {
        if (!StringComparer.Ordinal.Equals(previous, proposed))
            changes.Add(new ControlChange(field, "revised", null, previous, proposed));
    }

    static string Describe(ControlContentProvenance? provenance) => provenance is null
        ? ControlDraft.OrganizationAuthored
        : string.Join('|', provenance.Origin, provenance.SourceName, provenance.SourceReference,
            provenance.SourceVersion);

    static string? Describe(ControlApplicabilityReference? reference) => reference is null
        ? null
        : reference.SubjectType + ":" + (reference.GovernedRecordId?.ToString() ??
            reference.Subject);
}
