using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>Reads one exact decision from the program ledger, its authoritative source.</summary>
public sealed class GetCriterionApplicabilityHandler(IAggregateReader reader)
    : IRequestHandler<GetCriterionApplicability, CriterionApplicabilityView>
{
    public async ValueTask<Result<CriterionApplicabilityView>> HandleAsync(
        IRequestContext<GetCriterionApplicability> context, CancellationToken ct)
    {
        var request = context.Request;
        var ledger = await reader.HydrateAsync(new CriterionApplicabilityLedger(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        return ledger.Read(request.DecisionId) is { } decision
            ? Result<CriterionApplicabilityView>.Success(decision)
            : Result<CriterionApplicabilityView>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The criterion applicability decision was not found."));
    }
}
