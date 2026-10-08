using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

sealed class RecordArtifactRetentionBasisHandler(ArtifactRetentionMutation mutation) : IRequestHandler<RecordArtifactRetentionBasis>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RecordArtifactRetentionBasis> context, CancellationToken ct)
    {
        var request = context.Request;
        return mutation.ExecuteAsync(context, request.SourceKind, request.SourceId, request.ExpectedContentSha256,
            (retention, source, actor, at) => retention.RecordBasis(source, request.ExpectedRevision, request.PeriodStart, request.PeriodEnd, request.Reason, actor, at), ct);
    }
}
