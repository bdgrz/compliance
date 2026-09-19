using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed class CreateClientServiceHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<CreateClientService, ClientServiceRegistration>
{
    public ValueTask<Result<ClientServiceRegistration>> HandleAsync(
        IRequestContext<CreateClientService> context, CancellationToken ct)
    {
        var (memberId, display) = ClientServiceActor.Snapshot(context);
        return executor.ExecuteAsync(new ClientService(context.Request.TenantId, context.RequestId),
            service => AggregateOutcome.CommitOnSuccess(service.Create(context.Request.Name,
                context.Request.Purpose, context.Request.OwnerReference, memberId, display,
                clock.GetUtcNow())), context, ct);
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
            context.Actor.FindFirst("email")?.Value ?? userId.ToString());
    }
}

public sealed class GetClientServiceHandler(IClientServiceDirectoryReader directory)
    : IRequestHandler<GetClientService, ClientServiceView>
{
    public async ValueTask<Result<ClientServiceView>> HandleAsync(IRequestContext<GetClientService> context,
        CancellationToken ct)
    {
        var service = await directory.GetAsync(context.Request.TenantId,
            context.Request.ServiceId, ct).ConfigureAwait(false);
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
        IRequestContext<ListClientServices> context, CancellationToken ct) =>
        Result<Page<ClientServiceView>>.Success(await directory.ListAsync(context.Request.TenantId,
            context.Request.Limit ?? 50, context.Request.Cursor, ct).ConfigureAwait(false));
}

public sealed class ListClientServiceRevisionsHandler(IClientServiceDirectoryReader directory)
    : IRequestHandler<ListClientServiceRevisions, Page<ClientServiceRevisionView>>
{
    public async ValueTask<Result<Page<ClientServiceRevisionView>>> HandleAsync(
        IRequestContext<ListClientServiceRevisions> context, CancellationToken ct)
    {
        var page = await directory.ListRevisionsAsync(context.Request.TenantId,
            context.Request.ServiceId, context.Request.Limit ?? 50,
            context.Request.Cursor, ct).ConfigureAwait(false);
        return page is null
            ? Result<Page<ClientServiceRevisionView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The service was not found."))
            : Result<Page<ClientServiceRevisionView>>.Success(page);
    }
}
