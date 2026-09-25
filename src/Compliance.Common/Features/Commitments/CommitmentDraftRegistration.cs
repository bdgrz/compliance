using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

public sealed record CommitmentDraftRegistration(Uuid DraftId, string Kind,
    string Identifier, long Revision);
