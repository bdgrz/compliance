using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed class GetProgramRevisionHandler(IProgramDirectoryReader directory,
    ProgramHistoryReadConsistency consistency)
    : IRequestHandler<GetProgramRevision, ProgramRevisionView>
{
    public async ValueTask<Result<ProgramRevisionView>> HandleAsync(
        IRequestContext<GetProgramRevision> context, CancellationToken ct)
    {
        var request = context.Request;
        var freshness = await consistency.EnsureAsync(request.TenantId, request.ProgramId,
            request.Revision, ct).ConfigureAwait(false);
        if (!freshness.IsSuccess)
            return Result<ProgramRevisionView>.Failure(freshness.Error);
        var revision = await directory.GetRevisionAsync(request.TenantId, request.ProgramId,
            request.Revision, ct).ConfigureAwait(false);
        return revision is null
            ? Result<ProgramRevisionView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The program revision was not found."))
            : Result<ProgramRevisionView>.Success(revision);
    }
}
