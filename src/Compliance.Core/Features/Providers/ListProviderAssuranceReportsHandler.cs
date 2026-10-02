using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

public sealed class ListProviderAssuranceReportsHandler(IAssuranceReader directory, AssuranceReferences references,
    AssuranceReadConsistency consistency, AssuranceDisclosure disclosure)
    : IRequestHandler<ListProviderAssuranceReports, Page<AssuranceReportView>>
{
    public async ValueTask<Result<Page<AssuranceReportView>>> HandleAsync(
        IRequestContext<ListProviderAssuranceReports> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<AssuranceReportView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The assurance report list limit must be between 1 and 200."));
        var provider = await references.ProviderAsync(request.TenantId, request.ProviderId, ct).ConfigureAwait(false);
        if (!provider.IsSuccess)
            return Result<Page<AssuranceReportView>>.Failure(provider.Error);
        var freshness = await consistency.EnsureCaughtUpAsync(request.TenantId, ct).ConfigureAwait(false);
        if (!freshness.IsSuccess)
            return Result<Page<AssuranceReportView>>.Failure(freshness.Error);
        Page<AssuranceReportView> page;
        try
        {
            page = await directory.ListReportsAsync(request.TenantId, request.ProviderId, request.Limit ?? 50,
                request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<AssuranceReportView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The assurance report cursor is invalid."));
        }
        if (page.Items.Any(item => item.TenantId != request.TenantId || item.ProviderId != request.ProviderId))
            return Result<Page<AssuranceReportView>>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The assurance projection is incomplete."));
        if (await disclosure.CanReadRestrictedAsync(context, ct).ConfigureAwait(false))
            return Result<Page<AssuranceReportView>>.Success(page);
        return Result<Page<AssuranceReportView>>.Success(page with
        {
            Items = page.Items.Select(static item => AssuranceRules.IsRestricted(item.Content.Citation)
                ? AssuranceRules.Redact(item) : item).ToArray(),
        });
    }
}
