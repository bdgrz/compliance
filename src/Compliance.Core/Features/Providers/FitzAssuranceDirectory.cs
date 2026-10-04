using System.Text.Json;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

sealed class FitzAssuranceDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/provider-assurance-v1/projection", ProjectorName), IAssuranceReader, IAssuranceProjection
{
    public const string ProjectorName = "ProviderAssuranceV2";
    Uuid? _batchTenantId;

    public new ValueTask<IProjectionBatch> BeginAsync(ProjectionBatchContext context,
        CancellationToken ct = default) => BeginAssuranceBatchAsync(context, ct);

    ValueTask<IProjectionBatch> IProjectionStore.BeginAsync(ProjectionBatchContext context,
        CancellationToken ct) => BeginAssuranceBatchAsync(context, ct);

    async ValueTask<IProjectionBatch> BeginAssuranceBatchAsync(ProjectionBatchContext context, CancellationToken ct)
    {
        if (context.Identity.Pattern.Area != ProviderAssuranceRegister.Area ||
            context.Identity.Pattern.Resource is not null ||
            !Uuid.TryParse(context.Identity.Pattern.Realm, null, out var tenantId) || tenantId == Uuid.Empty)
            throw new InvalidOperationException("An assurance projection batch requires its tenant's assurance area.");
        var batch = await base.BeginAsync(context, ct).ConfigureAwait(false);
        _batchTenantId = tenantId;
        return batch;
    }

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity(ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), ProviderAssuranceRegister.Area)), ct);

    public async ValueTask<AssuranceReportView?> GetReportRevisionAsync(Uuid tenantId,
        Uuid reportId, long revision, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        var view = await AssuranceDirectorySchema.ReportRevisions.GetAsync(tx,
            AssuranceDirectorySchema.ReportRevisionKey(reportId, revision), ct).ConfigureAwait(false);
        return view?.TenantId == tenantId && view.ReportId == reportId && view.Revision == revision
            ? view
            : null;
    }

    public async ValueTask<ProviderCoverageGapView?> GetCoverageGapAsync(Uuid tenantId,
        Uuid gapId, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        var gap = await AssuranceDirectorySchema.CoverageGaps.GetAsync(tx, gapId, ct)
            .ConfigureAwait(false);
        return gap?.TenantId == tenantId && gap.GapId == gapId ? gap : null;
    }

    public async ValueTask<Page<ProviderCoverageGapView>> ListCoverageGapsAsync(Uuid tenantId,
        Uuid providerId, int limit, string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await AssuranceDirectorySchema.CoverageGaps.QueryAsync(tx,
            AssuranceDirectorySchema.CoverageGapsByProvider.Query()
                .WithPrefix(providerId.ToString()).Take(limit).After(cursor), ct)
            .ConfigureAwait(false);
    }

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case AssuranceReportRecorded ev:
                await ApplyReportAsync(View(ev.TenantId, ev.ReportId, ev.ProviderId, 1, ev.Content, ev.ProviderRevision,
                    ev.Actor, ev.RecordedAt), ct).ConfigureAwait(false);
                break;
            case AssuranceReportRevised { Revision: >= 2 } ev:
                await ApplyReportAsync(View(ev.TenantId, ev.ReportId, ev.ProviderId, ev.Revision, ev.Content,
                    ev.ProviderRevision, ev.Actor, ev.RecordedAt), ct).ConfigureAwait(false);
                break;
            case ProviderReviewRecorded ev:
                await ApplyReviewAsync(new ProviderReviewView(ev.TenantId, ev.ReviewId, ev.ProviderId,
                    AssuranceRules.Normalize(ev.Content), ev.ProviderRevision, ev.AssuranceReportRevision, ev.Actor,
                    ev.RecordedAt), ct).ConfigureAwait(false);
                break;
            case ProviderCoverageGapRecorded ev:
                if (ProviderCoverageGapRules.InputError(ev.Content) is not null ||
                    ev.RequestId == Uuid.Empty)
                    throw new InvalidOperationException("A provider coverage gap event must have valid facts and a request identity.");
                await ApplyCoverageGapAsync(new ProviderCoverageGapView(ev.TenantId, ev.GapId,
                    ev.ProviderId, ev.ProviderRevision, 1,
                    ProviderCoverageGapRules.Normalize(ev.Content), "open", ev.Actor,
                    ev.RecordedAt, null, []), ct).ConfigureAwait(false);
                break;
            case ProviderCoverageGapClosed ev:
                await ApplyCoverageGapClosureAsync(ev, ct).ConfigureAwait(false);
                break;
            case ProviderCoverageGapRiskAcceptanceLinked ev:
                await ApplyCoverageGapRiskAcceptanceAsync(ev, ct).ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException("The assurance event is not supported.");
        }
    }

    public async ValueTask<Page<AssuranceReportView>> ListReportsAsync(Uuid tenantId, Uuid providerId, int limit,
        string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await AssuranceDirectorySchema.Reports.QueryAsync(tx,
            AssuranceDirectorySchema.ReportsByProvider.Query().WithPrefix(providerId.ToString()).Take(limit).After(cursor), ct)
            .ConfigureAwait(false);
    }

    public async ValueTask<Page<ProviderReviewView>> ListReviewsAsync(Uuid tenantId, Uuid providerId, int limit,
        string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await AssuranceDirectorySchema.Reviews.QueryAsync(tx,
            AssuranceDirectorySchema.ReviewsByProvider.Query().WithPrefix(providerId.ToString()).Take(limit).After(cursor), ct)
            .ConfigureAwait(false);
    }

    async ValueTask ApplyReportAsync(AssuranceReportView view, CancellationToken ct)
    {
        RequireBatchTenant(view.TenantId, view.ProviderId, view.ReportId);
        var revisionKey = AssuranceDirectorySchema.ReportRevisionKey(view.ReportId,
            view.Revision);
        var retainedRevision = await AssuranceDirectorySchema.ReportRevisions.GetAsync(
            Transaction, revisionKey, ct).ConfigureAwait(false);
        if (retainedRevision is not null && !Same(retainedRevision, view))
            throw new InvalidOperationException("An assurance report revision cannot replace retained content.");
        var current = await AssuranceDirectorySchema.Reports.GetAsync(Transaction, view.ReportId, ct).ConfigureAwait(false);
        if (current is not null && current.Revision >= view.Revision)
        {
            // A replayed revision is idempotent only when it matches the retained current or a superseded fact.
            if (current.Revision == view.Revision && !Same(current, view))
                throw new InvalidOperationException("An assurance report revision cannot replace retained content.");
            if (retainedRevision is null)
                await AssuranceDirectorySchema.ReportRevisions.InsertAsync(Transaction, view, ct)
                    .ConfigureAwait(false);
            return;
        }
        if (current is null ? view.Revision != 1 : current.TenantId != view.TenantId ||
                current.ProviderId != view.ProviderId || current.Revision + 1 != view.Revision)
            throw new InvalidOperationException("An assurance report revision requires its tenant's immediate predecessor.");
        if (current is null)
            await AssuranceDirectorySchema.Reports.InsertAsync(Transaction, view, ct).ConfigureAwait(false);
        else
            await AssuranceDirectorySchema.Reports.ReplaceAsync(Transaction, current, view, ct).ConfigureAwait(false);
        if (retainedRevision is null)
            await AssuranceDirectorySchema.ReportRevisions.InsertAsync(Transaction, view, ct)
                .ConfigureAwait(false);
    }

    async ValueTask ApplyReviewAsync(ProviderReviewView view, CancellationToken ct)
    {
        RequireBatchTenant(view.TenantId, view.ProviderId, view.ReviewId);
        var retained = await AssuranceDirectorySchema.Reviews.GetAsync(Transaction, view.ReviewId, ct).ConfigureAwait(false);
        if (retained is not null)
        {
            if (!Same(retained, view))
                throw new InvalidOperationException("A provider review cannot replace retained content.");
            return;
        }
        await AssuranceDirectorySchema.Reviews.InsertAsync(Transaction, view, ct).ConfigureAwait(false);
    }

    async ValueTask ApplyCoverageGapAsync(ProviderCoverageGapView view, CancellationToken ct)
    {
        RequireBatchTenant(view.TenantId, view.ProviderId, view.GapId);
        if (view.ProviderRevision < 1 || view.Revision != 1 || view.Status != "open" ||
            view.Closure is not null)
            throw new InvalidOperationException("A new provider coverage gap must start open at revision one.");
        var retained = await AssuranceDirectorySchema.CoverageGaps.GetAsync(Transaction,
            view.GapId, ct).ConfigureAwait(false);
        if (retained is not null)
        {
            if (!SameCoverageGapRecord(retained, view))
                throw new InvalidOperationException("A provider coverage gap cannot replace retained content.");
            return;
        }
        await AssuranceDirectorySchema.CoverageGaps.InsertAsync(Transaction, view, ct)
            .ConfigureAwait(false);
    }

    async ValueTask ApplyCoverageGapClosureAsync(ProviderCoverageGapClosed ev,
        CancellationToken ct)
    {
        RequireBatchTenant(ev.TenantId, ev.ProviderId, ev.GapId);
        if (ProviderCoverageGapRules.ClosureInputError(ev.Content) is not null)
            throw new InvalidOperationException("A provider coverage gap closure requires valid resolution evidence.");
        var current = await AssuranceDirectorySchema.CoverageGaps.GetAsync(Transaction,
            ev.GapId, ct).ConfigureAwait(false);
        if (current is null || current.TenantId != ev.TenantId ||
            current.ProviderId != ev.ProviderId)
            throw new InvalidOperationException("A provider coverage gap closure requires its retained gap.");
        var closed = current with
        {
            Revision = ev.Revision,
            Status = "closed",
            Closure = new ProviderCoverageGapClosureView(
                ProviderCoverageGapRules.Normalize(ev.Content), ev.Actor, ev.ClosedAt)
            {
                Revision = ev.Revision,
            },
        };
        if (current.Revision >= ev.Revision)
        {
            if (current.Revision == ev.Revision && !Same(current, closed))
                throw new InvalidOperationException("A provider coverage gap closure cannot replace retained content.");
            return;
        }
        if (current.Status != "open" || current.Revision + 1 != ev.Revision)
            throw new InvalidOperationException("A provider coverage gap closure must immediately follow an open revision.");
        await AssuranceDirectorySchema.CoverageGaps.ReplaceAsync(Transaction, current, closed, ct)
            .ConfigureAwait(false);
    }

    async ValueTask ApplyCoverageGapRiskAcceptanceAsync(
        ProviderCoverageGapRiskAcceptanceLinked ev, CancellationToken ct)
    {
        RequireBatchTenant(ev.TenantId, ev.ProviderId, ev.GapId);
        if (ev.Acceptance is null)
            throw new InvalidOperationException("A provider coverage gap acceptance link requires an acceptance.");
        var current = await AssuranceDirectorySchema.CoverageGaps.GetAsync(Transaction,
            ev.GapId, ct).ConfigureAwait(false);
        if (current is null || current.TenantId != ev.TenantId || current.ProviderId != ev.ProviderId)
            throw new InvalidOperationException("A provider coverage gap acceptance link requires its gap.");
        if (current.Revision >= ev.Revision)
        {
            if ((current.RiskAcceptances ?? []).Any(link => link == ev.Acceptance))
                return;
            throw new InvalidOperationException("A provider coverage gap acceptance link cannot replace retained content.");
        }
        if (current.Status != "open" || current.Revision + 1 != ev.Revision ||
            ev.Acceptance.ProgramId == Uuid.Empty ||
            ev.Acceptance.RiskId == Uuid.Empty || ev.Acceptance.AcceptanceId == Uuid.Empty ||
            ev.Acceptance.Revision != ev.Revision ||
            ev.Acceptance.ExpiresAt <= ev.Acceptance.LinkedAt ||
            (current.RiskAcceptances ?? []).Count >= ProviderAssuranceRegister.MaximumRiskAcceptanceLinksPerGap ||
            (current.RiskAcceptances ?? []).Any(link => link.RiskId == ev.Acceptance.RiskId &&
                                                        link.AcceptanceId == ev.Acceptance.AcceptanceId))
            throw new InvalidOperationException("A provider coverage gap acceptance link must be a new, time-bounded decision at the next revision.");
        var linked = current with
        {
            Revision = ev.Revision,
            RiskAcceptances = [.. (current.RiskAcceptances ?? []), ev.Acceptance],
        };
        await AssuranceDirectorySchema.CoverageGaps.ReplaceAsync(Transaction, current, linked, ct)
            .ConfigureAwait(false);
    }

    void RequireBatchTenant(Uuid tenantId, Uuid providerId, Uuid recordId)
    {
        if (tenantId == Uuid.Empty || providerId == Uuid.Empty || recordId == Uuid.Empty || tenantId != _batchTenantId)
            throw new InvalidOperationException("An assurance event must belong to its tenant projection batch.");
    }

    static AssuranceReportView View(Uuid tenantId, Uuid reportId, Uuid providerId, long revision,
        AssuranceReportContent content, long providerRevision, Bdgrz.Compliance.Features.AccessControl.ActorReference actor,
        DateTimeOffset recordedAt)
    {
        content = AssuranceRules.Normalize(content);
        return new AssuranceReportView(tenantId, reportId, providerId, revision, content, providerRevision,
            content.Exceptions!.Count, content.ComplementaryControls!.Count, content.CoverageGaps!.Count, actor, recordedAt);
    }

    static bool Same(AssuranceReportView left, AssuranceReportView right) =>
        JsonSerializer.SerializeToUtf8Bytes(left, ComplianceCoreJsonContext.Default.AssuranceReportView).AsSpan()
            .SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(right, ComplianceCoreJsonContext.Default.AssuranceReportView));

    static bool Same(ProviderReviewView left, ProviderReviewView right) =>
        JsonSerializer.SerializeToUtf8Bytes(left, ComplianceCoreJsonContext.Default.ProviderReviewView).AsSpan()
            .SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(right, ComplianceCoreJsonContext.Default.ProviderReviewView));

    static bool Same(ProviderCoverageGapView left, ProviderCoverageGapView right) =>
        JsonSerializer.SerializeToUtf8Bytes(left,
            ComplianceCoreJsonContext.Default.ProviderCoverageGapView).AsSpan()
            .SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(right,
                ComplianceCoreJsonContext.Default.ProviderCoverageGapView));

    static bool SameCoverageGapRecord(ProviderCoverageGapView retained,
        ProviderCoverageGapView recorded) => retained.TenantId == recorded.TenantId &&
        retained.GapId == recorded.GapId && retained.ProviderId == recorded.ProviderId &&
        retained.ProviderRevision == recorded.ProviderRevision && retained.Content == recorded.Content &&
        retained.RecordedBy == recorded.RecordedBy && retained.RecordedAt == recorded.RecordedAt;
}
