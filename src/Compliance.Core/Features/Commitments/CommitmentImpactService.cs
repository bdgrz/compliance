using System.Security.Cryptography;
using System.Text.Json;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Controls;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

/// <summary>Previews what an effective commitment change would affect.</summary>
public sealed class CommitmentImpactService(CommitmentDraftReadConsistency drafts,
    CommitmentVersionReadConsistency versions, IBoundaryDirectoryReader boundaries,
    IControlDraftDirectoryReader controls, IAggregateReader reader)
{
    const int BoundaryPageSize = 200;
    const int MaximumBoundaryPages = 10;

    /// <summary>Contexts that cannot reference commitments, with the reason each contributes nothing.</summary>
    public static readonly IReadOnlyList<CommitmentUnlinkedContext> UnlinkedContexts =
    [
        new("control_criterion_mappings",
            "Mappings link criteria to controls, not commitments; affected controls are listed instead."),
        new("evidence", "Evidence records do not reference commitments yet."),
        new("readiness",
            "Readiness rules do not assess commitments yet; commitments are reported as an unassessed family."),
        new("risks", "Risk drafts do not reference commitments yet."),
    ];

    public async ValueTask<Result<CommitmentImpactPreview>> PreviewAsync(
        PreviewCommitmentImpact request, CancellationToken ct)
    {
        var draft = await drafts.GetAsync(request.TenantId, request.ProgramId, request.DraftId,
            request.ExpectedRevision, ct).ConfigureAwait(false);
        if (!draft.IsSuccess)
            return Result<CommitmentImpactPreview>.Failure(draft.Error);
        if (draft.Value.Revision != request.ExpectedRevision)
            return Result<CommitmentImpactPreview>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The draft changed. Reload it before previewing impact."));
        var source = await versions.SourceAsync(request.TenantId, request.ProgramId,
            request.DraftId, ct).ConfigureAwait(false);
        if (!source.IsSuccess)
            return Result<CommitmentImpactPreview>.Failure(source.Error);
        CommitmentVersionView? effective = null;
        var effectiveVersion = source.Value.EffectiveVersionCount;
        if (effectiveVersion > 0)
        {
            var latest = await versions.GetAsync(request.TenantId, request.ProgramId,
                request.DraftId, effectiveVersion, ct).ConfigureAwait(false);
            if (!latest.IsSuccess)
                return Result<CommitmentImpactPreview>.Failure(latest.Error);
            effective = latest.Value;
        }

        var changes = new List<CommitmentChange>();
        if (effective?.Statement != draft.Value.Statement)
            changes.Add(new CommitmentChange("statement", effective?.Statement, draft.Value.Statement));
        if (effective?.Context != draft.Value.Context)
            changes.Add(new CommitmentChange("context", effective?.Context, draft.Value.Context));
        if (effective?.SourceReference != draft.Value.SourceReference)
            changes.Add(new CommitmentChange("source_reference", effective?.SourceReference,
                draft.Value.SourceReference));

        var dependents = new List<CommitmentDependent>();
        string? cursor = null;
        var complete = false;
        for (var page = 0; page < MaximumBoundaryPages; page++)
        {
            var result = await boundaries.ListProgramAsync(request.TenantId, request.ProgramId,
                BoundaryPageSize, cursor, ct).ConfigureAwait(false);
            foreach (var boundary in result.Items.Where(item => item.TenantId == request.TenantId &&
                         item.ProgramId == request.ProgramId))
            {
                if (References(boundary.LatestApprovedVersion, request.DraftId))
                    dependents.Add(new CommitmentDependent("boundaries", "boundary",
                        boundary.BoundaryId, "approved_scope_entry"));
                if (References(boundary.Draft, request.DraftId))
                    dependents.Add(new CommitmentDependent("boundaries", "boundary",
                        boundary.BoundaryId, "draft_scope_entry"));
            }
            cursor = result.NextCursor;
            if (cursor is null)
            {
                complete = true;
                break;
            }
        }

        var controlsComplete = await AddControlDependentsAsync(request, dependents, ct)
            .ConfigureAwait(false);
        complete &= controlsComplete;

        var preview = new CommitmentImpactPreview(request.TenantId, request.ProgramId,
            request.DraftId, request.ExpectedRevision, effective?.Version, changes,
            [.. dependents.OrderBy(static item => item.RecordId.ToString(), StringComparer.Ordinal)
                .ThenBy(static item => item.Relationship, StringComparer.Ordinal)],
            UnlinkedContexts, complete, string.Empty);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(preview,
            ComplianceCoreJsonContext.Default.CommitmentImpactPreview);
        return Result<CommitmentImpactPreview>.Success(preview with
        {
            Digest = Convert.ToHexString(SHA256.HashData(bytes)),
        });
    }

    async ValueTask<bool> AddControlDependentsAsync(PreviewCommitmentImpact request,
        List<CommitmentDependent> dependents, CancellationToken ct)
    {
        string? cursor = null;
        for (var page = 0; page < MaximumBoundaryPages; page++)
        {
            var result = await controls.ListProgramAsync(request.TenantId, request.ProgramId,
                BoundaryPageSize, cursor, ct).ConfigureAwait(false);
            foreach (var item in result.Items.Where(item => item.TenantId == request.TenantId &&
                         item.ProgramId == request.ProgramId))
            {
                // The control stream is authoritative for both approved and pending applicability.
                var control = await reader.HydrateAsync(new ControlDraft(request.TenantId,
                    item.ControlId), ct).ConfigureAwait(false);
                if (control.ProgramId != request.ProgramId)
                    continue;
                if (References(control.ApprovedVersion?.Content, request.DraftId))
                    dependents.Add(new CommitmentDependent("controls", "control", item.ControlId,
                        "approved_applicability"));
                if (control.HasOpenDraft && References(control.CurrentContent, request.DraftId))
                    dependents.Add(new CommitmentDependent("controls", "control", item.ControlId,
                        "draft_applicability"));
            }
            cursor = result.NextCursor;
            if (cursor is null)
                return true;
        }
        return false;
    }

    static bool References(ControlDraftContent? content, Uuid draftId) =>
        content?.Applicability?.Any(reference => reference is { Unresolved: false } &&
            reference.GovernedRecordId == draftId &&
            StringComparer.Ordinal.Equals(reference.SubjectType, "commitment")) == true;

    static bool References(BoundaryVersionView? version, Uuid draftId) =>
        version?.Content.Entries.Any(entry => entry.GovernedRecordId == draftId &&
            StringComparer.Ordinal.Equals(entry.SubjectType, "commitment")) == true;
}
