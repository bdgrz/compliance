using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>
///     Derives in-product reminders from the actor's current queue at read time: a due reminder on
///     the due date, an overdue reminder once overdue, and an escalated reminder for program
///     managers. Each reminder's identity is stable, so reads never duplicate one, and completed
///     source work leaves no reminder behind.
/// </summary>
public sealed class ListWorkRemindersHandler(WorkQueueReader queue)
    : IRequestHandler<ListWorkReminders, IReadOnlyList<WorkReminderView>>
{
    public async ValueTask<Result<IReadOnlyList<WorkReminderView>>> HandleAsync(
        IRequestContext<ListWorkReminders> context, CancellationToken ct)
    {
        var request = context.Request;
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var read = await queue.ReadAsync(request.TenantId, request.ProgramId, actor,
            WorkQueueReader.DefaultHorizonDays, ct).ConfigureAwait(false);
        if (!read.IsSuccess)
            return Result<IReadOnlyList<WorkReminderView>>.Failure(read.Error);
        var snapshot = read.Value;
        var today = snapshot.Today;
        var reminders = new List<WorkReminderView>();
        foreach (var item in snapshot.Entries.Select(static entry => entry.Item))
        {
            if (item.DueOn is not { } due)
                continue;
            var daysOverdue = Math.Max(0, today.DayNumber - due.DayNumber);
            if (item.AssigneeMemberId == actor.MemberId && due <= today)
                reminders.Add(Reminder(due < today ? "overdue" : "due", item, due, daysOverdue));
            else if (snapshot.ActorManages && item.Escalated)
                reminders.Add(Reminder("escalated", item, due, daysOverdue));
        }
        return Result<IReadOnlyList<WorkReminderView>>.Success(reminders);
    }

    static WorkReminderView Reminder(string kind, WorkQueueItemView item, DateOnly due,
        int daysOverdue) => new(Uuid.CreateVersion5(item.WorkItemId,
            $"reminder:{kind}:{due.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}"), kind,
        item.WorkItemId, item.Summary, due, daysOverdue, item.ActionPath);
}
