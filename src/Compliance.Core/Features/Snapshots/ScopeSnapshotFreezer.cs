using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots;

public sealed class ScopeSnapshotFreezer(IProgramDirectoryReader programs,
    IBoundaryDirectoryReader boundaries, IAggregateReader reader,
    IAggregateExecutor executor, TimeProvider clock)
{
    public async ValueTask<Result<SnapshotRegistration>> FreezeAsync<TRequest>(
        IRequestContext<TRequest> context, Uuid tenantId, Uuid programId,
        long expectedProgramRevision, Uuid boundaryId, Uuid approvedBoundaryVersionId,
        Uuid? amendsSnapshotId, string? amendmentReason, CancellationToken ct)
        where TRequest : IRequestBase
    {
        if (programId == Uuid.Empty || boundaryId == Uuid.Empty ||
            approvedBoundaryVersionId == Uuid.Empty || expectedProgramRevision < 1 ||
            (amendsSnapshotId is null) != (amendmentReason is null) ||
            (amendmentReason is not null &&
             (string.IsNullOrWhiteSpace(amendmentReason) || amendmentReason.Length > 4000)))
            return Failure(RequestErrorKind.Validation,
                "A scope snapshot requires exact source versions and valid amendment details.");

        var program = await reader.HydrateAsync(new ComplianceProgram(tenantId, programId), ct)
            .ConfigureAwait(false);
        if (!program.IsCreated)
            return Failure(RequestErrorKind.NotFound, "The program was not found.");
        if (program.Revision < expectedProgramRevision)
            return Failure(RequestErrorKind.Conflict,
                "The program source has not reached the requested revision.");
        var programRevision = await programs.GetRevisionAsync(tenantId, programId,
            expectedProgramRevision, ct).ConfigureAwait(false);
        if (programRevision is null)
            return Failure(RequestErrorKind.Conflict,
                "The program revision projection has not reached the requested revision.");
        if (programRevision.ProgramId != programId ||
            programRevision.Revision != expectedProgramRevision)
            return Failure(RequestErrorKind.Conflict,
                "The program revision projection returned inconsistent content.");

        var boundary = await reader.HydrateAsync(new SystemBoundary(tenantId, boundaryId), ct)
            .ConfigureAwait(false);
        if (!boundary.IsCreated || !boundary.IsVisible || boundary.ProgramId != programId)
            return Failure(RequestErrorKind.NotFound, "The boundary was not found.");
        var approved = await boundaries.GetVersionAsync(tenantId, boundaryId,
            approvedBoundaryVersionId, ct).ConfigureAwait(false);
        if (approved is null)
            return boundary.IsVersionApproved(approvedBoundaryVersionId)
                ? Failure(RequestErrorKind.Conflict,
                    "The approved boundary version projection has not caught up.")
                : Failure(RequestErrorKind.NotFound,
                    "The approved boundary version was not found.");
        if (approved.Status != "approved" &&
            boundary.IsVersionApproved(approvedBoundaryVersionId))
            return Failure(RequestErrorKind.Conflict,
                "The approved boundary version projection has not caught up.");
        if (approved.Status != "approved" || approved.TenantId != tenantId ||
            approved.BoundaryId != boundaryId || approved.ProgramId != programId)
            return Failure(RequestErrorKind.NotFound,
                "The approved boundary version was not found.");

        var snapshotId = context.RequestId;
        var rootSnapshotId = snapshotId;
        if (amendsSnapshotId is { } priorId)
        {
            if (priorId == snapshotId)
                return Failure(RequestErrorKind.Validation,
                    "An amendment cannot name itself as its predecessor.");
            var prior = await reader.HydrateAsync(new ImmutableSnapshot(tenantId, priorId), ct)
                .ConfigureAwait(false);
            if (!prior.IsFrozen)
                return Failure(RequestErrorKind.NotFound, "The prior snapshot was not found.");
            if (prior.ProgramId != programId)
                return Failure(RequestErrorKind.NotFound, "The prior snapshot was not found.");
            var lineage = await SnapshotAmendmentLineage.InspectAsync(reader, prior, tenantId,
                SnapshotAmendmentLineage.MaximumAmendmentLinks - 1, ct).ConfigureAwait(false);
            if (!lineage.IsConsistent || lineage.ExceedsMaximum)
                return Failure(RequestErrorKind.Conflict,
                    "The prior snapshot lineage is invalid or has reached the supported amendment limit.");
            rootSnapshotId = prior.RootSnapshotId;
        }

        var manifest = new ProgramScopeManifest(1, tenantId, programId,
            expectedProgramRevision, SnapshotContentIdentity.ProgramRevision(programRevision),
            boundaryId, approvedBoundaryVersionId,
            SnapshotContentIdentity.ApprovedBoundaryVersion(approved));
        var (canonicalJson, digest) = SnapshotContentIdentity.Manifest(manifest);
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        return await executor.ExecuteAsync(new ImmutableSnapshot(tenantId, snapshotId),
            snapshot => AggregateOutcome.CommitOnSuccess(snapshot.Freeze(rootSnapshotId,
                amendsSnapshotId, programId, manifest, canonicalJson, digest,
                amendmentReason, RbacIds.Member(tenantId, userId),
                UserIdentityClaims.BdgrzDisplay(context.Actor, userId),
                clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }

    static Result<SnapshotRegistration> Failure(RequestErrorKind kind, string message) =>
        Result<SnapshotRegistration>.Failure(new RequestError(kind, message));
}
