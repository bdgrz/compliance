using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>Lists the program's Type I entry decisions from the readiness projection.</summary>
public sealed class ListTypeIEntryDecisionsHandler(IReadinessReadModel readModel)
    : IRequestHandler<ListTypeIEntryDecisions, Page<TypeIEntryDecisionView>>
{
    public ValueTask<Result<Page<TypeIEntryDecisionView>>> HandleAsync(
        IRequestContext<ListTypeIEntryDecisions> context, CancellationToken ct)
        => readModel.ListTypeIEntryDecisionsAsync(context.Request, ct);
}
