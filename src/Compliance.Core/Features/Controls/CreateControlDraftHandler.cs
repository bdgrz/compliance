using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

public sealed class CreateControlDraftHandler(IAggregateExecutor executor,
    IAggregateReader reader, IControlApplicabilityReferenceValidator applicability,
    TimeProvider clock)
    : IRequestHandler<CreateControlDraft, ControlRegistration>
{
    public async ValueTask<Result<ControlRegistration>> HandleAsync(
        IRequestContext<CreateControlDraft> context, CancellationToken ct)
    {
        var request = context.Request;
        var identifier = ControlDraft.NormalizeIdentifier(request.Identifier);
        if (identifier.Length is < 1 or > 80)
            return Result<ControlRegistration>.Failure(new RequestError(RequestErrorKind.Validation,
                "A control identifier requires 1 to 80 characters."));
        var program = await reader.HydrateAsync(new ComplianceProgram(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        if (!program.IsCreated)
            return Result<ControlRegistration>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The program was not found."));
        var structuralError = ControlDraft.ValidateCreateInput(request.Identifier, request.Content);
        if (structuralError is not null)
            return Result<ControlRegistration>.Failure(structuralError);
        var existing = await reader.HydrateAsync(new ControlDraft(request.TenantId,
            ControlDraft.IdFor(request.TenantId, request.ProgramId, request.Identifier)), ct)
            .ConfigureAwait(false);
        if (!existing.IsCreated)
        {
            var validation = await applicability.ValidateAsync(request.TenantId, request.Content, ct)
                .ConfigureAwait(false);
            if (!validation.IsSuccess)
                return Result<ControlRegistration>.Failure(validation.Error);
        }
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        var controlId = ControlDraft.IdFor(request.TenantId, request.ProgramId, identifier);
        return await executor.ExecuteAsync(new ControlDraft(request.TenantId, controlId),
            control => AggregateOutcome.CommitOnSuccess(control.Create(request.ProgramId,
                context.RequestId, identifier, request.Content,
                RbacIds.Member(request.TenantId, userId),
                UserIdentityClaims.BdgrzDisplay(context.Actor, userId), clock.GetUtcNow())),
            context, ct).ConfigureAwait(false);
    }
}
