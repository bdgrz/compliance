using System.Globalization;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public interface IBoundaryDirectoryReader
{
    ValueTask<BoundaryView?> GetAsync(Uuid tenantId, Uuid boundaryId,
        CancellationToken ct = default);
    ValueTask<Page<BoundaryView>> ListProgramAsync(Uuid tenantId, Uuid programId,
        int limit, string? cursor, CancellationToken ct = default);
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

    public static readonly KvDirectoryIndex<BoundaryView> ByProgram = new(
        "by_program", 1, static boundary =>
            [boundary.ProgramId.ToString(), boundary.BoundaryId.ToString()]);

    public static readonly KvDirectory<BoundaryView, Uuid> Directory = new(
        "boundaries", ComplianceCoreJsonContext.Default.BoundaryView,
        static boundary => boundary.BoundaryId,
        static boundaryId => [boundaryId.ToString()], [ByProgram]);
}

sealed class FitzBoundaryDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/boundary-directory-v2/projection", "BoundaryDirectoryV2"),
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
                            created.AuthorDisplay, created.ChangedAt), null, null, 1), ct)
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
                        Revision = current.Revision + 1,
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
            case BoundaryDraftDiscarded discarded:
                var discardedCurrent = await BoundaryDirectorySchema.Directory.GetAsync(Transaction,
                    discarded.BoundaryId, ct).ConfigureAwait(false);
                if (discardedCurrent?.Draft is null ||
                    discardedCurrent.Draft.VersionId != discarded.DraftVersionId ||
                    discardedCurrent.Draft.Revision != discarded.Revision)
                    throw new InvalidOperationException(
                        "A draft discard cannot project before its exact draft revision.");
                if (discardedCurrent.LatestApprovedVersion is null)
                    await BoundaryDirectorySchema.Directory.DeleteAsync(Transaction,
                        discardedCurrent, ct).ConfigureAwait(false);
                else
                    await BoundaryDirectorySchema.Directory.ReplaceAsync(Transaction,
                        discardedCurrent, discardedCurrent with
                        {
                            Draft = null,
                            Revision = discardedCurrent.Revision + 1,
                        }, ct)
                        .ConfigureAwait(false);
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
                        Revision = reviewedCurrent.Revision + 1,
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
                if (approvedCurrent.LatestApprovedVersion?.EffectiveFrom is { } priorFrom &&
                    !EffectiveInterval.CanFollow(priorFrom, approved.EffectiveFrom))
                    throw new InvalidOperationException(
                        "A successor boundary version must become effective after its predecessor.");
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
                        Revision = approvedCurrent.Revision + 1,
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
                        Revision = predecessor.Revision + 1,
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

    public async ValueTask<Page<BoundaryView>> ListProgramAsync(Uuid tenantId,
        Uuid programId, int limit, string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await BoundaryDirectorySchema.Directory.QueryAsync(tx,
            BoundaryDirectorySchema.ByProgram.Query().WithPrefix(programId.ToString())
                .Take(Math.Clamp(limit, 1, 200)).After(cursor), ct).ConfigureAwait(false);
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
        BoundaryVersionView? previous = null;
        do
        {
            var page = await BoundaryDirectorySchema.Versions.QueryAsync(tx,
                BoundaryDirectorySchema.VersionsByBoundary.Query()
                    .WithPrefix(boundaryId.ToString()).Take(200).After(cursor), ct)
                .ConfigureAwait(false);
            foreach (var version in page.Items)
            {
                var from = version.EffectiveFrom ?? throw new InvalidOperationException(
                    "An approved boundary version requires an effective date.");
                if (previous is null && from > effectiveOn)
                    return null;
                if (previous is { EffectiveFrom: { } previousFrom } &&
                    new EffectiveInterval(previousFrom, from).Contains(effectiveOn))
                    return previous;
                previous = version;
            }
            cursor = page.NextCursor;
        } while (cursor is not null);
        return previous is { EffectiveFrom: { } lastFrom } &&
               new EffectiveInterval(lastFrom, null).Contains(effectiveOn)
            ? previous
            : null;
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
