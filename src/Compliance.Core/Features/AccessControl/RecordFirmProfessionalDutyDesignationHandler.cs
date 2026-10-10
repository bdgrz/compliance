using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

sealed class RecordFirmProfessionalDutyDesignationHandler(IAggregateExecutor executor,
    IAggregateReader reader, IPlatformUserDirectoryReader users,
    IFirmProfessionalDutyAssertionVerifier assertions, TimeProvider clock)
    : IRequestHandler<RecordFirmProfessionalDutyDesignation, FirmProfessionalDutyDesignationView>
{
    public async ValueTask<Result<FirmProfessionalDutyDesignationView>> HandleAsync(
        IRequestContext<RecordFirmProfessionalDutyDesignation> context, CancellationToken ct)
    {
        var request = context.Request;
        var now = clock.GetUtcNow();
        if (!assertions.TryVerify(request.SignedAssertion, now, out var evidence) ||
            evidence.Status != "active" || evidence.DesignationId != request.DesignationId)
            return Failure(RequestErrorKind.Forbidden,
                "A valid, current signed active-duty assertion from the configured issuer is required.");
        var directory = await reader.HydrateAsync(new FirmStaffDirectory(), ct).ConfigureAwait(false);
        var staffMatches = directory.View().Staff.Where(item => item.UserId == evidence.UserId).ToArray();
        if (staffMatches is not [{ IsActive: true } staff])
            return Failure(RequestErrorKind.Conflict,
                "The firm-staff identity is missing or inactive; reload before designating a professional duty.", true);
        if (!await users.ExistsAsync(staff.UserId, ct).ConfigureAwait(false))
            return Failure(RequestErrorKind.Conflict,
                "The canonical platform user is unavailable; wait for directory recovery.", true);

        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var operatorId))
            throw new InvalidOperationException("ProfessionalDutyAdministrationAuthorizer must reject this actor.");
        var actor = ActorReference.ForPlatformOperator(operatorId,
            UserIdentityClaims.BdgrzDisplay(context.Actor, operatorId));
        return await executor.ExecuteAsync(new FirmProfessionalDutyCatalog(), catalog =>
            AggregateOutcome.CommitOnSuccess(catalog.Designate(context.RequestId, evidence,
                request.SignedAssertion, staff, request.ExpectedSequence, actor, now)), context, ct)
            .ConfigureAwait(false);
    }

    static Result<FirmProfessionalDutyDesignationView> Failure(RequestErrorKind kind, string message,
        bool transient = false) =>
        Result<FirmProfessionalDutyDesignationView>.Failure(new RequestError(kind, message, transient));
}
