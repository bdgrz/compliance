using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class RevokeFirmProfessionalDutyDesignationHandler(IAggregateExecutor executor,
    TimeProvider clock) : IRequestHandler<RevokeFirmProfessionalDutyDesignation, FirmProfessionalDutyDesignationView>
{
    public ValueTask<Result<FirmProfessionalDutyDesignationView>> HandleAsync(
        IRequestContext<RevokeFirmProfessionalDutyDesignation> context, CancellationToken ct)
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var operatorId))
            throw new InvalidOperationException("ProfessionalDutyAdministrationAuthorizer must reject this actor.");
        var actor = ActorReference.ForPlatformOperator(operatorId,
            UserIdentityClaims.BdgrzDisplay(context.Actor, operatorId));
        return executor.ExecuteAsync(new FirmProfessionalDutyCatalog(), catalog =>
            AggregateOutcome.CommitOnSuccess(catalog.Revoke(context.RequestId, context.Request.DesignationId,
                context.Request.ExpectedSequence, context.Request.Reason, actor, clock.GetUtcNow())), context, ct);
    }
}
