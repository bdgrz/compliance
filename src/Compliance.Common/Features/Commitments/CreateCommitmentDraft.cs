using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

[Discriminator("bdgrz.commitment.draft.create", 1)]
public sealed record CreateCommitmentDraft(Uuid TenantId, Uuid ProgramId, Uuid ServiceId,
    string Kind, string Identifier, string Statement, string Context, string SourceReference)
    : IRequest<CommitmentDraftRegistration>, IProgramManagementRequest, ICallable;
