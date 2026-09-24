using System.Globalization;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

public interface IRiskDraftHistoryDirectoryReader
{
    ValueTask<RiskDraftRevisionView?> GetRevisionAsync(Uuid tenantId, Uuid riskId,
        long revision, CancellationToken ct = default);
    ValueTask<Page<RiskDraftRevisionView>> ListRevisionsAsync(Uuid tenantId, Uuid riskId,
        int limit, string? cursor, CancellationToken ct = default);
}

public interface IRiskDraftHistoryDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}

static class RiskDraftHistoryDirectoryV1Schema
{
    public static readonly KvDirectoryIndex<RiskDraftRevisionView> ByRiskRevision = new(
        "by_risk_revision", 1, static revision =>
            [revision.RiskId.ToString(),
                revision.Revision.ToString("D20", CultureInfo.InvariantCulture)]);

    public static readonly KvDirectory<RiskDraftRevisionView, string> Revisions = new(
        "risk_draft_history_revisions_v1",
        ComplianceCoreJsonContext.Default.RiskDraftRevisionView,
        static revision => RevisionKey(revision.RiskId, revision.Revision),
        static key => [key], [ByRiskRevision]);

    public static string RevisionKey(Uuid riskId, long revision) =>
        $"{riskId}:{revision.ToString("D20", CultureInfo.InvariantCulture)}";
}

sealed class FitzRiskDraftHistoryDirectoryV1(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/risk-draft-history-v1/projection",
            "RiskDraftHistoryDirectoryV1"),
      IRiskDraftHistoryDirectoryReader, IRiskDraftHistoryDirectoryProjection
{
    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case RiskDraftCreated created:
                await InsertAsync(new RiskDraftRevisionView(created.TenantId, created.ProgramId,
                    created.RiskId, created.Identifier, 1, created.Content,
                    created.ActorMemberId, created.ActorDisplay, created.ChangedAt)
                {
                    Actor = created.Actor,
                }, ct)
                    .ConfigureAwait(false);
                break;
            case RiskDraftRevised revised:
                if (revised.Revision < 2)
                    throw new InvalidOperationException(
                        "A risk draft history revision must follow its creation.");
                var previous = await RiskDraftHistoryDirectoryV1Schema.Revisions.GetAsync(
                    Transaction, RiskDraftHistoryDirectoryV1Schema.RevisionKey(revised.RiskId,
                        revised.Revision - 1), ct).ConfigureAwait(false);
                if (previous is null || previous.TenantId != revised.TenantId ||
                    previous.ProgramId != revised.ProgramId || previous.RiskId != revised.RiskId ||
                    previous.Revision != revised.Revision - 1)
                    throw new InvalidOperationException(
                        "A risk draft history revision cannot project before its predecessor.");
                await InsertAsync(new RiskDraftRevisionView(revised.TenantId, revised.ProgramId,
                    revised.RiskId, previous.Identifier, revised.Revision, revised.Content,
                    revised.ActorMemberId, revised.ActorDisplay, revised.ChangedAt)
                {
                    Actor = revised.Actor,
                }, ct)
                    .ConfigureAwait(false);
                break;
        }
    }

    async ValueTask InsertAsync(RiskDraftRevisionView revision, CancellationToken ct) =>
        await RiskDraftHistoryDirectoryV1Schema.Revisions.InsertAsync(Transaction, revision, ct)
            .ConfigureAwait(false);

    public async ValueTask<RiskDraftRevisionView?> GetRevisionAsync(Uuid tenantId,
        Uuid riskId, long revision, CancellationToken ct = default)
    {
        await using var transaction = await BeginReadAsync(tenantId.ToString(), ct)
            .ConfigureAwait(false);
        return await RiskDraftHistoryDirectoryV1Schema.Revisions.GetAsync(transaction,
            RiskDraftHistoryDirectoryV1Schema.RevisionKey(riskId, revision), ct)
            .ConfigureAwait(false);
    }

    public async ValueTask<Page<RiskDraftRevisionView>> ListRevisionsAsync(Uuid tenantId,
        Uuid riskId, int limit, string? cursor, CancellationToken ct = default)
    {
        await using var transaction = await BeginReadAsync(tenantId.ToString(), ct)
            .ConfigureAwait(false);
        return await RiskDraftHistoryDirectoryV1Schema.Revisions.QueryAsync(transaction,
            RiskDraftHistoryDirectoryV1Schema.ByRiskRevision.Query()
                .WithPrefix(riskId.ToString()).Take(Math.Clamp(limit, 1, 200))
                .After(cursor), ct).ConfigureAwait(false);
    }
}
