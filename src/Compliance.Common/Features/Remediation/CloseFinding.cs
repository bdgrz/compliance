using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     The reviewer's personal verification and closure decision; HTTP-only. Closure requires
///     completed corrective work and resolution evidence, and the finding or action owners
///     cannot close it without an approved waiver.
/// </summary>
[Discriminator("bdgrz.finding.close", 1)]
public sealed record CloseFinding(Uuid TenantId, Uuid ProgramId, Uuid FindingId,
    long ExpectedRevision, string VerificationRationale,
    IReadOnlyList<EvidenceReference> ResolutionEvidence, string Rationale,
    Uuid? SeparationOfDutiesWaiverId = null)
    : IRequest<FindingView>, IProgramScopedRequest, ICallable;
