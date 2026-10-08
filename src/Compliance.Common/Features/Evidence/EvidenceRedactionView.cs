using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>Retained lineage and current approval availability; it never authorizes disclosure.</summary>
public sealed record EvidenceRedactionView(Uuid TenantId, Uuid RedactionId, long Revision,
    IReadOnlyList<EvidenceRedactionPreparationView> Preparations,
    IReadOnlyList<EvidenceRedactionApprovalView> Approvals,
    string OriginalState, string DerivedState, bool ApprovalCurrent);
