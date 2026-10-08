using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class GetApplicationImportOmissionProposalHandler(IAggregateReader reader)
    : IRequestHandler<GetApplicationImportOmissionProposal, ApplicationImportOmissionProposalView>
{
    public async ValueTask<Result<ApplicationImportOmissionProposalView>> HandleAsync(
        IRequestContext<GetApplicationImportOmissionProposal> context, CancellationToken ct)
    {
        var request = context.Request;
        var loaded = await ApplicationImportOmissionSources.LoadAsync(reader, request.TenantId, request.BatchId, ct).ConfigureAwait(false);
        if (!loaded.IsSuccess)
            return Result<ApplicationImportOmissionProposalView>.Failure(loaded.Error);
        var (batch, ledger) = loaded.Value;
        var proposal = ledger.GetRetirementProposal(batch);
        if (proposal is null)
            return Result<ApplicationImportOmissionProposalView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The import has no still-current durable omission proposal.", isTransient: true));
        if (!await ApplicationImportOmissionSources.UnchangedAsync(reader, request.TenantId, batch, ledger, ct).ConfigureAwait(false))
            return Result<ApplicationImportOmissionProposalView>.Failure(ApplicationImportOmissionSources.Changed());
        var start = proposal.Start;
        return Result<ApplicationImportOmissionProposalView>.Success(new(request.TenantId, batch.Id, start.SourceKey,
            start.SourceNamespace, proposal.Revision, start.SourcePosition, start.ContentSha256,
            Array.AsReadOnly(start.PresentSourceRecordIds.ToArray()), Array.AsReadOnly(proposal.Rows.ToArray()),
            start.MemberId, start.MemberDisplay, start.Reason, start.PreparedAt, proposal.ProposalSha256,
            ApplicationImportOmissionSources.Blockers));
    }
}
