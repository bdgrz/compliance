using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>
///     The acting member's in-product reminders: one per assigned item due today or overdue, and,
///     for program managers, one per escalated item. Reminders cannot be opted out of.
/// </summary>
[Discriminator("bdgrz.work.reminders.list", 1)]
public sealed record ListWorkReminders(Uuid TenantId, Uuid ProgramId)
    : IRequest<IReadOnlyList<WorkReminderView>>, IProgramReadRequest, ICallable;
