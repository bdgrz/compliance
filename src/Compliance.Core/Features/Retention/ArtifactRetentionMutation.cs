using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

sealed class ArtifactRetentionMutation(IAggregateExecutor executor, IAggregateReader reader, TimeProvider clock)
{
    public async ValueTask<Result> ExecuteAsync<T>(IRequestContext<T> context, string kind, Uuid id, string expectedSha,
        Func<ArtifactRetention, ArtifactRetentionSourceSnapshot, ActorReference, DateTimeOffset, Result> apply,
        CancellationToken ct) where T : IArtifactRetentionAdminRequest
    {
        if (context.Invocation is not HttpInvocation)
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "Retention decisions require a personal HTTP invocation."));
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var user) || RequestActor.IsSystem(context.Actor))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "Retention requires a personal Bdgrz user."));
        var source = await ArtifactRetentionSourceReader.LoadAsync(reader, context.Request.TenantId, kind, id, ct)
            .ConfigureAwait(false);
        if (!source.IsSuccess)
            return Result.Failure(source.Error);
        if (source.Value.Source.ContentSha256 != expectedSha)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The retained source content differs from the expected digest."));
        var fence = await ArtifactRetentionSourceReader.CheckAsync(reader, source.Value, ct).ConfigureAwait(false);
        if (!fence.IsSuccess)
            return fence;
        var actor = ActorReference.ForMember(RbacIds.Member(context.Request.TenantId, user),
            UserIdentityClaims.BdgrzDisplay(context.Actor, user));
        return await executor.ExecuteAsync(new ArtifactRetention(context.Request.TenantId, kind, id),
            aggregate => AggregateOutcome.CommitOnSuccess(apply(aggregate, source.Value, actor, clock.GetUtcNow())),
            context, ct).ConfigureAwait(false);
    }
}
