using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     Converts a failed or skipped attestation or a reviewer's requested action into a finding.
///     Dispatched only by the operations reactor; the finding ID is derived from the source.
/// </summary>
[Discriminator("bdgrz.finding.raise_from_occurrence", 1)]
public sealed record RaiseOccurrenceFinding(Uuid TenantId, Uuid ProgramId, Uuid FindingId,
    Uuid ControlId, Uuid OccurrenceId, string SourceVersion, string SourceKind,
    string SourceText)
    : IRequest, IOperationsReactionRequest, ICallable;
