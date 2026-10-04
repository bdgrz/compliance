using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>
///     The actor's weekly digest content for this week: assigned items that are overdue or due in
///     the next seven days, read from current source state. Email delivery is not part of R2-11c's
///     in-product scope; the email preference is reported so a later sender can honor it.
/// </summary>
public sealed class GetWorkDigestHandler(WorkQueueReader queue, IAggregateReader reader)
    : IRequestHandler<GetWorkDigest, WorkDigestView>
{
    const int DueSoonDays = 7;

    public async ValueTask<Result<WorkDigestView>> HandleAsync(IRequestContext<GetWorkDigest> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var read = await queue.ReadAsync(request.TenantId, request.ProgramId, actor,
            DueSoonDays, ct).ConfigureAwait(false);
        if (!read.IsSuccess)
            return Result<WorkDigestView>.Failure(read.Error);
        var snapshot = read.Value;
        var today = snapshot.Today;
        var mine = snapshot.Entries.Select(static entry => entry.Item)
            .Where(item => item.AssigneeMemberId == actor.MemberId && item.DueOn is not null)
            .ToArray();
        var weekOf = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var preference = await reader.HydrateAsync(new WorkDigestPreference(request.TenantId,
            actor.MemberId), ct).ConfigureAwait(false);
        return Result<WorkDigestView>.Success(new WorkDigestView(
            Uuid.CreateVersion5(actor.MemberId, string.Create(CultureInfo.InvariantCulture,
                $"digest:{request.ProgramId}:{weekOf:yyyy-MM-dd}")),
            weekOf, preference.Read().EmailDigestEnabled,
            mine.Where(item => item.DueOn < today).ToArray(),
            mine.Where(item => item.DueOn >= today && item.DueOn <= today.AddDays(DueSoonDays))
                .ToArray()));
    }
}
