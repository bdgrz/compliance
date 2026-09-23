using Bdgrz.Compliance.Features.Boundaries;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed class GetProgramSetupWorkHandler(IProgramDirectoryReader programs,
    IBoundaryDirectoryReader boundaries, IAggregateReader reader,
    ProgramSetupWorkReadConsistency consistency)
    : IRequestHandler<GetProgramSetupWork, ProgramSetupWorkView>
{
    public async ValueTask<Result<ProgramSetupWorkView>> HandleAsync(
        IRequestContext<GetProgramSetupWork> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.BoundaryLimit is < 1 or > 200)
            return Result<ProgramSetupWorkView>.Failure(new RequestError(RequestErrorKind.Validation,
                "A boundary page limit must be between 1 and 200."));
        if (request.MinimumProgramRevision is < 1 ||
            request.MinimumBoundaryRevision is < 1 ||
            (request.BoundaryId is null) != (request.MinimumBoundaryRevision is null) ||
            request.BoundaryId == Uuid.Empty)
            return Result<ProgramSetupWorkView>.Failure(new RequestError(RequestErrorKind.Validation,
                "A revision expectation must be positive and identify its boundary."));

        var beforeRead = await consistency.CaptureAsync(request.TenantId, ct)
            .ConfigureAwait(false);
        if (!beforeRead.IsSuccess)
            return Result<ProgramSetupWorkView>.Failure(beforeRead.Error);
        var fence = beforeRead.Value;

        var program = await programs.GetAsync(request.TenantId, request.ProgramId, ct)
            .ConfigureAwait(false);
        if (request.MinimumProgramRevision is { } programRevision &&
            (program is null || program.Revision < programRevision))
        {
            var current = await reader.HydrateAsync(new ComplianceProgram(request.TenantId,
                request.ProgramId), ct).ConfigureAwait(false);
            if (!current.IsCreated)
                return await ConfirmResultAsync(Result<ProgramSetupWorkView>.Failure(new RequestError(
                    RequestErrorKind.NotFound, "The program was not found.")), fence, request.TenantId,
                    ct).ConfigureAwait(false);
            return await ConfirmResultAsync(Result<ProgramSetupWorkView>.Failure(new RequestError(
                RequestErrorKind.Conflict, current.Revision < programRevision
                    ? $"The program source has not reached revision {programRevision}."
                    : $"The program projection has not reached revision {programRevision}.")), fence,
                request.TenantId, ct).ConfigureAwait(false);
        }
        if (program is null || program.TenantId != request.TenantId ||
            program.ProgramId != request.ProgramId)
            return await ConfirmResultAsync(Result<ProgramSetupWorkView>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The program was not found.")), fence, request.TenantId, ct)
                .ConfigureAwait(false);

        if (request.BoundaryId is { } boundaryId &&
            request.MinimumBoundaryRevision is { } boundaryRevision)
        {
            var boundary = await boundaries.GetAsync(request.TenantId, boundaryId, ct)
                .ConfigureAwait(false);
            if (boundary is null || boundary.Revision < boundaryRevision)
            {
                var current = await reader.HydrateAsync(new SystemBoundary(request.TenantId,
                    boundaryId), ct).ConfigureAwait(false);
                if (!current.IsCreated || !current.IsVisible || current.ProgramId != request.ProgramId)
                    return await ConfirmResultAsync(Result<ProgramSetupWorkView>.Failure(
                        new RequestError(RequestErrorKind.NotFound, "The boundary was not found.")),
                        fence, request.TenantId, ct).ConfigureAwait(false);
                return await ConfirmResultAsync(Result<ProgramSetupWorkView>.Failure(
                    new RequestError(RequestErrorKind.Conflict, current.Revision < boundaryRevision
                        ? $"The boundary source has not reached revision {boundaryRevision}."
                        : $"The boundary projection has not reached revision {boundaryRevision}.")),
                    fence, request.TenantId, ct).ConfigureAwait(false);
            }
            if (boundary.ProgramId != request.ProgramId)
                return await ConfirmResultAsync(Result<ProgramSetupWorkView>.Failure(new RequestError(
                    RequestErrorKind.NotFound, "The boundary was not found.")), fence, request.TenantId,
                    ct).ConfigureAwait(false);
        }

        var work = new List<ProgramSetupWorkItem>();
        Page<BoundaryView> page;
        try
        {
            page = await boundaries.ListProgramAsync(request.TenantId, request.ProgramId,
                request.BoundaryLimit ?? 50, request.BoundaryCursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<ProgramSetupWorkView>.Failure(new RequestError(RequestErrorKind.Validation,
                "The boundary cursor is invalid."));
        }
        if (page.Items.Any(boundary => boundary.TenantId != request.TenantId ||
                boundary.ProgramId != request.ProgramId))
            return await ConfirmResultAsync(Result<ProgramSetupWorkView>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The boundary projection returned inconsistent content.")),
                fence, request.TenantId, ct).ConfigureAwait(false);
        if (request.BoundaryCursor is null && page.Items.Count == 0)
            work.Add(new ProgramSetupWorkItem("define_system_boundary",
                "Define the services and intended Trust Services categories in a system boundary.",
                "program", request.ProgramId));

        foreach (var boundary in page.Items)
        {
            if (boundary.LatestApprovedVersion is null)
                work.Add(new ProgramSetupWorkItem("approve_system_boundary",
                    "Review and approve this system boundary before using it as assessed scope.",
                    "boundary", boundary.BoundaryId));
            var version = boundary.LatestApprovedVersion ?? boundary.Draft;
            if (version?.Content.Entries.Any(static entry =>
                    entry.Kind == "inclusion" && entry.SubjectType == "service") == false)
                work.Add(new ProgramSetupWorkItem("identify_scoped_services",
                    "Identify the client services included in this boundary.",
                    "boundary", boundary.BoundaryId));
            if (version?.Content.Entries.Any(static entry => entry.Unresolved) == true)
                work.Add(new ProgramSetupWorkItem("resolve_scope_references",
                    "Resolve the boundary's explicitly unresolved scope references.",
                    "boundary", boundary.BoundaryId));
        }

        return await ConfirmResultAsync(Result<ProgramSetupWorkView>.Success(
            new ProgramSetupWorkView(request.TenantId, request.ProgramId, program.Revision, work,
                page.NextCursor)), fence, request.TenantId, ct).ConfigureAwait(false);
    }

    async ValueTask<Result<ProgramSetupWorkView>> ConfirmResultAsync(
        Result<ProgramSetupWorkView> candidate, ProgramSetupWorkReadFence fence, Uuid tenantId,
        CancellationToken ct)
    {
        var confirmation = await consistency.ConfirmUnchangedAndCaughtUpAsync(tenantId, fence, ct)
            .ConfigureAwait(false);
        return confirmation.IsSuccess
            ? candidate
            : Result<ProgramSetupWorkView>.Failure(confirmation.Error);
    }
}
