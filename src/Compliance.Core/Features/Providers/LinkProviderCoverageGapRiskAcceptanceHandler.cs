using Bdgrz.Compliance.Features.Risks;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>Links a still-active, existing R1-07 decision; it never changes gap state.</summary>
public sealed class LinkProviderCoverageGapRiskAcceptanceHandler(
    IAggregateExecutor executor, IAggregateReader reader,
    ProviderReadConsistency providerConsistency, TimeProvider clock)
    : IRequestHandler<LinkProviderCoverageGapRiskAcceptance, ProviderCoverageGapRegistration>
{
    public async ValueTask<Result<ProviderCoverageGapRegistration>> HandleAsync(
        IRequestContext<LinkProviderCoverageGapRiskAcceptance> context, CancellationToken ct)
    {
        var request = context.Request;
        var source = await reader.HydrateAsync(new ProviderAssuranceRegister(request.TenantId), ct)
            .ConfigureAwait(false);
        var content = new ProviderCoverageGapRiskAcceptanceContent(request.ProgramId,
            request.RiskId, request.AcceptanceId);
        if (source.CheckCoverageGapRiskAcceptanceRetry(request.GapId, context.RequestId,
                request.ExpectedRevision, content) is { } retry)
            return retry;
        if (ProviderCoverageGapRules.RiskAcceptanceInputError(content) is { } inputError)
            return Result<ProviderCoverageGapRegistration>.Failure(new RequestError(
                RequestErrorKind.Validation, inputError));
        if (source.CoverageGap(request.GapId) is not { } gap ||
            gap.TenantId != request.TenantId || gap.ProviderId != request.ProviderId)
            return Result<ProviderCoverageGapRegistration>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The provider coverage gap was not found."));

        var provider = await providerConsistency.GetRevisionAsync(request.TenantId,
            request.ProviderId, gap.ProviderRevision, ct).ConfigureAwait(false);
        if (!provider.IsSuccess)
            return Result<ProviderCoverageGapRegistration>.Failure(provider.Error);
        if (AssuranceReferences.ServiceError(provider.Value, gap.Content.ServiceId,
                request.ProgramId) is { } serviceError)
            return Result<ProviderCoverageGapRegistration>.Failure(serviceError);

        var draft = await reader.HydrateAsync(new RiskDraft(request.TenantId, request.RiskId), ct)
            .ConfigureAwait(false);
        if (!draft.IsCreated || draft.ProgramId != request.ProgramId)
            return Result<ProviderCoverageGapRegistration>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The risk was not found in this program."));
        var risk = await reader.HydrateAsync(new RiskEvaluation(request.TenantId, request.RiskId), ct)
            .ConfigureAwait(false);
        var acceptance = risk.ProgramId == request.ProgramId
            ? risk.FindAcceptance(request.AcceptanceId)
            : null;
        var now = clock.GetUtcNow();
        if (acceptance is null || acceptance.AcceptedAt > now || acceptance.ExpiresAt <= now)
            return Result<ProviderCoverageGapRegistration>.Failure(new RequestError(
                RequestErrorKind.Conflict,
                "The risk acceptance is missing, expired, or not active."));

        var actor = ProviderActor.From(context);
        return await executor.ExecuteAsync(new ProviderAssuranceRegister(request.TenantId), register =>
            AggregateOutcome.CommitOnSuccess(register.LinkCoverageGapRiskAcceptance(
                request.GapId, request.ProviderId, context.RequestId, request.ExpectedRevision,
                content, acceptance.ExpiresAt, actor, now)), context, ct).ConfigureAwait(false);
    }
}
