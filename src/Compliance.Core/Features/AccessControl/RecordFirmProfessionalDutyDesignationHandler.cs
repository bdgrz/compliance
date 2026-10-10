using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class RecordFirmProfessionalDutyDesignationHandler(IAggregateExecutor executor,
    IAggregateReader reader, IPlatformUserDirectoryReader users, TimeProvider clock)
    : IRequestHandler<RecordFirmProfessionalDutyDesignation, FirmProfessionalDutyDesignationView>
{
    public async ValueTask<Result<FirmProfessionalDutyDesignationView>> HandleAsync(
        IRequestContext<RecordFirmProfessionalDutyDesignation> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.StaffMemberId == Uuid.Empty)
            return Result<FirmProfessionalDutyDesignationView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "A current canonical firm-staff identity is required before recording a duty designation."));
        var directory = await reader.HydrateAsync(new FirmStaffDirectory(), ct).ConfigureAwait(false);
        if (directory.Get(request.StaffMemberId) is not { IsActive: true } staff)
            return Result<FirmProfessionalDutyDesignationView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The firm-staff identity is missing or inactive; reload before designating a professional duty.", true));
        if (!await users.ExistsAsync(staff.UserId, ct).ConfigureAwait(false))
            return Result<FirmProfessionalDutyDesignationView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The canonical platform user is unavailable; wait for directory recovery."));

        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var operatorId))
            throw new InvalidOperationException("ProfessionalDutyAdministrationAuthorizer must reject this actor.");
        var actor = ActorReference.ForPlatformOperator(operatorId,
            UserIdentityClaims.BdgrzDisplay(context.Actor, operatorId));
        return await executor.ExecuteAsync(new FirmProfessionalDutyCatalog(), catalog =>
            AggregateOutcome.CommitOnSuccess(catalog.Designate(context.RequestId, request.DesignationId,
                staff, request.Duty, request.TenantId, request.SourceReference, request.ExpectedSequence,
                actor, clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}
