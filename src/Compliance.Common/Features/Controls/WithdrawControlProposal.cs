using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>
///     Withdraws the pending successor draft or retirement proposal at its exact revision. The
///     current approved version and every recorded decision are retained.
/// </summary>
[Discriminator("bdgrz.control.proposal.withdraw", 1)]
public sealed record WithdrawControlProposal(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    long ExpectedRevision, string Rationale) : IRequest, IProgramScopedRequest, ICallable;
