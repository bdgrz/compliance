using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class PreviewApplicationChangeMcpInputHandler(IRequestBus bus)
    : IRequestHandler<PreviewApplicationChangeMcpInput, ApplicationChangePreview>
{
    public ValueTask<Result<ApplicationChangePreview>> HandleAsync(
        IRequestContext<PreviewApplicationChangeMcpInput> context, CancellationToken ct)
    {
        var input = context.Request;
        var request = new PreviewApplicationChange(input.TenantId, input.ApplicationId,
            input.ExpectedApplicationRevision, input.ChangeKind, input.Name, input.Purpose,
            input.OwnerReference, input.Classification, input.SystemOwnerPersonId,
            input.AccessOwnerPersonId, input.ApplicationKind, input.Criticality)
        {
            ClassificationIntent = input.ClassificationIntent,
            SystemOwnerIntent = input.SystemOwnerIntent,
            AccessOwnerIntent = input.AccessOwnerIntent,
        };
        // The original request retains its full authorization and aggregate execution pipeline.
        return bus.DispatchAsync(request, (RequestDispatchContext)context, ct);
    }
}
