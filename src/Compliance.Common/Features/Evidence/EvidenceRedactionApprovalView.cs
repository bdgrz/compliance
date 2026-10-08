using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

public sealed record EvidenceRedactionApprovalView(Uuid ApprovalId, long Revision,
    Uuid PreparationId, long PreparedRevision, ActorReference ApprovedBy,
    DateTimeOffset ApprovedAt, bool SeparationOfDutiesWaived, Uuid? SeparationOfDutiesWaiverId);
