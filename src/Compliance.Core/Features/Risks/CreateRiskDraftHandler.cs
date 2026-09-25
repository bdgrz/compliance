using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

public sealed class CreateRiskDraftHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<CreateRiskDraft, RiskRegistration>
{
    public async ValueTask<Result<RiskRegistration>> HandleAsync(
        IRequestContext<CreateRiskDraft> context, CancellationToken ct)
    {
        var request = context.Request;
        var identifier = RiskDraft.NormalizeIdentifier(request.Identifier);
        if (identifier.Length is < 1 or > 80)
            return Result<RiskRegistration>.Failure(new RequestError(RequestErrorKind.Validation,
                "A risk identifier requires 1 to 80 characters."));
        var program = await reader.HydrateAsync(new ComplianceProgram(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        if (!program.IsCreated)
            return Result<RiskRegistration>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The program was not found."));
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        var riskId = RiskDraft.IdFor(request.TenantId, request.ProgramId, identifier);
        RiskDraftContent content = new(request.Title, request.Scenario,
            request.PotentialEffect, request.SourceNote);
        return await executor.ExecuteAsync(new RiskDraft(request.TenantId, riskId),
            risk => AggregateOutcome.CommitOnSuccess(risk.Create(request.ProgramId,
                context.RequestId, identifier, content,
                RbacIds.Member(request.TenantId, userId),
                UserIdentityClaims.BdgrzDisplay(context.Actor, userId), clock.GetUtcNow())),
            context, ct).ConfigureAwait(false);
    }
}
