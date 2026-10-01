using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>
///     Fulfils an open request with a captured artifact. Only the request owner or a program manager may fulfil, and a
///     rejected or disposed artifact can never answer a request.
/// </summary>
public sealed class FulfilEvidenceRequestHandler(IAggregateExecutor executor, IAggregateReader reader,
    OperatingAuthority authority, TimeProvider clock) : IRequestHandler<FulfilEvidenceRequest, EvidenceRequestView>
{
    public async ValueTask<Result<EvidenceRequestView>> HandleAsync(IRequestContext<FulfilEvidenceRequest> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var ledger = await reader.HydrateAsync(new EvidenceRequestLedger(request.TenantId, request.ProgramId), ct)
            .ConfigureAwait(false);
        if (ledger.Find(request.EvidenceRequestId) is not { } current)
            return Result<EvidenceRequestView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The evidence request was not found."));
        if (current.OwnerMemberId != actor.MemberId &&
            !await authority.ManagesProgramAsync(request.TenantId, actor, request.ProgramId, ct).ConfigureAwait(false))
            return Result<EvidenceRequestView>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the request owner or a program manager may fulfil this request."));
        var artifact = await reader.HydrateAsync(new EvidenceArtifact(request.TenantId, request.ArtifactId), ct)
            .ConfigureAwait(false);
        if (!artifact.IsCreated)
            return Result<EvidenceRequestView>.Failure(new RequestError(RequestErrorKind.Validation,
                "The evidence artifact was not found."));
        if (artifact.State is EvidenceArtifactStates.Rejected or EvidenceArtifactStates.Disposed)
            return Result<EvidenceRequestView>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"A {artifact.State} evidence artifact cannot fulfil a request."));
        return await EvidenceRequestCommands.ExecuteAsync(executor, context, request.TenantId, request.ProgramId,
            request.EvidenceRequestId, l => l.Fulfil(request.EvidenceRequestId, request.ExpectedRevision,
                request.ArtifactId, ActorReference.ForMember(actor.MemberId, actor.Display), clock.GetUtcNow()), ct)
            .ConfigureAwait(false);
    }
}
