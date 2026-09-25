using Bdgrz.Compliance.Features.Controls;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Keeps the current indexed entries for one Control draft without tenant-wide scans.</summary>
sealed record ControlDraftReferenceState(Uuid TenantId, Uuid ControlId, Uuid ProgramId,
    string Identifier, long Revision, IReadOnlyList<Uuid> EntryIds);
