using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

public sealed class GetRiskDraftRevisionHandler(IRiskDraftDirectoryReader directory,
    RiskDraftReadConsistency consistency)
    : IRequestHandler<GetRiskDraftRevision, RiskDraftRevisionView>
{
    public async ValueTask<Result<RiskDraftRevisionView>> HandleAsync(
        IRequestContext<GetRiskDraftRevision> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Revision < 1)
            return Result<RiskDraftRevisionView>.Failure(new RequestError(
                RequestErrorKind.Validation, "The risk draft revision must be positive."));
        var freshness = await consistency.GetAsync(request.TenantId, request.ProgramId,
            request.RiskId, request.Revision, ct).ConfigureAwait(false);
        if (!freshness.IsSuccess)
            return Result<RiskDraftRevisionView>.Failure(freshness.Error);
        var revision = await directory.GetRevisionAsync(request.TenantId, request.RiskId,
            request.Revision, ct).ConfigureAwait(false);
        return revision is not null && revision.TenantId == request.TenantId &&
               revision.ProgramId == request.ProgramId && revision.RiskId == request.RiskId
            ? Result<RiskDraftRevisionView>.Success(revision)
            : Result<RiskDraftRevisionView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The risk draft revision projection is incomplete.", isTransient: true));
    }
}
