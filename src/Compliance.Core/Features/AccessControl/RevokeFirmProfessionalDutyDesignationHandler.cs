using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

sealed class RevokeFirmProfessionalDutyDesignationHandler(IAggregateExecutor executor,
    IFirmProfessionalDutyAssertionVerifier assertions, TimeProvider clock)
    : IRequestHandler<RevokeFirmProfessionalDutyDesignation, FirmProfessionalDutyDesignationView>
{
    public ValueTask<Result<FirmProfessionalDutyDesignationView>> HandleAsync(
        IRequestContext<RevokeFirmProfessionalDutyDesignation> context, CancellationToken ct)
    {
        var request = context.Request;
        var now = clock.GetUtcNow();
        if (!assertions.TryVerify(request.SignedAssertion, now, out var evidence) ||
            evidence.Status != "revoked" || evidence.DesignationId != request.DesignationId)
            return ValueTask.FromResult(Failure(RequestErrorKind.Forbidden,
                "A valid, current signed revocation assertion from the configured issuer is required."));
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var operatorId))
            throw new InvalidOperationException("ProfessionalDutyAdministrationAuthorizer must reject this actor.");
        var actor = ActorReference.ForPlatformOperator(operatorId,
            UserIdentityClaims.BdgrzDisplay(context.Actor, operatorId));
        return executor.ExecuteAsync(new FirmProfessionalDutyCatalog(), catalog =>
            AggregateOutcome.CommitOnSuccess(catalog.Revoke(context.RequestId, evidence,
                request.SignedAssertion, request.ExpectedSequence, actor, now)), context, ct);
    }

    static Result<FirmProfessionalDutyDesignationView> Failure(RequestErrorKind kind, string message) =>
        Result<FirmProfessionalDutyDesignationView>.Failure(new RequestError(kind, message));
}
