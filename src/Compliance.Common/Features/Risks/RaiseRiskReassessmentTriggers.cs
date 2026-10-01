using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     Raises one trigger for each assessed risk of a program affected by a changed method or
///     boundary. Dispatched only by trusted reassessment reactors.
/// </summary>
[Discriminator("bdgrz.risk.reassessment.raise", 1)]
public sealed record RaiseRiskReassessmentTriggers(Uuid TenantId, Uuid ProgramId,
    string TriggerKind, string SourceReference, DateTimeOffset RaisedAt, Uuid? MethodVersionId)
    : IRequest, IRiskReassessmentReactionRequest;
