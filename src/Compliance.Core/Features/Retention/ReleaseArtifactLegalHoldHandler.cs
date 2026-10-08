using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

sealed class ReleaseArtifactLegalHoldHandler(ArtifactRetentionMutation mutation) : IRequestHandler<ReleaseArtifactLegalHold>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReleaseArtifactLegalHold> context, CancellationToken ct)
    {
        var request = context.Request;
        return mutation.ExecuteAsync(context, request.SourceKind, request.SourceId, request.ExpectedContentSha256,
            (retention, source, actor, at) => retention.ReleaseLegalHold(source, request.ExpectedRevision, request.HoldId, request.Reason, actor, at), ct);
    }
}
