using Bdgrz.Compliance.Features.Controls;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>Lists advisor feedback on one assessment from one ledger hydration.</summary>
public sealed class ListReadinessAnnotationsHandler(IAggregateReader reader)
    : IRequestHandler<ListReadinessAnnotations, Page<ReadinessAnnotationView>>
{
    public async ValueTask<Result<Page<ReadinessAnnotationView>>> HandleAsync(
        IRequestContext<ListReadinessAnnotations> context, CancellationToken ct)
    {
        var request = context.Request;
        var ledger = await reader.HydrateAsync(new ReadinessLedger(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        if (ledger.Annotations(request.AssessmentId) is not { } annotations)
            return Result<Page<ReadinessAnnotationView>>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The readiness assessment was not found."));
        return ControlActivationSource.Paginate(annotations, request.Limit, request.Cursor,
            "readiness annotations");
    }
}
