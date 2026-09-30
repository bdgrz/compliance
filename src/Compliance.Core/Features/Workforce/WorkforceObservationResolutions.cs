using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>Attaches attributed closures to observations after checking the closure projection.</summary>
public sealed class WorkforceObservationResolutions(IWorkforceObservationResolutionReader directory,
    IDomainEventReader events)
{
    public async ValueTask<Result<IReadOnlyDictionary<Uuid, WorkforceObservationResolutionView>>>
        LoadAsync(Uuid tenantId, IReadOnlyCollection<Uuid> observationIds, CancellationToken ct)
    {
        var checkpoint = await directory.LoadCheckpointAsync(tenantId, ct).ConfigureAwait(false);
        await using (var pending = events.ReadAsync(
                         EventStreamPattern.ForPattern(tenantId.ToString(),
                             "workforce-observation-resolutions"),
                         checkpoint.Cursor, ct).GetAsyncEnumerator(ct))
        {
            if (await pending.MoveNextAsync().ConfigureAwait(false))
                return Result<IReadOnlyDictionary<Uuid, WorkforceObservationResolutionView>>.Failure(
                    new RequestError(RequestErrorKind.Conflict,
                        "The workforce observation resolutions have not reached the source.",
                        isTransient: true));
        }
        return Result<IReadOnlyDictionary<Uuid, WorkforceObservationResolutionView>>.Success(
            await directory.GetManyAsync(tenantId, observationIds, ct).ConfigureAwait(false));
    }
}
