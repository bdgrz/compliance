using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

public sealed class ReviseControlDraftHandler(IAggregateExecutor executor,
    IAggregateReader reader, IControlApplicabilityReferenceValidator applicability,
    TimeProvider clock)
    : IRequestHandler<ReviseControlDraft>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ReviseControlDraft> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var current = await reader.HydrateAsync(new ControlDraft(request.TenantId,
            request.ControlId), ct).ConfigureAwait(false);
        if (!current.IsCreated || !current.IsVisible || current.ProgramId != request.ProgramId)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The control draft was not found."));
        if (request.ExpectedRevision != current.Revision)
            return Result.Failure(VersionedRecordRules.StaleRevision("control draft",
                current.Revision).ToRequestError());
        var structuralError = ControlDraft.ValidateContent(request.Content);
        if (structuralError is not null)
            return Result.Failure(structuralError);
        var validation = await applicability.ValidateAsync(request.TenantId, request.Content, ct)
            .ConfigureAwait(false);
        if (!validation.IsSuccess)
            return validation;
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        return await executor.ExecuteAsync(new ControlDraft(request.TenantId, request.ControlId),
            control => AggregateOutcome.CommitOnSuccess(control.Revise(request.ProgramId,
                request.ExpectedRevision, request.Content,
                RbacIds.Member(request.TenantId, userId),
                UserIdentityClaims.BdgrzDisplay(context.Actor, userId), clock.GetUtcNow())),
            context, ct).ConfigureAwait(false);
    }
}
