using Bdgrz.Compliance.Features.Versioning;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Retires an application, optionally as merged into an active successor.</summary>
public sealed class RetireApplicationHandler(IAggregateExecutor executor,
    IAggregateReader reader, IApplicationChangeImpactReader impactReader,
    TimeProvider clock) : IRequestHandler<RetireApplication>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<RetireApplication> context,
        CancellationToken ct)
    {
        var (memberId, display) = ApplicationActor.From(context);
        var request = context.Request;
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : Uuid.Empty;
        if (request.MergedIntoApplicationId is { } successorId &&
            successorId != request.ApplicationId && successorId != Uuid.Empty)
        {
            var predecessor = await reader.HydrateApplicationAsync(request.TenantId,
                request.ApplicationId, ct).ConfigureAwait(false);
            if (!predecessor.IsCreated)
                return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The application was not found."));
            var successor = await reader.HydrateApplicationAsync(request.TenantId, successorId, ct).ConfigureAwait(false);
            if (!successor.IsCreated)
                return Result.Failure(new RequestError(RequestErrorKind.Validation,
                    "The merged-into application was not found."));
            if (successor.IsRetired)
                return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The merged-into application is retired."));
            if (!MatchesCurrentApproval(predecessor, successor, request))
                return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "Merging requires a current approved successor relationship."));
            var assessment = await impactReader.ReadAsync(new PreviewApplicationChange(
                request.TenantId, request.ApplicationId, request.ExpectedRevision, "retire"),
                userId, ct).ConfigureAwait(false);
            if (!assessment.IsSuccess)
                return Result.Failure(assessment.Error);
            var approval = predecessor.Relationship(ApplicationRelationshipIdentity.Replaces,
                successorId)!.Approval!;
            if (!assessment.Value.Complete || assessment.Value.PendingContexts.Count != 0 ||
                !string.Equals(assessment.Value.ImpactDigest, approval.ImpactDigest,
                    StringComparison.Ordinal))
                return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "Retirement is blocked because the approved impact evidence is incomplete or changed."));

            var latestPredecessor = await reader.HydrateApplicationAsync(request.TenantId,
                request.ApplicationId, ct).ConfigureAwait(false);
            var latestSuccessor = await reader.HydrateApplicationAsync(request.TenantId,
                successorId, ct).ConfigureAwait(false);
            if (!MatchesCurrentApproval(latestPredecessor, latestSuccessor, request) ||
                latestPredecessor.Relationship(ApplicationRelationshipIdentity.Replaces,
                    successorId)?.Revision != predecessor.Relationship(
                    ApplicationRelationshipIdentity.Replaces, successorId)?.Revision)
                return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The application relationship changed during retirement validation."));
        }
        // Portia commits one aggregate stream at a time. The retirement event binds the
        // approved successor revision; it does not promise a cross-stream atomic snapshot.
        var target = await ApplicationImportWriteGuard.PrepareAsync(reader, request.TenantId, request.ApplicationId, ct)
            .ConfigureAwait(false);
        return await executor.ExecuteAsync(target,
            app => app.CheckPendingImportChanges() is { } importError
                ? AggregateOutcome.Discard(Result.Failure(importError))
                : request.MergedIntoApplicationId is { } successorId &&
                  successorId != Uuid.Empty && successorId != request.ApplicationId &&
                  !MatchesCurrentApproval(app, successorId, request.ExpectedRevision)
                    ? AggregateOutcome.Discard(Result.Failure(new RequestError(
                        RequestErrorKind.Conflict,
                        "Merging requires a current approved successor relationship.")))
                    : CommandFailureRequestAdapter.ToOutcome(app.Retire(request.ExpectedRevision,
                        request.EffectiveAt, request.Reason, request.MergedIntoApplicationId,
                        memberId, display, clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }

    static bool MatchesCurrentApproval(DeclaredApplication predecessor,
        DeclaredApplication successor, RetireApplication request)
    {
        if (!predecessor.IsCreated || predecessor.IsRetired ||
            predecessor.Revision != request.ExpectedRevision || !successor.IsCreated ||
            successor.IsRetired || request.MergedIntoApplicationId is not { } successorId)
            return false;
        var relationship = predecessor.Relationship(ApplicationRelationshipIdentity.Replaces,
            successorId);
        return relationship is { Status: "approved", Approval: { } approval } &&
               relationship.SourceApplicationRevision == request.ExpectedRevision &&
               approval.SourceApplicationRevision == request.ExpectedRevision &&
               approval.TargetApplicationRevision == successor.Revision;
    }

    static bool MatchesCurrentApproval(DeclaredApplication predecessor, Uuid successorId,
        long expectedPredecessorRevision)
    {
        var relationship = predecessor.Relationship(ApplicationRelationshipIdentity.Replaces,
            successorId);
        return relationship is { Status: "approved", Approval: { } approval } &&
               relationship.SourceApplicationRevision == expectedPredecessorRevision &&
               approval.SourceApplicationRevision == expectedPredecessorRevision;
    }
}
