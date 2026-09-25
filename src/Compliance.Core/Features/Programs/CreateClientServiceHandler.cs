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
