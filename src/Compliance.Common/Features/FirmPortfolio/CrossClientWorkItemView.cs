using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.FirmPortfolio;

/// <summary>The current actor's eligible work item, with its owning client scope explicit.</summary>
public sealed record CrossClientWorkItemView(Uuid TenantId, Uuid ClientTenantId,
    Uuid ProgramId, Uuid WorkItemId, string Title, string Status, DateTimeOffset? DueAt,
    string SourceKind);
