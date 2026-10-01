using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>
///     A team member eligible under the source workflow claims unassigned team work. Claimed or
///     member-held work cannot be claimed.
/// </summary>
public sealed class ClaimWorkItemHandler(IAggregateExecutor executor, WorkQueueReader queue,
    TimeProvider clock) : IRequestHandler<ClaimWorkItem, WorkItemDetailView>
{
    public ValueTask<Result<WorkItemDetailView>> HandleAsync(
        IRequestContext<ClaimWorkItem> context, CancellationToken ct)
    {
        var request = context.Request;
        return WorkCommands.RunAsync(queue, context, request.TenantId, request.ProgramId,
            request.WorkItemId, async (actor, snapshot, entry) =>
            {
                if (entry.Candidate.Responsible.Kind != OperatingAuthority.TeamHolder)
                    return WorkCommands.Fail(RequestErrorKind.Validation,
                        "Only team work can be claimed.");
                if (entry.Item.AssigneeMemberId is not null)
                    return WorkCommands.Fail(RequestErrorKind.Conflict,
                        "The work item is already assigned.");
                if (!entry.ActorEligible || !entry.ActorInTeam)
                    return WorkCommands.Fail(RequestErrorKind.Forbidden,
                        "Only an eligible member of the holding team may claim this work.");
                return await WorkCommands.ExecuteAsync(executor, context, request.TenantId,
                    request.ProgramId, entry, snapshot.Today, ledger => ledger.AssignTo(
                        request.WorkItemId, request.ExpectedRevision, WorkAssignmentLedger.Claim,
                        actor.MemberId, null, null,
                        ActorReference.ForMember(actor.MemberId, actor.Display),
                        clock.GetUtcNow()), ct).ConfigureAwait(false);
            }, ct);
    }
}
