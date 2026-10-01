using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

/// <summary>The projected list row of a policy; review overdue is evaluated when read.</summary>
public sealed record PolicySummaryView(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    string Identifier, string Title, string Status, string? PendingStatus, long Revision,
    long? CurrentVersion, DateOnly? CurrentEffectiveFrom, DateOnly? NextReviewDueOn,
    bool ReviewOverdue, DateTimeOffset LastChangedAt);
