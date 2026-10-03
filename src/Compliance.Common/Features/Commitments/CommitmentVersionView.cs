using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

/// <summary>
/// An immutable effective version. <c>PerformedBy</c> names the party that performs the
/// commitment; CUECs and CSOCs are never internally performed controls. <c>Decision</c> is the
/// accepted review and <c>Approval</c> the separate approval; legacy versions have no approval
/// and an unverified source.
/// </summary>
public sealed record CommitmentVersionView(Uuid TenantId, Uuid ProgramId, Uuid DraftId,
    Uuid ServiceId, string Kind, string Identifier, long Version, long Revision,
    string Statement, string Context, string SourceReference, string OwnerReference,
    string Applicability, string Interpretation, string? InterpretationNote,
    string PerformedBy, bool InternallyPerformed, DateOnly EffectiveFrom,
    CommitmentDecisionView Decision, string SourceResolution = "unverified",
    string? SourceEvidence = null, CommitmentDecisionView? Approval = null)
{
    /// <summary>The subservice provider responsible for this CSOC, when recorded.</summary>
    public Uuid? ProviderId { get; init; }
}
