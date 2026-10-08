using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

public sealed record EvidenceRedactionApprovalFact(Uuid ApprovalId, long Revision,
    Uuid PreparationId, long PreparedRevision, ulong DerivedSourcePosition,
    ActorReference ApprovedBy, DateTimeOffset ApprovedAt, EvidenceRedactionWaiverSnapshot? SeparationOfDutiesWaiver = null);
