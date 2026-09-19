using System.Globalization;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public interface IBoundaryDirectoryReader
{
    ValueTask<BoundaryView?> GetAsync(Uuid tenantId, Uuid boundaryId,
        CancellationToken ct = default);
    ValueTask<BoundaryVersionView?> GetVersionAsync(Uuid tenantId, Uuid boundaryId,
        Uuid versionId, CancellationToken ct = default);
    ValueTask<Page<BoundaryVersionView>?> ListVersionsAsync(Uuid tenantId, Uuid boundaryId,
        int limit, string? cursor, CancellationToken ct = default);
    ValueTask<BoundaryVersionView?> GetEffectiveVersionAsync(Uuid tenantId,
        Uuid boundaryId, DateOnly effectiveOn, CancellationToken ct = default);
    ValueTask<BoundaryDecisionView?> GetDecisionAsync(Uuid tenantId,
        Uuid boundaryId, Uuid decisionId, CancellationToken ct = default);
    ValueTask<Page<BoundaryDecisionView>?> ListDecisionsAsync(Uuid tenantId,
        Uuid boundaryId, int limit, string? cursor, CancellationToken ct = default);
}

public interface IBoundaryDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}

static class BoundaryDirectorySchema
{
    public static readonly KvDirectoryIndex<BoundaryVersionView> VersionsByBoundary = new(
        "by_boundary", 1, static version =>
            [version.BoundaryId.ToString(),
                version.EffectiveFrom!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)]);

    public static readonly KvDirectory<BoundaryVersionView, Uuid> Versions = new(
        "boundary_versions", ComplianceCoreJsonContext.Default.BoundaryVersionView,
        static version => version.VersionId,
        static versionId => [versionId.ToString()], [VersionsByBoundary]);

    public static readonly KvDirectoryIndex<BoundaryDecisionView> DecisionsByBoundary = new(
        "by_boundary", 1, static decision =>
            [decision.BoundaryId.ToString(),
                decision.DecidedAt.ToUniversalTime().Ticks.ToString("D20", CultureInfo.InvariantCulture),
                decision.DecisionId.ToString()]);

    public static readonly KvDirectory<BoundaryDecisionView, Uuid> Decisions = new(
        "boundary_decisions", ComplianceCoreJsonContext.Default.BoundaryDecisionView,
        static decision => decision.DecisionId,
        static decisionId => [decisionId.ToString()], [DecisionsByBoundary]);

    public static readonly KvDirectory<BoundaryView, Uuid> Directory = new(
        "boundaries", ComplianceCoreJsonContext.Default.BoundaryView,
        static boundary => boundary.BoundaryId,
        static boundaryId => [boundaryId.ToString()], []);
}

sealed class FitzBoundaryDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/boundary-directory/projection", "BoundaryDirectory"),
      IBoundaryDirectoryReader, IBoundaryDirectoryProjection
{
    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case BoundaryDraftCreated created:
                await BoundaryDirectorySchema.Directory.InsertAsync(Transaction,
                    new BoundaryView(created.TenantId, created.BoundaryId,
                        created.ProgramId,
                        new BoundaryVersionView(created.TenantId, created.BoundaryId,
                            created.ProgramId, created.DraftVersionId, 1,
                            created.Content, "draft", null, created.AuthorMemberId,
                            created.AuthorDisplay, created.ChangedAt), null, null), ct)
                    .ConfigureAwait(false);
                break;
            case BoundaryDraftRevised revised:
                var current = await BoundaryDirectorySchema.Directory.GetAsync(Transaction,
                    revised.BoundaryId, ct).ConfigureAwait(false);
                if (current?.Draft is null || current.Draft.VersionId != revised.DraftVersionId)
                    throw new InvalidOperationException(
                        "A boundary draft revision cannot project before its creation.");
                await BoundaryDirectorySchema.Directory.ReplaceAsync(Transaction, current,
                    current with
                    {
                        Draft = current.Draft with
                        {
                            Revision = revised.Revision,
                            Content = revised.Content,
                            AuthorMemberId = revised.AuthorMemberId,
                            AuthorDisplay = revised.AuthorDisplay,
                            ChangedAt = revised.ChangedAt,
                        },
                        LatestDecision = null,
                    }, ct).ConfigureAwait(false);
                break;
            case BoundaryReviewed reviewed:
                var reviewedCurrent = await BoundaryDirectorySchema.Directory.GetAsync(Transaction,
                    reviewed.BoundaryId, ct).ConfigureAwait(false);
                if (reviewedCurrent?.Draft is null ||
                    reviewedCurrent.Draft.VersionId != reviewed.DraftVersionId ||
                    reviewedCurrent.Draft.Revision != reviewed.Revision)
                    throw new InvalidOperationException(
                        "A boundary review cannot project before its exact draft revision.");
                var reviewDecision = new BoundaryDecisionView(reviewed.TenantId,
                    reviewed.BoundaryId, reviewed.DecisionId, reviewed.DraftVersionId,
                    reviewed.Revision, reviewed.Outcome, reviewed.ActorMemberId,
                    reviewed.ActorDisplay, reviewed.Rationale, reviewed.DecidedAt,
                    reviewedCurrent.LatestDecision is { } previousDecision &&
                    previousDecision.VersionId == reviewed.DraftVersionId &&
                    previousDecision.Revision == reviewed.Revision
                        ? previousDecision.DecisionId : null, null, null);
                await BoundaryDirectorySchema.Decisions.InsertAsync(Transaction,
                    reviewDecision, ct).ConfigureAwait(false);
                await BoundaryDirectorySchema.Directory.ReplaceAsync(Transaction, reviewedCurrent,
                    reviewedCurrent with
                    {
                        LatestDecision = reviewDecision,
                    }, ct).ConfigureAwait(false);
                break;
            case BoundaryApproved approved:
                var approvedCurrent = await BoundaryDirectorySchema.Directory.GetAsync(Transaction,
                    approved.BoundaryId, ct).ConfigureAwait(false);
                if (approvedCurrent?.Draft is null ||
                    approvedCurrent.Draft.VersionId != approved.DraftVersionId ||
                    approvedCurrent.Draft.Revision != approved.Revision ||
                    approvedCurrent.LatestDecision?.DecisionId != approved.AcceptedReviewDecisionId)
                    throw new InvalidOperationException(
                        "A boundary approval cannot project before its accepted review.");
                var approvedVersion = approvedCurrent.Draft with
                {
                    Status = "approved",
                    EffectiveFrom = approved.EffectiveFrom,
                };
                await BoundaryDirectorySchema.Versions.InsertAsync(Transaction,
                    approvedVersion, ct).ConfigureAwait(false);
                var approvalDecision = new BoundaryDecisionView(approved.TenantId,
                    approved.BoundaryId, approved.ApprovalDecisionId,
                    approved.DraftVersionId, approved.Revision, "approve",
                    approved.ActorMemberId, approved.ActorDisplay,
                    approved.Rationale, approved.DecidedAt, null,
                    approved.AcceptedReviewDecisionId, approved.ImpactDigest);
                await BoundaryDirectorySchema.Decisions.InsertAsync(Transaction,
                    approvalDecision, ct).ConfigureAwait(false);
                await BoundaryDirectorySchema.Directory.ReplaceAsync(Transaction, approvedCurrent,
                    approvedCurrent with
                    {
                        Draft = null,
                        LatestApprovedVersion = approvedVersion,
                        LatestDecision = approvalDecision,
                    }, ct).ConfigureAwait(false);
                break;
            case BoundarySuccessorProposed proposed:
                var predecessor = await BoundaryDirectorySchema.Directory.GetAsync(Transaction,
                    proposed.BoundaryId, ct).ConfigureAwait(false);
                if (predecessor?.LatestApprovedVersion?.VersionId != proposed.PredecessorVersionId ||
                    predecessor.Draft is not null)
                    throw new InvalidOperationException(
                        "A successor cannot project before its approved predecessor.");
                await BoundaryDirectorySchema.Directory.ReplaceAsync(Transaction, predecessor,
                    predecessor with
                    {
                        Draft = new BoundaryVersionView(proposed.TenantId, proposed.BoundaryId,
                            predecessor.ProgramId, proposed.DraftVersionId, 1,
                            proposed.Content, "draft", null, proposed.AuthorMemberId,
                            proposed.AuthorDisplay, proposed.ChangedAt),
                        LatestDecision = null,
                    }, ct).ConfigureAwait(false);
                break;
        }
    }

    public async ValueTask<BoundaryView?> GetAsync(Uuid tenantId, Uuid boundaryId,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await BoundaryDirectorySchema.Directory.GetAsync(tx, boundaryId, ct)
            .ConfigureAwait(false);
    }

    public async ValueTask<BoundaryVersionView?> GetVersionAsync(Uuid tenantId,
        Uuid boundaryId, Uuid versionId, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        var version = await BoundaryDirectorySchema.Versions.GetAsync(tx, versionId, ct)
            .ConfigureAwait(false);
        return version?.BoundaryId == boundaryId ? version : null;
    }

    public async ValueTask<Page<BoundaryVersionView>?> ListVersionsAsync(Uuid tenantId,
        Uuid boundaryId, int limit, string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        if (await BoundaryDirectorySchema.Directory.GetAsync(tx, boundaryId, ct)
                .ConfigureAwait(false) is null)
            return null;
        return await BoundaryDirectorySchema.Versions.QueryAsync(tx,
                BoundaryDirectorySchema.VersionsByBoundary.Query()
                    .WithPrefix(boundaryId.ToString()).Take(Math.Clamp(limit, 1, 200))
                    .After(cursor), ct)
            .ConfigureAwait(false);
    }

    public async ValueTask<BoundaryVersionView?> GetEffectiveVersionAsync(Uuid tenantId,
        Uuid boundaryId, DateOnly effectiveOn, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        string? cursor = null;
        BoundaryVersionView? effective = null;
        do
        {
            var page = await BoundaryDirectorySchema.Versions.QueryAsync(tx,
                BoundaryDirectorySchema.VersionsByBoundary.Query()
                    .WithPrefix(boundaryId.ToString()).Take(200).After(cursor), ct)
                .ConfigureAwait(false);
            foreach (var version in page.Items)
            {
                if (version.EffectiveFrom > effectiveOn)
                    return effective;
                effective = version;
            }
            cursor = page.NextCursor;
        } while (cursor is not null);
        return effective;
    }

    public async ValueTask<BoundaryDecisionView?> GetDecisionAsync(Uuid tenantId,
        Uuid boundaryId, Uuid decisionId, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        var decision = await BoundaryDirectorySchema.Decisions.GetAsync(tx, decisionId, ct)
            .ConfigureAwait(false);
        return decision?.BoundaryId == boundaryId ? decision : null;
    }

    public async ValueTask<Page<BoundaryDecisionView>?> ListDecisionsAsync(Uuid tenantId,
        Uuid boundaryId, int limit, string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        if (await BoundaryDirectorySchema.Directory.GetAsync(tx, boundaryId, ct)
                .ConfigureAwait(false) is null)
            return null;
        return await BoundaryDirectorySchema.Decisions.QueryAsync(tx,
                BoundaryDirectorySchema.DecisionsByBoundary.Query()
                    .WithPrefix(boundaryId.ToString()).Take(Math.Clamp(limit, 1, 200))
                    .After(cursor), ct)
            .ConfigureAwait(false);
    }
}
