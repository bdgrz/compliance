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

public sealed class ListProgramsHandler(IProgramDirectoryReader directory)
    : IRequestHandler<ListPrograms, Page<ProgramView>>
{
    public async ValueTask<Result<Page<ProgramView>>> HandleAsync(
        IRequestContext<ListPrograms> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<ProgramView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The program list limit must be between 1 and 200."));
        Page<ProgramView> page;
        try
        {
            page = await directory.ListAsync(request.TenantId, request.Limit ?? 50,
                request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<ProgramView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The program cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId)
            ? Result<Page<ProgramView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The programs were not found."))
            : Result<Page<ProgramView>>.Success(page);
    }
}

public sealed class ProgramHistoryReadConsistency(IProgramDirectoryReader directory,
    IAggregateReader reader)
{
    public async ValueTask<Result> EnsureAsync(Uuid tenantId, Uuid programId,
        long minimumRevision, CancellationToken ct)
    {
        if (minimumRevision < 1)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum program revision must be positive."));
        var view = await directory.GetAsync(tenantId, programId, ct).ConfigureAwait(false);
        if (view is not null && view.Revision >= minimumRevision)
            return Result.Success;
        var source = await reader.HydrateAsync(new ComplianceProgram(tenantId, programId), ct)
            .ConfigureAwait(false);
        if (!source.IsCreated)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The program was not found."));
        return Result.Failure(new RequestError(RequestErrorKind.Conflict,
            source.Revision < minimumRevision
                ? $"The program source has not reached revision {minimumRevision}."
                : $"The program projection has not reached revision {minimumRevision}."));
    }
}

public sealed class GetProgramRevisionHandler(IProgramDirectoryReader directory,
    ProgramHistoryReadConsistency consistency)
    : IRequestHandler<GetProgramRevision, ProgramRevisionView>
{
    public async ValueTask<Result<ProgramRevisionView>> HandleAsync(
        IRequestContext<GetProgramRevision> context, CancellationToken ct)
    {
        var request = context.Request;
        var freshness = await consistency.EnsureAsync(request.TenantId, request.ProgramId,
            request.Revision, ct).ConfigureAwait(false);
        if (!freshness.IsSuccess)
            return Result<ProgramRevisionView>.Failure(freshness.Error);
        var revision = await directory.GetRevisionAsync(request.TenantId, request.ProgramId,
            request.Revision, ct).ConfigureAwait(false);
        return revision is null
            ? Result<ProgramRevisionView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The program revision was not found."))
            : Result<ProgramRevisionView>.Success(revision);
    }
}

public sealed class ListProgramRevisionsHandler(IProgramDirectoryReader directory,
    ProgramHistoryReadConsistency consistency)
    : IRequestHandler<ListProgramRevisions, Page<ProgramRevisionView>>
{
    public async ValueTask<Result<Page<ProgramRevisionView>>> HandleAsync(
        IRequestContext<ListProgramRevisions> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<ProgramRevisionView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The program revision list limit must be between 1 and 200."));
        if (request.MinimumProgramRevision is { } minimum)
        {
            var freshness = await consistency.EnsureAsync(request.TenantId, request.ProgramId,
                minimum, ct).ConfigureAwait(false);
            if (!freshness.IsSuccess)
                return Result<Page<ProgramRevisionView>>.Failure(freshness.Error);
        }
        Page<ProgramRevisionView>? page;
        try
        {
            page = await directory.ListRevisionsAsync(request.TenantId, request.ProgramId,
                request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<ProgramRevisionView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The program revision cursor is invalid."));
        }
        return page is null
            ? Result<Page<ProgramRevisionView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The program was not found."))
            : page.Items.Any(item => item.ProgramId != request.ProgramId)
                ? Result<Page<ProgramRevisionView>>.Failure(new RequestError(
                    RequestErrorKind.Conflict, "The program revision projection is incomplete."))
            : Result<Page<ProgramRevisionView>>.Success(page);
    }
}
