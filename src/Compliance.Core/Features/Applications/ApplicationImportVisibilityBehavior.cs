using Bdgrz.Compliance.Features.AccessReviews;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Fences application inventory queries across committed import projection changes.</summary>
sealed class ApplicationImportVisibilityBehavior(ApplicationImportVisibilityReadConsistency consistency)
    : IRequestPipelineBehavior<GetApplication, ApplicationView>,
      IRequestPipelineBehavior<ListApplications, Page<ApplicationView>>,
      IRequestPipelineBehavior<GetApplicationRevision, ApplicationRevisionView>,
      IRequestPipelineBehavior<ListApplicationRevisions, Page<ApplicationRevisionView>>,
      IRequestPipelineBehavior<GetSystemInstance, SystemInstanceView>,
      IRequestPipelineBehavior<ListSystemInstances, Page<SystemInstanceView>>,
      IRequestPipelineBehavior<PreviewApplicationChange, ApplicationChangePreview>,
      IRequestPipelineBehavior<ListApplicationBoundaryReferences, Page<ApplicationBoundaryReferenceView>>,
      IRequestPipelineBehavior<ListSystemInstanceBoundaryReferences, Page<ApplicationBoundaryReferenceView>>,
      IRequestPipelineBehavior<GetAccessReviewScope, AccessReviewScopeView>,
      IRequestPipelineBehavior<ListAccessReviewScopes, Page<AccessReviewScopeStatusView>>,
      IRequestPipelineBehavior<ListAccessExpectations, AccessExpectationsView>,
      IRequestPipelineBehavior<ListAccessPopulations, Page<AccessPopulationSummaryView>>,
      IRequestPipelineBehavior<GetAccessReviewCoverage, AccessReviewCoverageView>
{
    public ValueTask<Result<ApplicationView>> HandleAsync(IRequestContext<GetApplication> context,
        RequestPipelineNext<ApplicationView> continuation, CancellationToken ct) =>
        ReadAsync(context.Request.TenantId, continuation, ct);

    public ValueTask<Result<Page<ApplicationView>>> HandleAsync(IRequestContext<ListApplications> context,
        RequestPipelineNext<Page<ApplicationView>> continuation, CancellationToken ct) =>
        ReadAsync(context.Request.TenantId, continuation, ct);

    public ValueTask<Result<ApplicationRevisionView>> HandleAsync(IRequestContext<GetApplicationRevision> context,
        RequestPipelineNext<ApplicationRevisionView> continuation, CancellationToken ct) =>
        ReadAsync(context.Request.TenantId, continuation, ct);

    public ValueTask<Result<Page<ApplicationRevisionView>>> HandleAsync(IRequestContext<ListApplicationRevisions> context,
        RequestPipelineNext<Page<ApplicationRevisionView>> continuation, CancellationToken ct) =>
        ReadAsync(context.Request.TenantId, continuation, ct);

    public ValueTask<Result<SystemInstanceView>> HandleAsync(IRequestContext<GetSystemInstance> context,
        RequestPipelineNext<SystemInstanceView> continuation, CancellationToken ct) =>
        ReadAsync(context.Request.TenantId, continuation, ct);

    public ValueTask<Result<Page<SystemInstanceView>>> HandleAsync(IRequestContext<ListSystemInstances> context,
        RequestPipelineNext<Page<SystemInstanceView>> continuation, CancellationToken ct) =>
        ReadAsync(context.Request.TenantId, continuation, ct);

    public ValueTask<Result<ApplicationChangePreview>> HandleAsync(IRequestContext<PreviewApplicationChange> context,
        RequestPipelineNext<ApplicationChangePreview> continuation, CancellationToken ct) =>
        ReadAsync(context.Request.TenantId, continuation, ct);

    public ValueTask<Result<Page<ApplicationBoundaryReferenceView>>> HandleAsync(IRequestContext<ListApplicationBoundaryReferences> context,
        RequestPipelineNext<Page<ApplicationBoundaryReferenceView>> continuation, CancellationToken ct) =>
        ReadAsync(context.Request.TenantId, continuation, ct);

    public ValueTask<Result<Page<ApplicationBoundaryReferenceView>>> HandleAsync(IRequestContext<ListSystemInstanceBoundaryReferences> context,
        RequestPipelineNext<Page<ApplicationBoundaryReferenceView>> continuation, CancellationToken ct) =>
        ReadAsync(context.Request.TenantId, continuation, ct);

    public ValueTask<Result<AccessReviewScopeView>> HandleAsync(IRequestContext<GetAccessReviewScope> context,
        RequestPipelineNext<AccessReviewScopeView> continuation, CancellationToken ct) =>
        ReadAsync(context.Request.TenantId, continuation, ct);

    public ValueTask<Result<Page<AccessReviewScopeStatusView>>> HandleAsync(IRequestContext<ListAccessReviewScopes> context,
        RequestPipelineNext<Page<AccessReviewScopeStatusView>> continuation, CancellationToken ct) =>
        ReadAsync(context.Request.TenantId, continuation, ct);

    public ValueTask<Result<AccessExpectationsView>> HandleAsync(IRequestContext<ListAccessExpectations> context,
        RequestPipelineNext<AccessExpectationsView> continuation, CancellationToken ct) =>
        ReadAsync(context.Request.TenantId, continuation, ct);

    public ValueTask<Result<Page<AccessPopulationSummaryView>>> HandleAsync(IRequestContext<ListAccessPopulations> context,
        RequestPipelineNext<Page<AccessPopulationSummaryView>> continuation, CancellationToken ct) =>
        ReadAsync(context.Request.TenantId, continuation, ct);

    public ValueTask<Result<AccessReviewCoverageView>> HandleAsync(IRequestContext<GetAccessReviewCoverage> context,
        RequestPipelineNext<AccessReviewCoverageView> continuation, CancellationToken ct) =>
        ReadAsync(context.Request.TenantId, continuation, ct);

    async ValueTask<Result<TOut>> ReadAsync<TOut>(Uuid tenantId,
        RequestPipelineNext<TOut> continuation, CancellationToken ct)
    {
        var fence = await consistency.CaptureAsync(tenantId, ct).ConfigureAwait(false);
        if (!fence.IsSuccess)
            return Result<TOut>.Failure(fence.Error);
        Result<TOut> result;
        try
        {
            result = await continuation(ct).ConfigureAwait(false);
        }
        catch (ApplicationImportVisibilityChangedException)
        {
            return Result<TOut>.Failure(new RequestError(RequestErrorKind.Conflict,
                "Application import visibility changed; restart paging.", isTransient: true));
        }
        var confirmed = await consistency.ConfirmAsync(tenantId, fence.Value, ct).ConfigureAwait(false);
        return confirmed.IsSuccess ? result : Result<TOut>.Failure(confirmed.Error);
    }
}
