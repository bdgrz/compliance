using System.Security.Cryptography;
using System.Text.Json;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public interface IBoundaryImpactContributor :
    IImpactContributor<BoundaryView, BoundaryChange, BoundaryImpactContribution>;

public sealed class ProgramBoundaryImpactContributor : IBoundaryImpactContributor
{
    public string Context => "programs";

    public ValueTask<Result<BoundaryImpactContribution>> ContributeAsync(BoundaryView boundary,
        IReadOnlyList<BoundaryChange> changes, CancellationToken ct)
    {
        IReadOnlyList<BoundaryAffectedRecord> records =
            boundary.LatestApprovedVersion is not null && changes.Count > 0
                ? [new BoundaryAffectedRecord(boundary.TenantId, Context,
                    "program", boundary.ProgramId,
                    "The approved scope used by this program would change.")]
                : [];
        return ValueTask.FromResult(Result<BoundaryImpactContribution>.Success(
            new BoundaryImpactContribution(Context, records, true)));
    }
}

public sealed class BoundaryImpactService(
    IBoundaryDirectoryReader directory,
    IEnumerable<IBoundaryImpactContributor> contributors)
{
    static readonly string[] ExpectedContexts =
        ["programs", "controls", "evidence", "risks", "providers", "engagements", "readiness"];

    public async ValueTask<Result<BoundaryImpactPreview>> PreviewAsync(
        PreviewBoundaryImpact request, CancellationToken ct)
    {
        var boundary = await directory.GetAsync(request.TenantId, request.BoundaryId, ct)
            .ConfigureAwait(false);
        if (boundary is null)
            return Result<BoundaryImpactPreview>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The boundary was not found."));
        if (boundary.Draft is null || boundary.Draft.VersionId != request.DraftVersionId ||
            boundary.Draft.Revision != request.ExpectedRevision)
            return Result<BoundaryImpactPreview>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The boundary draft changed or its projection has not reached the requested revision."));

        var changes = Compare(boundary.LatestApprovedVersion?.Content, boundary.Draft.Content);
        var contributions = new List<BoundaryImpactContribution>();
        foreach (var contributor in contributors.OrderBy(static item => item.Context,
                     StringComparer.Ordinal))
        {
            var contributionResult = await contributor.ContributeAsync(boundary, changes, ct)
                .ConfigureAwait(false);
            if (!contributionResult.IsSuccess)
                return Result<BoundaryImpactPreview>.Failure(contributionResult.Error);
            var contribution = contributionResult.Value;
            if (contribution.Context != contributor.Context || contribution.Records is null ||
                contribution.Records.Count > 200 ||
                contribution.Records.Any(record => record.TenantId != request.TenantId ||
                    record.Context != contributor.Context || record.RecordId == Uuid.Empty))
                throw new InvalidOperationException(
                    "A boundary impact contributor returned an invalid or unbounded result.");
            contributions.Add(contribution with
            {
                Records = [.. contribution.Records
                    .OrderBy(static record => record.RecordType, StringComparer.Ordinal)
                    .ThenBy(static record => record.RecordId.ToString(), StringComparer.Ordinal)],
            });
        }
        if (contributions.Select(static item => item.Context).Distinct(StringComparer.Ordinal).Count() !=
            contributions.Count)
            throw new InvalidOperationException("Boundary impact contexts must be unique.");
        var contributed = contributions.Select(static item => item.Context)
            .ToHashSet(StringComparer.Ordinal);
        var pending = boundary.LatestApprovedVersion is null
            ? Array.Empty<string>()
            : ExpectedContexts.Where(contextName => !contributed.Contains(contextName) ||
                contributions.Any(item => item.Context == contextName && !item.Complete)).ToArray();
        var complete = pending.Length == 0 && contributions.All(static item => item.Complete);
        var preview = new BoundaryImpactPreview(request.TenantId,
            request.BoundaryId, request.DraftVersionId, request.ExpectedRevision,
            boundary.LatestApprovedVersion?.VersionId, changes, contributions,
            pending, complete, string.Empty);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(preview,
            ComplianceCoreJsonContext.Default.BoundaryImpactPreview);
        return Result<BoundaryImpactPreview>.Success(preview with
        {
            Digest = Convert.ToHexString(SHA256.HashData(bytes)),
        });
    }

    static List<BoundaryChange> Compare(BoundaryContent? previous,
        BoundaryContent proposed)
    {
        var changes = new List<BoundaryChange>();
        if (previous?.Statement != proposed.Statement)
            changes.Add(new BoundaryChange("statement", "revised", null,
                previous?.Statement, proposed.Statement, null, null));
        if (previous?.EngagementStage != proposed.EngagementStage)
            changes.Add(new BoundaryChange("engagement_stage", "revised", null,
                previous?.EngagementStage, proposed.EngagementStage, null, null));
        var oldCategories = previous is null ? null : string.Join(",",
            previous.TrustServicesCategories.OrderBy(static value => value, StringComparer.Ordinal));
        var newCategories = string.Join(",",
            proposed.TrustServicesCategories.OrderBy(static value => value, StringComparer.Ordinal));
        if (oldCategories != newCategories)
            changes.Add(new BoundaryChange("trust_services_categories", "revised", null,
                oldCategories, newCategories, null, null));

        var oldEntries = previous?.Entries.ToDictionary(static entry => entry.EntryId) ?? [];
        var newEntries = proposed.Entries.ToDictionary(static entry => entry.EntryId);
        foreach (var entryId in oldEntries.Keys.Concat(newEntries.Keys).Distinct()
                     .OrderBy(static id => id.ToString(), StringComparer.Ordinal))
        {
            oldEntries.TryGetValue(entryId, out var before);
            newEntries.TryGetValue(entryId, out var after);
            if (Equals(before, after))
                continue;
            changes.Add(new BoundaryChange("scope_entry",
                before is null ? "added" : after is null ? "removed" : "revised",
                entryId, null, null, before, after));
        }
        return changes;
    }
}

public sealed class PreviewBoundaryImpactHandler(BoundaryImpactService service)
    : IRequestHandler<PreviewBoundaryImpact, BoundaryImpactPreview>
{
    public ValueTask<Result<BoundaryImpactPreview>> HandleAsync(
        IRequestContext<PreviewBoundaryImpact> context, CancellationToken ct) =>
        service.PreviewAsync(context.Request, ct);
}
