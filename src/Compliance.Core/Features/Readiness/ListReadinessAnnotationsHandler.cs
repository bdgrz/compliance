using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>Lists advisor feedback on one assessment from the readiness projection.</summary>
public sealed class ListReadinessAnnotationsHandler(IReadinessReadModel readModel)
    : IRequestHandler<ListReadinessAnnotations, Page<ReadinessAnnotationView>>
{
    public ValueTask<Result<Page<ReadinessAnnotationView>>> HandleAsync(
        IRequestContext<ListReadinessAnnotations> context, CancellationToken ct)
        => readModel.ListAnnotationsAsync(context.Request, ct);
}
