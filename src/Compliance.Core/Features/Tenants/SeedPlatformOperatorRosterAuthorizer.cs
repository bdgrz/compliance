using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

sealed class SeedPlatformOperatorRosterAuthorizer : IRequestAuthorizer<SeedPlatformOperatorRoster>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<SeedPlatformOperatorRoster> context,
        CancellationToken ct) => ValueTask.FromResult(RequestActor.IsSystem(context.Actor)
        ? Result.Success
        : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
            "Only the system may seed platform operators.")));
}
