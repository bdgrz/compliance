using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

sealed class PlaceArtifactLegalHoldHandler(ArtifactRetentionMutation mutation) : IRequestHandler<PlaceArtifactLegalHold>
{
    public ValueTask<Result> HandleAsync(IRequestContext<PlaceArtifactLegalHold> context, CancellationToken ct)
    {
        var request = context.Request;
        return mutation.ExecuteAsync(context, request.SourceKind, request.SourceId, request.ExpectedContentSha256,
            (retention, source, actor, at) => retention.PlaceLegalHold(source, request.ExpectedRevision, request.HoldId, request.Reason, actor, at), ct);
    }
}
