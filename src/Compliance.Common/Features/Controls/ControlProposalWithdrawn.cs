using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>
///     Withdraws a pending successor draft or retirement proposal at its exact revision. The
///     current approved version stays effective; the <see cref="ControlDraftRevised" /> raised in
///     the same commit restores its content on a new revision so stale decisions cannot apply.
/// </summary>
/// <param name="Kind">successor or retirement.</param>
/// <param name="TargetId">The withdrawn successor version ID or retirement proposal ID.</param>
/// <param name="CurrentVersionId">The approved version that remains current.</param>
[Discriminator("bdgrz.control.proposal.withdrawn", 1)]
public sealed record ControlProposalWithdrawn(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid DecisionId, string Kind, Uuid TargetId, Uuid CurrentVersionId, long Revision,
    string Rationale, Uuid ActorMemberId, ActorReference Actor, DateTimeOffset WithdrawnAt)
    : DomainEvent;
