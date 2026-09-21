using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed class CreateClientServiceHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<CreateClientService, ClientServiceRegistration>
{
    public async ValueTask<Result<ClientServiceRegistration>> HandleAsync(
        IRequestContext<CreateClientService> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.ProgramId == Uuid.Empty)
            return Result<ClientServiceRegistration>.Failure(new RequestError(RequestErrorKind.Validation,
                "A service requires its owning program."));
        var program = await reader.HydrateAsync(new ComplianceProgram(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        if (!program.IsCreated)
            return Result<ClientServiceRegistration>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The program was not found."));
        var (memberId, display) = ClientServiceActor.Snapshot(context);
        return await executor.ExecuteAsync(new ClientService(request.TenantId, context.RequestId),
            service => AggregateOutcome.CommitOnSuccess(service.Create(request.ProgramId,
                request.Name, request.Purpose, request.OwnerReference, memberId, display,
                clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}

public sealed class ReviseClientServiceHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<ReviseClientService>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviseClientService> context,
        CancellationToken ct)
    {
        var (memberId, display) = ClientServiceActor.Snapshot(context);
        return executor.ExecuteAsync(new ClientService(context.Request.TenantId,
                context.Request.ServiceId),
            service => AggregateOutcome.CommitOnSuccess(service.Revise(context.Request.ExpectedRevision,
                context.Request.Name, context.Request.Purpose, context.Request.OwnerReference,
                memberId, display, clock.GetUtcNow())), context, ct);
    }
}

public sealed class RetireClientServiceHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<RetireClientService>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RetireClientService> context,
        CancellationToken ct)
    {
        var (memberId, display) = ClientServiceActor.Snapshot(context);
        return executor.ExecuteAsync(new ClientService(context.Request.TenantId,
                context.Request.ServiceId),
            service => AggregateOutcome.CommitOnSuccess(service.Retire(context.Request.ExpectedRevision,
                context.Request.Rationale, memberId, display, clock.GetUtcNow())), context, ct);
    }
}

static class ClientServiceActor
{
    public static (Uuid MemberId, string Display) Snapshot<T>(IRequestContext<T> context)
        where T : IRequestBase
    {
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        var tenantId = context.Request switch
        {
            IProgramManagementRequest request => request.TenantId,
            _ => throw new InvalidOperationException("A service command requires tenant scope."),
        };
        return (RbacIds.Member(tenantId, userId),
            UserIdentityClaims.BdgrzDisplay(context.Actor, userId));
    }
}

public sealed class GetClientServiceHandler(IClientServiceDirectoryReader directory,
    IAggregateReader reader)
    : IRequestHandler<GetClientService, ClientServiceView>
{
    public async ValueTask<Result<ClientServiceView>> HandleAsync(IRequestContext<GetClientService> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.MinimumRevision is < 1)
            return Result<ClientServiceView>.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum revision must be positive."));
        var service = await directory.GetAsync(request.TenantId,
            request.ServiceId, ct).ConfigureAwait(false);
        if (request.MinimumRevision is { } minimum &&
            (service is null || service.Revision < minimum))
        {
            var current = await reader.HydrateAsync(new ClientService(request.TenantId,
                request.ServiceId), ct).ConfigureAwait(false);
            if (!current.IsCreated)
                return Result<ClientServiceView>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The service was not found."));
            return Result<ClientServiceView>.Failure(new RequestError(RequestErrorKind.Conflict,
                current.Revision < minimum
                    ? $"The service source has not reached revision {minimum}."
                    : $"The service projection has not reached revision {minimum}."));
        }
        return service is null
            ? Result<ClientServiceView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The service was not found."))
            : Result<ClientServiceView>.Success(service);
    }
}

public sealed class ListClientServicesHandler(IClientServiceDirectoryReader directory)
    : IRequestHandler<ListClientServices, Page<ClientServiceView>>
{
    public async ValueTask<Result<Page<ClientServiceView>>> HandleAsync(
        IRequestContext<ListClientServices> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<ClientServiceView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The client service list limit must be between 1 and 200."));
        Page<ClientServiceView> page;
        try
        {
            page = await directory.ListAsync(request.TenantId, request.Limit ?? 50,
                request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<ClientServiceView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The client service cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId)
            ? Result<Page<ClientServiceView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The services were not found."))
            : Result<Page<ClientServiceView>>.Success(page);
    }
}

public sealed class ListProgramClientServicesHandler(IClientServiceDirectoryReader directory,
    IAggregateReader reader) : IRequestHandler<ListProgramClientServices, Page<ClientServiceView>>
{
    public async ValueTask<Result<Page<ClientServiceView>>> HandleAsync(
        IRequestContext<ListProgramClientServices> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<ClientServiceView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The program client service list limit must be between 1 and 200."));
        var program = await reader.HydrateAsync(new ComplianceProgram(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        if (!program.IsCreated)
            return Result<Page<ClientServiceView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The program was not found."));
        Page<ClientServiceView> page;
        try
        {
            page = await directory.ListProgramAsync(request.TenantId, request.ProgramId,
                request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<ClientServiceView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The program client service cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId ||
                                      item.ProgramId != request.ProgramId)
            ? Result<Page<ClientServiceView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The program services were not found."))
            : Result<Page<ClientServiceView>>.Success(page);
    }
}

public sealed class ClientServiceHistoryReadConsistency(IClientServiceDirectoryReader directory,
    IAggregateReader reader)
{
    public async ValueTask<Result> EnsureAsync(Uuid tenantId, Uuid serviceId,
        long minimumRevision, CancellationToken ct)
    {
        if (minimumRevision < 1)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum service revision must be positive."));
        var view = await directory.GetAsync(tenantId, serviceId, ct).ConfigureAwait(false);
        if (view is not null && view.Revision >= minimumRevision)
            return Result.Success;
        var source = await reader.HydrateAsync(new ClientService(tenantId, serviceId), ct)
            .ConfigureAwait(false);
        if (!source.IsCreated)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The service was not found."));
        return Result.Failure(new RequestError(RequestErrorKind.Conflict,
            source.Revision < minimumRevision
                ? $"The service source has not reached revision {minimumRevision}."
                : $"The service projection has not reached revision {minimumRevision}."));
    }
}

public sealed class GetClientServiceRevisionHandler(IClientServiceDirectoryReader directory,
    ClientServiceHistoryReadConsistency consistency)
    : IRequestHandler<GetClientServiceRevision, ClientServiceRevisionView>
{
    public async ValueTask<Result<ClientServiceRevisionView>> HandleAsync(
        IRequestContext<GetClientServiceRevision> context, CancellationToken ct)
    {
        var request = context.Request;
        var freshness = await consistency.EnsureAsync(request.TenantId, request.ServiceId,
            request.Revision, ct).ConfigureAwait(false);
        if (!freshness.IsSuccess)
            return Result<ClientServiceRevisionView>.Failure(freshness.Error);
        var revision = await directory.GetRevisionAsync(request.TenantId, request.ServiceId,
            request.Revision, ct).ConfigureAwait(false);
        return revision is null
            ? Result<ClientServiceRevisionView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The service revision was not found."))
            : Result<ClientServiceRevisionView>.Success(revision);
    }
}

public sealed class ListClientServiceRevisionsHandler(IClientServiceDirectoryReader directory,
    ClientServiceHistoryReadConsistency consistency)
    : IRequestHandler<ListClientServiceRevisions, Page<ClientServiceRevisionView>>
{
    public async ValueTask<Result<Page<ClientServiceRevisionView>>> HandleAsync(
        IRequestContext<ListClientServiceRevisions> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<ClientServiceRevisionView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The client service revision list limit must be between 1 and 200."));
        if (request.MinimumServiceRevision is { } minimum)
        {
            var freshness = await consistency.EnsureAsync(request.TenantId, request.ServiceId,
                minimum, ct).ConfigureAwait(false);
            if (!freshness.IsSuccess)
                return Result<Page<ClientServiceRevisionView>>.Failure(freshness.Error);
        }
        Page<ClientServiceRevisionView>? page;
        try
        {
            page = await directory.ListRevisionsAsync(request.TenantId, request.ServiceId,
                request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<ClientServiceRevisionView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The client service revision cursor is invalid."));
        }
        return page is null
            ? Result<Page<ClientServiceRevisionView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The service was not found."))
            : page.Items.Any(item => item.ServiceId != request.ServiceId)
                ? Result<Page<ClientServiceRevisionView>>.Failure(new RequestError(
                    RequestErrorKind.Conflict, "The service revision projection is incomplete."))
            : Result<Page<ClientServiceRevisionView>>.Success(page);
    }
}
