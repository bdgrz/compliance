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

public sealed class FreezeProgramScopeSnapshotHandler(ScopeSnapshotFreezer freezer)
    : IRequestHandler<FreezeProgramScopeSnapshot, SnapshotRegistration>
{
    public ValueTask<Result<SnapshotRegistration>> HandleAsync(
        IRequestContext<FreezeProgramScopeSnapshot> context, CancellationToken ct)
    {
        var request = context.Request;
        return freezer.FreezeAsync(context, request.TenantId, request.ProgramId,
            request.ExpectedProgramRevision, request.BoundaryId,
            request.ApprovedBoundaryVersionId, null, null, ct);
    }
}

public sealed class AmendProgramScopeSnapshotHandler(ScopeSnapshotFreezer freezer)
    : IRequestHandler<AmendProgramScopeSnapshot, SnapshotRegistration>
{
    public ValueTask<Result<SnapshotRegistration>> HandleAsync(
        IRequestContext<AmendProgramScopeSnapshot> context, CancellationToken ct)
    {
        var request = context.Request;
        return freezer.FreezeAsync(context, request.TenantId, request.ProgramId,
            request.ExpectedProgramRevision, request.BoundaryId,
            request.ApprovedBoundaryVersionId, request.SnapshotId, request.Reason, ct);
    }
}

public sealed class GetSnapshotHandler(ISnapshotDirectoryReader directory,
    IAggregateReader reader) : IRequestHandler<GetSnapshot, SnapshotView>
{
    public async ValueTask<Result<SnapshotView>> HandleAsync(
        IRequestContext<GetSnapshot> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.MinimumRevision is < 1)
            return Result<SnapshotView>.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum revision must be positive."));
        var view = await directory.GetAsync(request.TenantId, request.SnapshotId, ct)
            .ConfigureAwait(false);
        if (request.MinimumRevision is { } minimum &&
            (view is null || view.Revision < minimum))
        {
            var source = await reader.HydrateAsync(new ImmutableSnapshot(request.TenantId,
                request.SnapshotId), ct).ConfigureAwait(false);
            if (!source.IsFrozen)
                return Result<SnapshotView>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The snapshot was not found."));
            return Result<SnapshotView>.Failure(new RequestError(RequestErrorKind.Conflict,
                minimum > 1
                    ? "The snapshot source has not reached the requested revision."
                    : "The snapshot projection has not reached the requested revision."));
        }
        if (view is null || view.TenantId != request.TenantId ||
            view.SnapshotId != request.SnapshotId)
            return Result<SnapshotView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The snapshot was not found."));
        return SnapshotContentIdentity.MatchesView(view)
            ? Result<SnapshotView>.Success(view)
            : Result<SnapshotView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The stored snapshot manifest failed integrity verification."));
    }
}

public sealed class ListProgramSnapshotsHandler(ISnapshotDirectoryReader directory,
    IAggregateReader reader) : IRequestHandler<ListProgramSnapshots, Page<SnapshotView>>
{
    public async ValueTask<Result<Page<SnapshotView>>> HandleAsync(
        IRequestContext<ListProgramSnapshots> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<SnapshotView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The snapshot list limit must be between 1 and 200."));
        var program = await reader.HydrateAsync(new ComplianceProgram(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        if (!program.IsCreated)
            return Result<Page<SnapshotView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The program was not found."));
        Page<SnapshotView> page;
        try
        {
            page = await directory.ListProgramAsync(
                request.TenantId, request.ProgramId, request.Limit ?? 50, request.Cursor,
                ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<SnapshotView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The snapshot cursor is invalid."));
        }
        if (page.Items.Any(item => item.TenantId != request.TenantId ||
                item.ProgramId != request.ProgramId || !SnapshotContentIdentity.MatchesView(item)))
            return Result<Page<SnapshotView>>.Failure(new RequestError(RequestErrorKind.Conflict,
                "A stored snapshot manifest failed integrity verification."));
        return Result<Page<SnapshotView>>.Success(page);
    }
}
