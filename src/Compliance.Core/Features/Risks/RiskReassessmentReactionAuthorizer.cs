using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>Allows reassessment trigger commands only from Portia's trusted system actor.</summary>
sealed class RiskReassessmentReactionAuthorizer
    : IRequestAuthorizer<IRiskReassessmentReactionRequest>
{
    public ValueTask<Result> AuthorizeAsync(
        IRequestContext<IRiskReassessmentReactionRequest> context, CancellationToken ct) =>
        ValueTask.FromResult(RequestActor.IsSystem(context.Actor)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only Portia's trusted system actor may dispatch this request.")));
}
