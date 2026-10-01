using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     Links an existing approved acceptance owned by another workflow: an R1-07 risk acceptance
///     in this program or an approved separation-of-duties waiver. Its expiry is copied so the
///     finding returns to an actionable state when the acceptance lapses.
/// </summary>
public sealed class LinkFindingAcceptanceHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<LinkFindingAcceptance, FindingView>
{
    public async ValueTask<Result<FindingView>> HandleAsync(
        IRequestContext<LinkFindingAcceptance> context, CancellationToken ct)
    {
        var request = context.Request;
        DateTimeOffset? expiresAt = null;
        if (request.Kind == RemediationLedger.RiskAcceptance && request.DecisionId is { } decision)
        {
            var risk = await reader.HydrateAsync(new RiskEvaluation(request.TenantId,
                request.RecordId), ct).ConfigureAwait(false);
            if (risk.ProgramId == request.ProgramId && risk.FindAcceptance(decision) is { } accepted)
                expiresAt = accepted.ExpiresAt;
        }
        else if (request.Kind == RemediationLedger.Waiver && request.DecisionId is null)
        {
            var waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId,
                request.RecordId), ct).ConfigureAwait(false);
            if (waiver.IsRecorded && waiver.ApprovedAt is not null)
                expiresAt = waiver.ExpiresAt;
        }
        else
            return Result<FindingView>.Failure(new RequestError(RequestErrorKind.Validation,
                "Link a risk_acceptance with its risk and acceptance IDs, or an approved waiver by its ID."));
        if (expiresAt is not { } expiry)
            return Result<FindingView>.Failure(new RequestError(RequestErrorKind.Validation,
                "The acceptance was not found or is not approved in this program."));
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var now = clock.GetUtcNow();
        return await RemediationCommands.ExecuteAsync(executor, context, request.TenantId,
            request.ProgramId, request.FindingId, now, ledger => ledger.LinkAcceptance(
                request.FindingId, request.ExpectedRevision, request.Kind, request.RecordId,
                request.DecisionId, expiry, ActorReference.ForMember(actor.MemberId,
                    actor.Display), now), ct).ConfigureAwait(false);
    }
}
