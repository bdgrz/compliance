using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

sealed class GetEvidenceRedactionHandler(EvidenceRedactionRead read) : IRequestHandler<GetEvidenceRedaction, EvidenceRedactionView>
{
    public async ValueTask<Result<EvidenceRedactionView>> HandleAsync(IRequestContext<GetEvidenceRedaction> context, CancellationToken ct)
    {
        var snapshot = await read.GetAsync(context, context.Request.TenantId, context.Request.RedactionId, ct).ConfigureAwait(false);
        return snapshot.IsSuccess ? Result<EvidenceRedactionView>.Success(EvidenceRedactionRead.View(snapshot.Value)) :
            Result<EvidenceRedactionView>.Failure(snapshot.Error);
    }
}
