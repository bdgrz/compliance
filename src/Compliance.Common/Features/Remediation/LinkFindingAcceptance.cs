using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     Links an approved risk acceptance (kind risk_acceptance with the risk ID and acceptance ID)
///     or an approved waiver (kind waiver with the waiver ID). It never closes the finding.
/// </summary>
[Discriminator("bdgrz.finding.acceptance.link", 1)]
public sealed record LinkFindingAcceptance(Uuid TenantId, Uuid ProgramId, Uuid FindingId,
    long ExpectedRevision, string Kind, Uuid RecordId, Uuid? DecisionId = null)
    : IRequest<FindingView>, IProgramScopedRequest, ICallable;
