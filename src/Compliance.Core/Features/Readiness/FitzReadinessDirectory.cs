using System.Globalization;
using Bdgrz.Compliance.Features.Controls;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

sealed class FitzReadinessDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/readiness-directory-v1/projection", ProjectorName),
        IReadinessDirectoryReader, IReadinessDirectoryProjection
{
    public const string ProjectorName = "ReadinessDirectoryV1";
    const int QueryPageSize = 200;
    Uuid? _batchTenantId;

    public new ValueTask<IProjectionBatch> BeginAsync(ProjectionBatchContext context,
        CancellationToken ct = default) => BeginReadinessBatchAsync(context, ct);

    ValueTask<IProjectionBatch> IProjectionStore.BeginAsync(ProjectionBatchContext context,
        CancellationToken ct) => BeginReadinessBatchAsync(context, ct);

    async ValueTask<IProjectionBatch> BeginReadinessBatchAsync(ProjectionBatchContext context,
        CancellationToken ct)
    {
        if (context.Identity.Pattern.Area != ReadinessLedger.Area ||
            context.Identity.Pattern.Resource is not null ||
            !Uuid.TryParse(context.Identity.Pattern.Realm, null, out var tenantId) ||
            tenantId == Uuid.Empty)
            throw new InvalidOperationException(
                "A readiness projection batch requires its tenant's readiness area.");
        var batch = await base.BeginAsync(context, ct).ConfigureAwait(false);
        _batchTenantId = tenantId;
        return batch;
    }

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) => base.LoadCheckpointAsync(new CheckpointIdentity(
        ProjectorName, EventStreamPattern.ForPattern(tenantId.ToString(), ReadinessLedger.Area)), ct);

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case ReadinessAssessmentRecorded ev:
                await ApplyAssessmentAsync(ev, ct).ConfigureAwait(false);
                break;
            case ReadinessGapPlanned ev:
                await ApplyGapPlanAsync(ev, ct).ConfigureAwait(false);
                break;
            case ReadinessDecisionRecorded ev:
                await ApplyDecisionAsync(ev, ct).ConfigureAwait(false);
                break;
            case TypeIEntryDecisionRecorded ev:
                await ApplyTypeIEntryDecisionAsync(ev, ct).ConfigureAwait(false);
                break;
            case ReadinessGapAnnotated ev:
                await ApplyAnnotationAsync(ev, ct).ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException("The readiness event is not supported.");
        }
    }

    public async ValueTask<ReadinessAssessmentView?> GetAssessmentAsync(Uuid tenantId,
        Uuid programId, Uuid assessmentId, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        var program = await ReadinessDirectorySchema.Programs.GetAsync(tx, programId, ct)
            .ConfigureAwait(false);
        var projection = await ReadinessDirectorySchema.AssessmentDetails.GetAsync(tx,
            assessmentId, ct).ConfigureAwait(false);
        if (program is null || program.TenantId != tenantId || program.ProgramId != programId ||
            projection is null || projection.TenantId != tenantId ||
            projection.ProgramId != programId || projection.AssessmentId != assessmentId)
            return null;

        var recorded = projection.Recorded;
        var gaps = new ReadinessGapView[recorded.Gaps.Count];
        for (var i = 0; i < recorded.Gaps.Count; i++)
            gaps[i] = await WithCurrentPlanAsync(tx, tenantId, programId, recorded.Gaps[i], ct)
                .ConfigureAwait(false);

        return new ReadinessAssessmentView(tenantId, programId, assessmentId, program.Revision,
            recorded.RuleVersion, recorded.AsOf, recorded.EditionId, recorded.InputFingerprint,
            recorded.Inputs, recorded.Findings, gaps,
            recorded.Findings.Count(static finding => finding.Outcome == ReadinessRules.RuleMet),
            gaps.Length, recorded.RunBy, recorded.RunAt, projection.Decision,
            projection.TypeIEntryDecision);
    }

    public async ValueTask<Result<Page<ReadinessAssessmentSummaryView>>> ListAssessmentsAsync(
        Uuid tenantId, Uuid programId, int? limit, string? cursor,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        var program = await ReadinessDirectorySchema.Programs.GetAsync(tx, programId, ct)
            .ConfigureAwait(false);
        if (program is null || program.TenantId != tenantId)
            return ControlActivationSource.Paginate<ReadinessAssessmentSummaryView>([], limit,
                cursor, "readiness assessments");

        var projections = await ReadAllAsync(nextCursor =>
            ReadinessDirectorySchema.Assessments.QueryAsync(tx,
                ReadinessDirectorySchema.AssessmentsByProgram.Query()
                    .WithPrefix(programId.ToString()).Take(QueryPageSize).After(nextCursor), ct))
            .ConfigureAwait(false);
        var summaries = projections.Where(view => view.TenantId == tenantId &&
                view.ProgramId == programId)
            .Select(static view => view.Summary).ToArray();
        return ControlActivationSource.Paginate(summaries, limit, cursor,
            "readiness assessments");
    }

    public async ValueTask<Result<Page<ReadinessGapView>>> ListGapsAsync(Uuid tenantId,
        Uuid programId, Uuid assessmentId, string? planState, int? limit, string? cursor,
        Uuid? ownerMemberId, string? kind, string? ruleId, string? subject,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        var assessment = await ReadinessDirectorySchema.AssessmentDetails.GetAsync(tx,
            assessmentId, ct).ConfigureAwait(false);
        if (assessment is null || assessment.TenantId != tenantId ||
            assessment.ProgramId != programId)
            return Result<Page<ReadinessGapView>>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The readiness assessment was not found."));

        var gaps = new List<ReadinessGapView>(assessment.Recorded.Gaps.Count);
        foreach (var gap in assessment.Recorded.Gaps)
        {
            var current = await WithCurrentPlanAsync(tx, tenantId, programId, gap, ct)
                .ConfigureAwait(false);
            if ((planState is null || (current.Plan is not null) == (planState == "planned")) &&
                (ownerMemberId is not { } owner || current.Plan?.OwnerMemberId == owner) &&
                (kind is null || current.Kind == kind) &&
                (ruleId is null || current.RuleId == ruleId) &&
                (subject is null || current.Subject == subject))
                gaps.Add(current);
        }
        return ControlActivationSource.Paginate(gaps, limit, cursor, "readiness gaps");
    }

    public async ValueTask<Result<Page<ReadinessAnnotationView>>> ListAnnotationsAsync(
        Uuid tenantId, Uuid programId, Uuid assessmentId, int? limit, string? cursor,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        var assessment = await ReadinessDirectorySchema.AssessmentDetails.GetAsync(tx,
            assessmentId, ct).ConfigureAwait(false);
        if (assessment is null || assessment.TenantId != tenantId ||
            assessment.ProgramId != programId)
            return Result<Page<ReadinessAnnotationView>>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The readiness assessment was not found."));
        var program = await ReadinessDirectorySchema.Programs.GetAsync(tx, programId, ct)
            .ConfigureAwait(false);
        if (program is null || program.TenantId != tenantId)
            return Result<Page<ReadinessAnnotationView>>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The readiness assessment was not found."));

        var projections = await ReadAllAsync(nextCursor =>
            ReadinessDirectorySchema.Annotations.QueryAsync(tx,
                ReadinessDirectorySchema.AnnotationsByAssessment.Query()
                    .WithPrefix(programId.ToString(), assessmentId.ToString())
                    .Take(QueryPageSize).After(nextCursor), ct)).ConfigureAwait(false);
        var annotations = projections.Where(view => view.TenantId == tenantId &&
                view.ProgramId == programId && view.Annotation.AssessmentId == assessmentId)
            .Select(view => view.Annotation with
            {
                Current = program.LatestAssessmentId == assessmentId,
            }).ToArray();
        return ControlActivationSource.Paginate(annotations, limit, cursor,
            "readiness annotations");
    }

    public async ValueTask<Result<Page<TypeIEntryDecisionView>>> ListTypeIEntryDecisionsAsync(
        Uuid tenantId, Uuid programId, int? limit, string? cursor,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        var program = await ReadinessDirectorySchema.Programs.GetAsync(tx, programId, ct)
            .ConfigureAwait(false);
        if (program is null || program.TenantId != tenantId)
            return ControlActivationSource.Paginate<TypeIEntryDecisionView>([], limit, cursor,
                "Type I entry decisions");

        var projections = await ReadAllAsync(nextCursor =>
            ReadinessDirectorySchema.TypeIEntries.QueryAsync(tx,
                ReadinessDirectorySchema.TypeIEntriesByProgram.Query()
                    .WithPrefix(programId.ToString()).Take(QueryPageSize).After(nextCursor), ct))
            .ConfigureAwait(false);
        var decisions = projections.Where(view => view.TenantId == tenantId &&
                view.ProgramId == programId)
            .Select(static view => view.Decision).ToArray();
        return ControlActivationSource.Paginate(decisions, limit, cursor,
            "Type I entry decisions");
    }

    async ValueTask ApplyAssessmentAsync(ReadinessAssessmentRecorded ev, CancellationToken ct)
    {
        RequireBatchTenant(ev.TenantId, ev.ProgramId, ev.AssessmentId);
        var state = await BeginEventAsync(ev.TenantId, ev.ProgramId, ev.Revision, ct)
            .ConfigureAwait(false);
        if (state.Skip)
            return;
        var details = await ReadinessDirectorySchema.AssessmentDetails.GetAsync(Transaction,
            ev.AssessmentId, ct).ConfigureAwait(false);
        if (details is not null)
            throw new InvalidOperationException("A readiness assessment cannot replace retained content.");
        var recorded = new ReadinessAssessmentProjection(ev.TenantId, ev.ProgramId,
            ev.AssessmentId, ev, null, null);
        await ReadinessDirectorySchema.AssessmentDetails.InsertAsync(Transaction, recorded, ct)
            .ConfigureAwait(false);
        var summary = new ReadinessAssessmentSummaryView(ev.AssessmentId, ev.RuleVersion, ev.AsOf,
            ev.Findings.Count(static finding => finding.Outcome == ReadinessRules.RuleMet),
            ev.Gaps.Count, ev.RunBy, ev.RunAt, null);
        await ReadinessDirectorySchema.Assessments.InsertAsync(Transaction,
            new ReadinessAssessmentSummaryProjection(ev.TenantId, ev.ProgramId,
                ev.AssessmentId, ev.Revision, summary), ct).ConfigureAwait(false);
        await StoreNextStateAsync(state.Current, ev.TenantId, ev.ProgramId, ev.Revision,
            ev.AssessmentId, ct).ConfigureAwait(false);
    }

    async ValueTask ApplyGapPlanAsync(ReadinessGapPlanned ev, CancellationToken ct)
    {
        RequireBatchTenant(ev.TenantId, ev.ProgramId, ev.Plan.GapId);
        var state = await BeginEventAsync(ev.TenantId, ev.ProgramId, ev.Revision, ct)
            .ConfigureAwait(false);
        if (state.Skip)
            return;
        var current = await ReadinessDirectorySchema.GapPlans.GetAsync(Transaction,
            ev.Plan.GapId, ct).ConfigureAwait(false);
        var next = new ReadinessGapPlanProjection(ev.TenantId, ev.ProgramId, ev.Revision, ev.Plan);
        if (current is null)
            await ReadinessDirectorySchema.GapPlans.InsertAsync(Transaction, next, ct)
                .ConfigureAwait(false);
        else if (current.TenantId != ev.TenantId || current.ProgramId != ev.ProgramId)
            throw new InvalidOperationException("A readiness gap plan must remain in its program.");
        else
            await ReadinessDirectorySchema.GapPlans.ReplaceAsync(Transaction, current, next, ct)
                .ConfigureAwait(false);
        await StoreNextStateAsync(state.Current, ev.TenantId, ev.ProgramId, ev.Revision,
            state.Current!.LatestAssessmentId, ct).ConfigureAwait(false);
    }

    async ValueTask ApplyDecisionAsync(ReadinessDecisionRecorded ev, CancellationToken ct)
    {
        RequireBatchTenant(ev.TenantId, ev.ProgramId, ev.Decision.AssessmentId);
        var state = await BeginEventAsync(ev.TenantId, ev.ProgramId, ev.Revision, ct)
            .ConfigureAwait(false);
        if (state.Skip)
            return;
        var current = await GetAssessmentForUpdateAsync(ev.TenantId, ev.ProgramId,
            ev.Decision.AssessmentId, ct).ConfigureAwait(false);
        await ReadinessDirectorySchema.AssessmentDetails.ReplaceAsync(Transaction, current,
            current with { Decision = ev.Decision }, ct).ConfigureAwait(false);
        var summary = await ReadinessDirectorySchema.Assessments.GetAsync(Transaction,
            ev.Decision.AssessmentId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("A readiness decision requires its assessment.");
        await ReadinessDirectorySchema.Assessments.ReplaceAsync(Transaction, summary,
            summary with { Summary = summary.Summary with { DecisionOutcome = ev.Decision.Outcome } },
            ct).ConfigureAwait(false);
        await StoreNextStateAsync(state.Current, ev.TenantId, ev.ProgramId, ev.Revision,
            state.Current!.LatestAssessmentId, ct).ConfigureAwait(false);
    }

    async ValueTask ApplyTypeIEntryDecisionAsync(TypeIEntryDecisionRecorded ev,
        CancellationToken ct)
    {
        RequireBatchTenant(ev.TenantId, ev.ProgramId, ev.Decision.DecisionId);
        var state = await BeginEventAsync(ev.TenantId, ev.ProgramId, ev.Revision, ct)
            .ConfigureAwait(false);
        if (state.Skip)
            return;
        var current = await GetAssessmentForUpdateAsync(ev.TenantId, ev.ProgramId,
            ev.Decision.AssessmentId, ct).ConfigureAwait(false);
        await ReadinessDirectorySchema.AssessmentDetails.ReplaceAsync(Transaction, current,
            current with { TypeIEntryDecision = ev.Decision }, ct).ConfigureAwait(false);
        await ReadinessDirectorySchema.TypeIEntries.InsertAsync(Transaction,
            new ReadinessTypeIEntryDecisionProjection(ev.TenantId, ev.ProgramId, ev.Revision,
                ev.Decision), ct).ConfigureAwait(false);
        await StoreNextStateAsync(state.Current, ev.TenantId, ev.ProgramId, ev.Revision,
            state.Current!.LatestAssessmentId, ct).ConfigureAwait(false);
    }

    async ValueTask ApplyAnnotationAsync(ReadinessGapAnnotated ev, CancellationToken ct)
    {
        RequireBatchTenant(ev.TenantId, ev.ProgramId, ev.Annotation.AnnotationId);
        var state = await BeginEventAsync(ev.TenantId, ev.ProgramId, ev.Revision, ct)
            .ConfigureAwait(false);
        if (state.Skip)
            return;
        _ = await GetAssessmentForUpdateAsync(ev.TenantId, ev.ProgramId,
            ev.Annotation.AssessmentId, ct).ConfigureAwait(false);
        await ReadinessDirectorySchema.Annotations.InsertAsync(Transaction,
            new ReadinessAnnotationProjection(ev.TenantId, ev.ProgramId, ev.Revision,
                ev.Annotation), ct).ConfigureAwait(false);
        await StoreNextStateAsync(state.Current, ev.TenantId, ev.ProgramId, ev.Revision,
            state.Current!.LatestAssessmentId, ct).ConfigureAwait(false);
    }

    async ValueTask<ReadinessAssessmentProjection> GetAssessmentForUpdateAsync(Uuid tenantId,
        Uuid programId, Uuid assessmentId, CancellationToken ct)
    {
        var current = await ReadinessDirectorySchema.AssessmentDetails.GetAsync(Transaction,
            assessmentId, ct).ConfigureAwait(false);
        if (current is null || current.TenantId != tenantId || current.ProgramId != programId)
            throw new InvalidOperationException("A readiness ledger event requires its assessment.");
        return current;
    }

    async ValueTask<(bool Skip, ReadinessProgramProjection? Current)> BeginEventAsync(Uuid tenantId,
        Uuid programId, long revision, CancellationToken ct)
    {
        var current = await ReadinessDirectorySchema.Programs.GetAsync(Transaction, programId, ct)
            .ConfigureAwait(false);
        if (current is not null && (current.TenantId != tenantId || current.ProgramId != programId))
            throw new InvalidOperationException("A readiness projection cannot cross tenants.");
        if (current is not null && revision <= current.Revision)
            return (true, current);
        if (current is null ? revision != 1 : revision != current.Revision + 1)
            throw new InvalidOperationException("Readiness events must project in ledger revision order.");
        return (false, current);
    }

    async ValueTask StoreNextStateAsync(ReadinessProgramProjection? current, Uuid tenantId,
        Uuid programId, long revision, Uuid latestAssessmentId, CancellationToken ct)
    {
        var next = new ReadinessProgramProjection(tenantId, programId, revision,
            latestAssessmentId);
        if (current is null)
            await ReadinessDirectorySchema.Programs.InsertAsync(Transaction, next, ct)
                .ConfigureAwait(false);
        else
            await ReadinessDirectorySchema.Programs.ReplaceAsync(Transaction, current, next, ct)
                .ConfigureAwait(false);
    }

    static async ValueTask<ReadinessGapView> WithCurrentPlanAsync(IKvTransaction tx, Uuid tenantId,
        Uuid programId, ReadinessGapView gap, CancellationToken ct)
    {
        var plan = await ReadinessDirectorySchema.GapPlans.GetAsync(tx, gap.GapId, ct)
            .ConfigureAwait(false);
        return gap with
        {
            Plan = plan is { TenantId: var planTenant, ProgramId: var planProgram } &&
                   planTenant == tenantId && planProgram == programId
                ? plan.Plan
                : null,
        };
    }

    static async ValueTask<IReadOnlyList<T>> ReadAllAsync<T>(
        Func<string?, ValueTask<Page<T>>> readPage)
    {
        var items = new List<T>();
        string? cursor = null;
        do
        {
            var page = await readPage(cursor).ConfigureAwait(false);
            items.AddRange(page.Items);
            cursor = page.NextCursor;
        } while (cursor is not null);
        return items;
    }

    void RequireBatchTenant(Uuid tenantId, Uuid programId, Uuid recordId)
    {
        if (tenantId == Uuid.Empty || programId == Uuid.Empty || recordId == Uuid.Empty ||
            tenantId != _batchTenantId)
            throw new InvalidOperationException("A readiness event must belong to its tenant projection batch.");
    }
}
