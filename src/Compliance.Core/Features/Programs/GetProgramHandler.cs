using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed class GetProgramHandler(IProgramDirectoryReader directory, IAggregateReader reader)
    : IRequestHandler<GetProgram, ProgramView>
{
    public async ValueTask<Result<ProgramView>> HandleAsync(IRequestContext<GetProgram> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.MinimumRevision is < 1)
            return Result<ProgramView>.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum revision must be positive."));
        var program = await directory.GetAsync(request.TenantId, request.ProgramId, ct)
            .ConfigureAwait(false);
        if (request.MinimumRevision is { } minimum &&
            (program is null || program.Revision < minimum))
        {
            var current = await reader.HydrateAsync(new ComplianceProgram(request.TenantId,
                request.ProgramId), ct).ConfigureAwait(false);
            if (!current.IsCreated)
                return Result<ProgramView>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The program was not found."));
            return Result<ProgramView>.Failure(new RequestError(RequestErrorKind.Conflict,
                current.Revision < minimum
                    ? $"The program source has not reached revision {minimum}."
                    : $"The program projection has not reached revision {minimum}."));
        }
        return program is null
            ? Result<ProgramView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The program was not found."))
            : Result<ProgramView>.Success(program);
    }
}
