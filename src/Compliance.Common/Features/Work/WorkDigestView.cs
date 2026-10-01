using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>
///     One member's digest for the week starting <c>WeekOf</c> (Monday). The identifier is stable for
///     the member and week. <c>EmailDigestEnabled</c> reflects the member's email preference only.
/// </summary>
public sealed record WorkDigestView(Uuid DigestId, DateOnly WeekOf, bool EmailDigestEnabled,
    IReadOnlyList<WorkQueueItemView> Overdue, IReadOnlyList<WorkQueueItemView> DueSoon);
