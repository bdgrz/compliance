using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Policies;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

sealed class FitzPolicyDecisionWorkItemDirectory(IKvClient client, IAggregateReader sourceReader,
    TimeProvider clock)
    : FitzKvProjectionStore(client,
        "kv://bdgrz/accountable-work-items/policy-decisions-v1", ProjectorName),
      IAccountableWorkItemDirectoryReader, IPolicyDecisionWorkItemProjection
{
    public const string ProjectorName = "AccountableWorkItemPolicyDecisionV1";
    const string RevisionKey = ProjectorName;

    string IAccountableWorkItemDirectoryReader.ProjectorName => ProjectorName;

    public IReadOnlyCollection<string> ProjectedKinds => PolicyDecisionWork.ProjectedKinds;

    public EventStreamPattern SourcePattern(Uuid tenantId) =>
        EventStreamPattern.ForPattern(tenantId.ToString(), "policies");

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) => base.LoadCheckpointAsync(new CheckpointIdentity(
        ProjectorName, SourcePattern(tenantId)), ct);

    public async ValueTask<long> LoadRevisionAsync(Uuid tenantId,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return (await PolicyDecisionWorkItemDirectorySchema.Revisions.GetAsync(tx,
            RevisionKey, ct).ConfigureAwait(false))?.Revision ?? 0;
    }

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        var (tenantId, programId, policyId) = ScopeFor(domainEvent);
        if (tenantId == Uuid.Empty || programId == Uuid.Empty || policyId == Uuid.Empty)
            throw new InvalidOperationException(
                "A policy work event must have complete tenant, program, and policy scope.");

        var current = await PolicyDecisionWorkItemDirectorySchema.Policies.GetAsync(Transaction,
            policyId, ct).ConfigureAwait(false);
        if (current is not null && (current.TenantId != tenantId ||
            current.ProgramId != programId || current.PolicyId != policyId))
            throw new InvalidOperationException(
                "A policy work event cannot replace state outside its recorded scope.");

        var policy = await sourceReader.HydrateAsync(new Policy(tenantId, policyId), ct)
            .ConfigureAwait(false);
        if (policy.ProgramId == Uuid.Empty || policy.ProgramId != programId)
            throw new InvalidOperationException(
                "A policy work event must match its authoritative program scope.");

        if (!policy.IsVisible)
        {
            if (current is not null)
                await PolicyDecisionWorkItemDirectorySchema.Policies.DeleteAsync(Transaction,
                    current, ct).ConfigureAwait(false);
        }
        else
        {
            var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
            var view = policy.ToView(today) ?? throw new InvalidOperationException(
                "A visible policy must produce its current work view.");
            var candidates = PolicyDecisionWork.Candidates(policy, view, tenantId, programId,
                DateOnly.MaxValue, null);
            var next = new PolicyDecisionWorkState(tenantId, programId, policyId,
                policy.Revision, candidates.Select(candidate =>
                    AccountableWorkItemView.FromCandidate(tenantId, programId, candidate)).ToArray());
            if (current is null)
                await PolicyDecisionWorkItemDirectorySchema.Policies.InsertAsync(Transaction, next,
                    ct).ConfigureAwait(false);
            else
                await PolicyDecisionWorkItemDirectorySchema.Policies.ReplaceAsync(Transaction,
                    current, next, ct).ConfigureAwait(false);
        }

        await IncrementRevisionAsync(ct).ConfigureAwait(false);
    }

    public ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadProgramAsync(Uuid tenantId,
        Uuid programId, CancellationToken ct = default) =>
        LoadProgramAsync(tenantId, programId, DateOnly.MinValue, DateOnly.MaxValue, null, ct);

    public async ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadProgramAsync(Uuid tenantId,
        Uuid programId, DateOnly today, DateOnly horizon, Uuid? workItemId,
        CancellationToken ct = default)
    {
        _ = today;
        var candidates = new List<WorkCandidate>();
        string? cursor = null;
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        do
        {
            var page = await PolicyDecisionWorkItemDirectorySchema.Policies.QueryAsync(tx,
                PolicyDecisionWorkItemDirectorySchema.ByProgram.Query()
                    .WithPrefix(programId.ToString()).Take(200).After(cursor), ct)
                .ConfigureAwait(false);
            foreach (var state in page.Items)
            {
                if (state.TenantId != tenantId || state.ProgramId != programId ||
                    state.PolicyId == Uuid.Empty || state.SourceRevision < 1 ||
                    state.Items is null)
                    return Result<IReadOnlyList<WorkCandidate>>.Failure(InvalidScope());

                var sourcePath = $"/api/v1/tenants/{tenantId}/programs/{programId}/policies/" +
                                 $"{state.PolicyId}/";
                var workItemIds = new HashSet<Uuid>();
                foreach (var item in state.Items)
                {
                    if (item.TenantId != tenantId || item.ProgramId != programId ||
                        item.WorkItemId == Uuid.Empty || item.SourceId != state.PolicyId ||
                        !ProjectedKinds.Contains(item.Kind, StringComparer.Ordinal) ||
                        item.Responsible != new OperatingHolder(
                            OperatingAuthority.ProgramReviewerHolder, programId) ||
                        !item.ActionPath.StartsWith(sourcePath, StringComparison.Ordinal) ||
                        item.Excluded is null || !workItemIds.Add(item.WorkItemId))
                        return Result<IReadOnlyList<WorkCandidate>>.Failure(InvalidScope());

                    if ((item.DueOn is { } dueOn && dueOn > horizon) ||
                        (workItemId is { } wanted && item.WorkItemId != wanted))
                        continue;
                    candidates.Add(item.ToCandidate());
                }
            }
            cursor = page.NextCursor;
        } while (cursor is not null);

        return Result<IReadOnlyList<WorkCandidate>>.Success(candidates);
    }

    async ValueTask IncrementRevisionAsync(CancellationToken ct)
    {
        var current = await PolicyDecisionWorkItemDirectorySchema.Revisions.GetAsync(Transaction,
            RevisionKey, ct).ConfigureAwait(false);
        var next = new AccountableWorkItemProjectionRevision(ProjectorName,
            checked((current?.Revision ?? 0) + 1));
        if (current is null)
            await PolicyDecisionWorkItemDirectorySchema.Revisions.InsertAsync(Transaction, next, ct)
                .ConfigureAwait(false);
        else
            await PolicyDecisionWorkItemDirectorySchema.Revisions.ReplaceAsync(Transaction, current,
                next, ct).ConfigureAwait(false);
    }

    static (Uuid TenantId, Uuid ProgramId, Uuid PolicyId) ScopeFor(DomainEvent domainEvent) =>
        domainEvent switch
        {
            PolicyDraftCreated ev => (ev.TenantId, ev.ProgramId, ev.PolicyId),
            PolicyDraftRevised ev => (ev.TenantId, ev.ProgramId, ev.PolicyId),
            PolicyReviewed ev => (ev.TenantId, ev.ProgramId, ev.PolicyId),
            PolicyApproved ev => (ev.TenantId, ev.ProgramId, ev.PolicyId),
            PolicyPeriodicReviewConfirmed ev => (ev.TenantId, ev.ProgramId, ev.PolicyId),
            PolicyRetirementProposed ev => (ev.TenantId, ev.ProgramId, ev.PolicyId),
            PolicyRetired ev => (ev.TenantId, ev.ProgramId, ev.PolicyId),
            PolicyDraftDiscarded ev => (ev.TenantId, ev.ProgramId, ev.PolicyId),
            _ => throw new InvalidOperationException(
                $"The policy work projector cannot apply {domainEvent.GetType().Name}.")
        };

    static RequestError InvalidScope() => new(RequestErrorKind.Conflict,
        "The policy work projection has an invalid tenant or program scope.");
}
