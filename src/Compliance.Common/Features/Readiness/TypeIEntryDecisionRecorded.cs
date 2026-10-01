using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

[Discriminator("bdgrz.readiness.type_i_entry.decided", 1)]
public sealed record TypeIEntryDecisionRecorded(Uuid TenantId, Uuid ProgramId, long Revision,
    TypeIEntryDecisionView Decision) : DomainEvent;
