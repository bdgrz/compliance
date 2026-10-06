using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

sealed class ApplyApplicationImportEffectAuthorizer(IAggregateReader reader)
    : IRequestAuthorizer<ApplyApplicationImportEffect>
{
    public async ValueTask<Result> AuthorizeAsync(IRequestContext<ApplyApplicationImportEffect> context,
        CancellationToken ct)
    {
        if (!RequestActor.IsSystem(context.Actor))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only Portia's trusted system actor may write a pending import effect."));
        var authority = await ApplicationImportEffectAuthority.LoadAsync(reader, context.Request, ct)
            .ConfigureAwait(false);
        return authority.IsSuccess ? Result.Success : Result.Failure(authority.Error);
    }
}
