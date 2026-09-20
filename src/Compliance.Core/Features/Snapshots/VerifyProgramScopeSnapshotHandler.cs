using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots;

public sealed class VerifyProgramScopeSnapshotHandler(ISnapshotDirectoryReader snapshots,
    IProgramDirectoryReader programs, IBoundaryDirectoryReader boundaries,
    IAggregateReader reader)
    : IRequestHandler<VerifyProgramScopeSnapshot, ProgramScopeSnapshotVerification>
{
    public async ValueTask<Result<ProgramScopeSnapshotVerification>> HandleAsync(
        IRequestContext<VerifyProgramScopeSnapshot> context, CancellationToken ct)
    {
        var request = context.Request;
        var source = await reader.HydrateAsync(new ImmutableSnapshot(request.TenantId,
            request.SnapshotId), ct).ConfigureAwait(false);
        if (!source.IsFrozen)
            return Result<ProgramScopeSnapshotVerification>.Failure(
                new RequestError(RequestErrorKind.NotFound, "The snapshot was not found."));

        var manifest = source.Manifest;
        if (manifest is null || manifest.TenantId != request.TenantId ||
            manifest.ProgramId != source.ProgramId)
            return Result<ProgramScopeSnapshotVerification>.Failure(
                new RequestError(RequestErrorKind.Conflict,
                    "The snapshot source manifest is inconsistent."));

        var projected = await snapshots.GetAsync(request.TenantId, request.SnapshotId, ct)
            .ConfigureAwait(false);
        if (projected is not null && (projected.TenantId != request.TenantId ||
                                     projected.SnapshotId != request.SnapshotId))
            return Result<ProgramScopeSnapshotVerification>.Failure(
                new RequestError(RequestErrorKind.NotFound, "The snapshot was not found."));

        var sourceIntegrity = source.Kind == "program_scope" &&
            SnapshotContentIdentity.MatchesManifest(manifest,
            source.CanonicalManifest ?? string.Empty, source.ContentSha256 ?? string.Empty);
        var snapshotStatus = !sourceIntegrity ? "digest_mismatch" :
            projected is null ? "lag" :
            SnapshotContentIdentity.MatchesView(projected) &&
            projected.Manifest == manifest &&
            projected.ProgramId == source.ProgramId &&
            projected.RootSnapshotId == source.RootSnapshotId &&
            projected.AmendsSnapshotId == source.AmendsSnapshotId &&
            projected.CanonicalManifest == source.CanonicalManifest &&
            projected.ContentSha256 == source.ContentSha256
                ? "verified"
                : "digest_mismatch";
        var snapshotCheck = new SnapshotSourceCheck(snapshotStatus,
            source.ContentSha256 ?? string.Empty, projected?.ContentSha256);

        var program = await reader.HydrateAsync(new ComplianceProgram(request.TenantId,
            manifest.ProgramId), ct).ConfigureAwait(false);
        var programRevision = await programs.GetRevisionAsync(request.TenantId,
            manifest.ProgramId, manifest.ProgramRevision, ct).ConfigureAwait(false);
        var programCheck = CheckProgram(manifest, program, programRevision);

        var boundary = await reader.HydrateAsync(new SystemBoundary(request.TenantId,
            manifest.BoundaryId), ct).ConfigureAwait(false);
        var approvedVersion = await boundaries.GetVersionAsync(request.TenantId,
            manifest.BoundaryId, manifest.ApprovedBoundaryVersionId, ct).ConfigureAwait(false);
        var boundaryCheck = CheckBoundary(manifest, boundary, approvedVersion);

        return Result<ProgramScopeSnapshotVerification>.Success(
            new ProgramScopeSnapshotVerification(request.TenantId, request.SnapshotId,
                snapshotCheck.Status == "verified" && programCheck.Status == "verified" &&
                boundaryCheck.Status == "verified", snapshotCheck, programCheck,
                boundaryCheck));
    }

    static SnapshotSourceCheck CheckProgram(ProgramScopeManifest manifest,
        ComplianceProgram source, ProgramRevisionView? projected)
    {
        var exactRow = projected is not null &&
                       projected.ProgramId == manifest.ProgramId &&
                       projected.Revision == manifest.ProgramRevision;
        var observed = exactRow
            ? SnapshotContentIdentity.ProgramRevision(projected!)
            : null;
        var status = !source.IsCreated || source.Id != manifest.ProgramId ||
                     source.Revision < manifest.ProgramRevision
            ? "missing"
            : projected is null
                ? "lag"
                : exactRow && observed == manifest.ProgramContentSha256
                    ? "verified"
                    : "digest_mismatch";
        return new SnapshotSourceCheck(status, manifest.ProgramContentSha256, observed);
    }

    static SnapshotSourceCheck CheckBoundary(ProgramScopeManifest manifest,
        SystemBoundary source, BoundaryVersionView? projected)
    {
        var exactRow = projected is not null &&
                       projected.TenantId == manifest.TenantId &&
                       projected.ProgramId == manifest.ProgramId &&
                       projected.BoundaryId == manifest.BoundaryId &&
                       projected.VersionId == manifest.ApprovedBoundaryVersionId;
        var observed = exactRow
            ? SnapshotContentIdentity.ApprovedBoundaryVersion(projected!)
            : null;
        var approved = source.IsCreated && source.Id == manifest.BoundaryId &&
                       source.IsVisible &&
                       source.ProgramId == manifest.ProgramId &&
                       source.IsVersionApproved(manifest.ApprovedBoundaryVersionId);
        string status;
        if (!approved)
            status = "missing";
        else if (projected is null || (exactRow && projected.Status != "approved"))
            status = "lag";
        else if (!exactRow || observed != manifest.BoundaryContentSha256)
            status = "digest_mismatch";
        else
            status = "verified";
        return new SnapshotSourceCheck(status, manifest.BoundaryContentSha256, observed);
    }
}
