using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>The person-owner designation of the exact open draft version and revision.</summary>
public sealed record ControlDraftOwnerPersonView(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid? DraftVersionId, long Revision, ControlPersonOwnerDesignationView? Designation);
