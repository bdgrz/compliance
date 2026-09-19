using Bdgrz.Compliance.Features.Boundaries;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed class GetProgramSetupWorkHandler(IProgramDirectoryReader programs,
    IBoundaryDirectoryReader boundaries)
    : IRequestHandler<GetProgramSetupWork, ProgramSetupWorkView>
{
    public async ValueTask<Result<ProgramSetupWorkView>> HandleAsync(
        IRequestContext<GetProgramSetupWork> context, CancellationToken ct)
    {
        var request = context.Request;
        var program = await programs.GetAsync(request.TenantId, request.ProgramId, ct)
            .ConfigureAwait(false);
        if (program is null)
            return Result<ProgramSetupWorkView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The program was not found."));

        var work = new List<ProgramSetupWorkItem>();
        var page = await boundaries.ListProgramAsync(request.TenantId, request.ProgramId,
            Math.Clamp(request.BoundaryLimit ?? 50, 1, 200), request.BoundaryCursor, ct)
            .ConfigureAwait(false);
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

        return Result<ProgramSetupWorkView>.Success(new ProgramSetupWorkView(request.TenantId,
            request.ProgramId, program.Revision, work, page.NextCursor));
    }
}
