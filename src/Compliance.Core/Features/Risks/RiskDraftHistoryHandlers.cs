using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

public sealed class RiskDraftHistoryReadConsistency(IRiskDraftHistoryDirectoryReader directory,
    IAggregateReader reader)
{
    public async ValueTask<Result> EnsureAsync(Uuid tenantId, Uuid programId, Uuid riskId,
        long? minimumRiskRevision, CancellationToken ct)
    {
        if (minimumRiskRevision is < 1)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum risk draft revision must be positive."));
        var source = await reader.HydrateAsync(new RiskDraft(tenantId, riskId), ct)
            .ConfigureAwait(false);
        if (!source.IsCreated || source.ProgramId != programId)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The risk draft was not found."));
        if (minimumRiskRevision is { } minimum && source.Revision < minimum)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The risk draft source has not reached revision {minimum}.", isTransient: true));
        var current = await directory.GetRevisionAsync(tenantId, riskId, source.Revision, ct)
            .ConfigureAwait(false);
        return current is not null && current.TenantId == tenantId &&
               current.ProgramId == programId && current.RiskId == riskId &&
               current.Revision == source.Revision
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The risk draft history projection has not reached the current source revision.",
                isTransient: true));
    }
}

public sealed class ListRiskDraftRevisionsHandler(IRiskDraftHistoryDirectoryReader directory,
    RiskDraftHistoryReadConsistency consistency)
    : IRequestHandler<ListRiskDraftRevisions, Page<RiskDraftRevisionView>>
{
    public async ValueTask<Result<Page<RiskDraftRevisionView>>> HandleAsync(
        IRequestContext<ListRiskDraftRevisions> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<RiskDraftRevisionView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The risk draft revision list limit must be between 1 and 200."));
        var freshness = await consistency.EnsureAsync(request.TenantId, request.ProgramId,
            request.RiskId, request.MinimumRiskRevision, ct).ConfigureAwait(false);
        if (!freshness.IsSuccess)
            return Result<Page<RiskDraftRevisionView>>.Failure(freshness.Error);
        Page<RiskDraftRevisionView> page;
        try
        {
            page = await directory.ListRevisionsAsync(request.TenantId, request.RiskId,
                request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<RiskDraftRevisionView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The risk draft revision cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId ||
                                      item.ProgramId != request.ProgramId ||
                                      item.RiskId != request.RiskId)
            ? Result<Page<RiskDraftRevisionView>>.Failure(new RequestError(
                RequestErrorKind.Conflict,
                "The risk draft history projection has an invalid scope."))
            : Result<Page<RiskDraftRevisionView>>.Success(page);
    }
}
