using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Frozen selection method, population reference, selected rows, and manifest.</summary>
public sealed record SampleSelectionView(Uuid TenantId, Uuid ProgramId, Uuid SampleSelectionId, string Status, long Revision, DateTimeOffset UpdatedAt);
