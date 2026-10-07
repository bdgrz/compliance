using Bdgrz.Compliance.Features.Remediation;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Projects independent closure work without changing the finding workflow.</summary>
sealed class FitzFindingClosureWorkItemDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/accountable-work-items/finding-closure-v1",
        ProjectorName), IAccountableWorkItemDirectoryReader, IFindingClosureWorkItemProjection
{
    public const string ProjectorName = "AccountableWorkItemFindingClosureV1";

    string IAccountableWorkItemDirectoryReader.ProjectorName => ProjectorName;
    public IReadOnlyCollection<string> ProjectedKinds => [FindingClosureWork.Kind];

    public EventStreamPattern SourcePattern(Uuid tenantId) =>
        EventStreamPattern.ForPattern(tenantId.ToString(), "remediation");

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) => base.LoadCheckpointAsync(new CheckpointIdentity(
        ProjectorName, SourcePattern(tenantId)), ct);

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        var scope = domainEvent switch
        {
            FindingRaised ev => (ev.TenantId, ev.ProgramId, ev.FindingId, ev.Revision),
            FindingRevised ev => (ev.TenantId, ev.ProgramId, ev.FindingId, ev.Revision),
            CorrectiveActionAdded ev => (ev.TenantId, ev.ProgramId, ev.FindingId, ev.Revision),
            CorrectiveActionCompleted ev => (ev.TenantId, ev.ProgramId, ev.FindingId, ev.Revision),
            FindingAcceptanceLinked ev => (ev.TenantId, ev.ProgramId, ev.FindingId, ev.Revision),
            FindingClosed ev => (ev.TenantId, ev.ProgramId, ev.FindingId, ev.Revision),
            FindingReopened ev => (ev.TenantId, ev.ProgramId, ev.FindingId, ev.Revision),
            _ => throw new InvalidOperationException("The finding closure projector received an unknown event."),
        };
        if (scope.TenantId == Uuid.Empty || scope.ProgramId == Uuid.Empty ||
            scope.FindingId == Uuid.Empty)
            throw new InvalidOperationException("Finding closure work requires complete source scope.");
        var current = await FindingClosureWorkItemDirectorySchema.Findings.GetAsync(Transaction,
            scope.FindingId, ct).ConfigureAwait(false);
        if (domainEvent is FindingRaised raised)
        {
            if (current is not null || raised.Revision != 1 || raised.OwnerMemberId == Uuid.Empty)
                throw new InvalidOperationException("A finding closure projection must start at revision one.");
            await FindingClosureWorkItemDirectorySchema.Findings.InsertAsync(Transaction, new(
                raised.TenantId, raised.ProgramId, raised.FindingId, raised.Revision, raised.Title,
                raised.Severity, raised.OwnerMemberId, raised.DueOn, [], false, raised.RaisedAt), ct)
                .ConfigureAwait(false);
            return;
        }
        if (current is null || current.TenantId != scope.TenantId ||
            current.ProgramId != scope.ProgramId || scope.Revision != current.Revision + 1)
            throw new InvalidOperationException("Finding closure work cannot change scope or skip source revisions.");
        if (current.Closed && domainEvent is not FindingReopened ||
            !current.Closed && domainEvent is FindingReopened ||
            domainEvent is FindingClosed && FindingClosureWork.Candidate(current) is null)
            throw new InvalidOperationException("Finding closure events must preserve the source lifecycle.");
        var next = domainEvent switch
        {
            FindingRevised ev => current with
            {
                OwnerMemberId = ev.OwnerMemberId,
                DueOn = ev.DueOn,
                Severity = ev.Severity,
                ChangedAt = ev.RevisedAt,
            },
            CorrectiveActionAdded ev => AddAction(current, ev),
            CorrectiveActionCompleted ev => CompleteAction(current, ev),
            FindingAcceptanceLinked ev => current with { ChangedAt = ev.Acceptance.LinkedAt },
            FindingClosed ev => current with { Closed = true, ChangedAt = ev.Closure.ClosedAt },
            FindingReopened ev => current with { Closed = false, ChangedAt = ev.ReopenedAt },
            _ => throw new InvalidOperationException("The finding closure event cannot update this source."),
        };
        await FindingClosureWorkItemDirectorySchema.Findings.ReplaceAsync(Transaction, current,
            next with { Revision = scope.Revision }, ct).ConfigureAwait(false);
    }

    public async ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadProgramAsync(Uuid tenantId,
        Uuid programId, CancellationToken ct = default)
    {
        var candidates = new List<WorkCandidate>();
        string? cursor = null;
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        do
        {
            var page = await FindingClosureWorkItemDirectorySchema.Findings.QueryAsync(tx,
                FindingClosureWorkItemDirectorySchema.ByProgram.Query()
                    .WithPrefix(programId.ToString()).Take(200).After(cursor), ct)
                .ConfigureAwait(false);
            foreach (var finding in page.Items)
            {
                if (finding.TenantId != tenantId || finding.ProgramId != programId ||
                    finding.FindingId == Uuid.Empty || finding.OwnerMemberId == Uuid.Empty ||
                    finding.Revision < 1 || finding.Actions is null || finding.Actions.Any(static action =>
                        action.OwnerMemberId == Uuid.Empty || action.ActionId == Uuid.Empty ||
                        action.Status is not (RemediationLedger.Open or RemediationLedger.Completed) ||
                        action.Status == RemediationLedger.Completed &&
                        action.CompletedByMemberId is null ||
                        action.Status == RemediationLedger.Completed &&
                        action.CompletedByMemberId == Uuid.Empty))
                    return Result<IReadOnlyList<WorkCandidate>>.Failure(new RequestError(
                        RequestErrorKind.Conflict, "The finding closure work projection has invalid source scope."));
                if (FindingClosureWork.Candidate(finding) is { } candidate)
                    candidates.Add(candidate);
            }
            cursor = page.NextCursor;
        } while (cursor is not null);
        return Result<IReadOnlyList<WorkCandidate>>.Success(candidates);
    }

    static FindingClosureWorkState AddAction(FindingClosureWorkState current,
        CorrectiveActionAdded ev)
    {
        if (current.Closed || ev.Action.Status != RemediationLedger.Open ||
            ev.Action.OwnerMemberId == Uuid.Empty || ev.Action.ActionId == Uuid.Empty ||
            current.Actions.Any(action => action.ActionId == ev.Action.ActionId))
            throw new InvalidOperationException("A finding closure projection requires a new open corrective action.");
        return current with { Actions = [.. current.Actions, ev.Action], ChangedAt = ev.Action.AddedAt };
    }

    static FindingClosureWorkState CompleteAction(FindingClosureWorkState current,
        CorrectiveActionCompleted ev)
    {
        if (current.Closed || ev.CompletedByMemberId == Uuid.Empty ||
            !current.Actions.Any(action => action.ActionId == ev.ActionId &&
                action.Status == RemediationLedger.Open))
            throw new InvalidOperationException("A finding closure projection cannot complete unknown or completed work.");
        return current with
        {
            Actions = current.Actions.Select(action => action.ActionId != ev.ActionId ? action :
                action with
                {
                    Status = RemediationLedger.Completed,
                    CompletedByMemberId = ev.CompletedByMemberId,
                    CompletedBy = ev.CompletedBy,
                    CompletedAt = ev.CompletedAt,
                    ResolutionNotes = ev.ResolutionNotes,
                    Evidence = ev.Evidence,
                }).ToArray(),
            ChangedAt = ev.CompletedAt,
        };
    }
}
