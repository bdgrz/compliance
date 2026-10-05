using Bdgrz.Compliance.Features.Evaluations;
using Bdgrz.Compliance.Features.Operations;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

sealed class FitzControlEvaluationWorkItemDirectory(IKvClient client)
    : FitzKvProjectionStore(client,
        "kv://bdgrz/accountable-work-items/control-evaluations-v1", ProjectorName),
      IAccountableWorkItemDirectoryReader, IControlEvaluationWorkItemProjection
{
    public const string ProjectorName = "AccountableWorkItemControlEvaluationV1";
    const string RevisionKey = ProjectorName;
    const string InProgress = "in_progress";
    const string Submitted = "submitted";
    const string Accepted = "accepted";
    const string Rejected = "rejected";
    const string ChangesRequested = "changes_requested";

    string IAccountableWorkItemDirectoryReader.ProjectorName => ProjectorName;

    public IReadOnlyCollection<string> ProjectedKinds => [WorkSource.ControlEvaluationReview];

    public EventStreamPattern SourcePattern(Uuid tenantId) =>
        EventStreamPattern.ForPattern(tenantId.ToString(), "control-evaluations");

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) => base.LoadCheckpointAsync(new CheckpointIdentity(
        ProjectorName, SourcePattern(tenantId)), ct);

    public async ValueTask<long> LoadRevisionAsync(Uuid tenantId,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return (await ControlEvaluationWorkItemDirectorySchema.Revisions.GetAsync(tx,
            RevisionKey, ct).ConfigureAwait(false))?.Revision ?? 0;
    }

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        var scope = ScopeFor(domainEvent);
        if (scope.TenantId == Uuid.Empty || scope.ProgramId == Uuid.Empty ||
            scope.ControlId == Uuid.Empty || scope.EvaluationId == Uuid.Empty)
            throw new InvalidOperationException(
                "A control evaluation event must have a complete tenant and source scope.");

        var key = ControlEvaluationWorkItemDirectorySchema.EvaluationKey(scope.ProgramId,
            scope.EvaluationId);
        var current = await ControlEvaluationWorkItemDirectorySchema.Evaluations.GetAsync(
            Transaction, key, ct).ConfigureAwait(false);
        if (domainEvent is ControlEvaluationStarted started)
        {
            if (current is not null || scope.Revision != 1 || started.EvaluatorMemberId == Uuid.Empty)
                throw new InvalidOperationException(
                    "A control evaluation must start once at revision one with an evaluator.");
            var initial = new ControlEvaluationWorkState(started.TenantId, started.ProgramId,
                started.ControlId, started.EvaluationId, started.EvaluatorMemberId,
                started.Revision, 1, InProgress, null);
            await ControlEvaluationWorkItemDirectorySchema.Evaluations.InsertAsync(Transaction,
                initial, ct).ConfigureAwait(false);
        }
        else
        {
            if (current is null || current.TenantId != scope.TenantId ||
                current.ProgramId != scope.ProgramId || current.ControlId != scope.ControlId ||
                current.EvaluationId != scope.EvaluationId ||
                scope.Revision != checked(current.Revision + 1))
                throw new InvalidOperationException(
                    "A control evaluation event must advance its existing source revision and scope.");

            var next = domainEvent switch
            {
                ControlEvaluationStepRecorded step => ApplyStep(current, step),
                ControlEvaluationDeviationDisposed disposed => ApplyDisposition(current, disposed),
                ControlEvaluationSubmitted submitted => await SubmitAsync(current, submitted, ct)
                    .ConfigureAwait(false),
                ControlEvaluationReviewed reviewed => await ReviewAsync(current, reviewed, ct)
                    .ConfigureAwait(false),
                _ => throw new InvalidOperationException(
                    $"The control evaluation projector cannot apply {domainEvent.GetType().Name}.")
            };
            await ControlEvaluationWorkItemDirectorySchema.Evaluations.ReplaceAsync(Transaction,
                current, next, ct).ConfigureAwait(false);
        }

        await IncrementRevisionAsync(ct).ConfigureAwait(false);
    }

    public async ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadProgramAsync(Uuid tenantId,
        Uuid programId, CancellationToken ct = default)
    {
        var candidates = new List<WorkCandidate>();
        string? cursor = null;
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        do
        {
            var page = await ControlEvaluationWorkItemDirectorySchema.WorkItems.QueryAsync(tx,
                ControlEvaluationWorkItemDirectorySchema.ByProgram.Query()
                    .WithPrefix(programId.ToString()).Take(200).After(cursor), ct)
                .ConfigureAwait(false);
            foreach (var item in page.Items)
            {
                var state = await ControlEvaluationWorkItemDirectorySchema.Evaluations.GetAsync(tx,
                    ControlEvaluationWorkItemDirectorySchema.EvaluationKey(programId,
                        item.SourceId), ct).ConfigureAwait(false);
                if (item.TenantId != tenantId || item.ProgramId != programId ||
                    item.Kind != WorkSource.ControlEvaluationReview || item.FindingId is not null ||
                    item.RiskId is not null || state is null || state.TenantId != tenantId ||
                    state.ProgramId != programId || state.EvaluationId != item.SourceId ||
                    state.ControlId != item.ControlId || state.State != Submitted ||
                    state.SubmittedAt != item.CreatedAt ||
                    item.WorkItemId != WorkItemIdFor(state.EvaluationId, state.Round))
                    return Result<IReadOnlyList<WorkCandidate>>.Failure(InvalidScope());
                candidates.Add(item.ToCandidate());
            }
            cursor = page.NextCursor;
        } while (cursor is not null);

        return Result<IReadOnlyList<WorkCandidate>>.Success(candidates);
    }

    static ControlEvaluationWorkState ApplyStep(ControlEvaluationWorkState current,
        ControlEvaluationStepRecorded step)
    {
        if (current.State != InProgress || step.Result.Round != current.Round)
            throw new InvalidOperationException(
                "A control evaluation step must belong to its current open round.");
        return current with { Revision = step.Revision };
    }

    static ControlEvaluationWorkState ApplyDisposition(ControlEvaluationWorkState current,
        ControlEvaluationDeviationDisposed disposed)
    {
        if (current.State != InProgress)
            throw new InvalidOperationException(
                "A control evaluation deviation can only be dispositioned in an open round.");
        return current with { Revision = disposed.Revision };
    }

    async ValueTask<ControlEvaluationWorkState> SubmitAsync(ControlEvaluationWorkState current,
        ControlEvaluationSubmitted submitted, CancellationToken ct)
    {
        if (current.State != InProgress || submitted.Submission.Round != current.Round)
            throw new InvalidOperationException(
                "A control evaluation submission must belong to its current open round.");
        var candidate = ReviewCandidate(submitted.TenantId, submitted.ProgramId,
            submitted.ControlId, submitted.EvaluationId, current.EvaluatorMemberId,
            current.Round, submitted.Submission.SubmittedAt);
        await AddWorkItemAsync(submitted.TenantId, submitted.ProgramId, candidate, ct)
            .ConfigureAwait(false);
        return current with
        {
            Revision = submitted.Revision,
            State = Submitted,
            SubmittedAt = submitted.Submission.SubmittedAt,
        };
    }

    async ValueTask<ControlEvaluationWorkState> ReviewAsync(ControlEvaluationWorkState current,
        ControlEvaluationReviewed reviewed, CancellationToken ct)
    {
        if (current.State != Submitted || reviewed.Review.Round != current.Round ||
            reviewed.Review.Decision is not (Accepted or Rejected or ChangesRequested))
            throw new InvalidOperationException(
                "A control evaluation review must resolve the currently submitted round.");
        await RemoveWorkItemAsync(reviewed.TenantId, reviewed.ProgramId,
            reviewed.EvaluationId, current.Round, ct).ConfigureAwait(false);
        var accepted = reviewed.Review.Decision == Accepted;
        return current with
        {
            Revision = reviewed.Revision,
            Round = accepted ? current.Round : checked(current.Round + 1),
            State = accepted ? Accepted : InProgress,
            SubmittedAt = null,
        };
    }

    async ValueTask AddWorkItemAsync(Uuid tenantId, Uuid programId, WorkCandidate candidate,
        CancellationToken ct)
    {
        var workItem = AccountableWorkItemView.FromCandidate(tenantId, programId, candidate);
        var existing = await ControlEvaluationWorkItemDirectorySchema.WorkItems.GetAsync(
            Transaction, candidate.WorkItemId, ct).ConfigureAwait(false);
        if (existing is null)
            await ControlEvaluationWorkItemDirectorySchema.WorkItems.InsertAsync(Transaction,
                workItem, ct).ConfigureAwait(false);
        else if (existing.TenantId != tenantId || existing.ProgramId != programId ||
                 existing.SourceId != candidate.SourceId || existing.Kind != candidate.Kind)
            throw new InvalidOperationException(
                "A control evaluation work item cannot change its source scope.");
        else
            await ControlEvaluationWorkItemDirectorySchema.WorkItems.ReplaceAsync(Transaction,
                existing, workItem, ct).ConfigureAwait(false);
    }

    async ValueTask RemoveWorkItemAsync(Uuid tenantId, Uuid programId, Uuid evaluationId,
        int round, CancellationToken ct)
    {
        var workItemId = WorkItemIdFor(evaluationId, round);
        var existing = await ControlEvaluationWorkItemDirectorySchema.WorkItems.GetAsync(
            Transaction, workItemId, ct).ConfigureAwait(false) ??
            throw new InvalidOperationException(
                "A control evaluation review cannot complete before its submission is projected.");
        if (existing.TenantId != tenantId || existing.ProgramId != programId ||
            existing.SourceId != evaluationId || existing.Kind != WorkSource.ControlEvaluationReview ||
            existing.WorkItemId != workItemId)
            throw new InvalidOperationException(
                "A control evaluation review cannot be removed outside its source scope.");
        await ControlEvaluationWorkItemDirectorySchema.WorkItems.DeleteAsync(Transaction, existing,
            ct).ConfigureAwait(false);
    }

    async ValueTask IncrementRevisionAsync(CancellationToken ct)
    {
        var current = await ControlEvaluationWorkItemDirectorySchema.Revisions.GetAsync(
            Transaction, RevisionKey, ct).ConfigureAwait(false);
        var next = new AccountableWorkItemProjectionRevision(ProjectorName,
            checked((current?.Revision ?? 0) + 1));
        if (current is null)
            await ControlEvaluationWorkItemDirectorySchema.Revisions.InsertAsync(Transaction, next,
                ct).ConfigureAwait(false);
        else
            await ControlEvaluationWorkItemDirectorySchema.Revisions.ReplaceAsync(Transaction,
                current, next, ct).ConfigureAwait(false);
    }

    static WorkCandidate ReviewCandidate(Uuid tenantId, Uuid programId, Uuid controlId,
        Uuid evaluationId, Uuid evaluatorMemberId, int round, DateTimeOffset submittedAt)
        => WorkSource.ControlEvaluationReviewCandidate(tenantId, programId, controlId,
            evaluationId, evaluatorMemberId, round, submittedAt);

    static Uuid WorkItemIdFor(Uuid evaluationId, int round) =>
        WorkSource.ControlEvaluationReviewWorkItemId(evaluationId, round);

    static (Uuid TenantId, Uuid ProgramId, Uuid ControlId, Uuid EvaluationId, long Revision)
        ScopeFor(DomainEvent domainEvent) => domainEvent switch
        {
            ControlEvaluationStarted ev => (ev.TenantId, ev.ProgramId, ev.ControlId, ev.EvaluationId,
                ev.Revision),
            ControlEvaluationStepRecorded ev => (ev.TenantId, ev.ProgramId, ev.ControlId,
                ev.EvaluationId, ev.Revision),
            ControlEvaluationDeviationDisposed ev => (ev.TenantId, ev.ProgramId, ev.ControlId,
                ev.EvaluationId, ev.Revision),
            ControlEvaluationSubmitted ev => (ev.TenantId, ev.ProgramId, ev.ControlId,
                ev.EvaluationId, ev.Revision),
            ControlEvaluationReviewed ev => (ev.TenantId, ev.ProgramId, ev.ControlId, ev.EvaluationId,
                ev.Revision),
            _ => throw new InvalidOperationException(
                $"The control evaluation projector cannot apply {domainEvent.GetType().Name}.")
        };

    static RequestError InvalidScope() => new(RequestErrorKind.Conflict,
        "The control evaluation work projection has an invalid tenant or program scope.");
}
