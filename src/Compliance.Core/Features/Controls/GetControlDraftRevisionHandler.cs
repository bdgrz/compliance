using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

public sealed class GetControlDraftRevisionHandler(IControlDraftDirectoryReader directory,
    ControlDraftReadConsistency consistency)
    : IRequestHandler<GetControlDraftRevision, ControlDraftRevisionView>
{
    public async ValueTask<Result<ControlDraftRevisionView>> HandleAsync(
        IRequestContext<GetControlDraftRevision> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Revision < 1)
            return Result<ControlDraftRevisionView>.Failure(new RequestError(
                RequestErrorKind.Validation, "The control draft revision must be positive."));
        var freshness = await consistency.GetAsync(request.TenantId, request.ProgramId,
            request.ControlId, request.Revision, ct).ConfigureAwait(false);
        if (!freshness.IsSuccess)
            return Result<ControlDraftRevisionView>.Failure(freshness.Error);
        var revision = await directory.GetRevisionAsync(request.TenantId, request.ControlId,
            request.Revision, ct).ConfigureAwait(false);
        return revision is not null && revision.TenantId == request.TenantId &&
               revision.ProgramId == request.ProgramId && revision.ControlId == request.ControlId
            ? Result<ControlDraftRevisionView>.Success(revision)
            : Result<ControlDraftRevisionView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The control draft revision projection is incomplete.", isTransient: true));
    }
}
