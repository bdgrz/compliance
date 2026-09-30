using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

/// <summary>
/// An immutable effective version. <c>PerformedBy</c> names the party that performs the
/// commitment; CUECs and CSOCs are never internally performed controls.
/// </summary>
public sealed record CommitmentVersionView(Uuid TenantId, Uuid ProgramId, Uuid DraftId,
    Uuid ServiceId, string Kind, string Identifier, long Version, long Revision,
    string Statement, string Context, string SourceReference, string OwnerReference,
    string Applicability, string Interpretation, string? InterpretationNote,
    string PerformedBy, bool InternallyPerformed, DateOnly EffectiveFrom,
    CommitmentDecisionView Decision);
