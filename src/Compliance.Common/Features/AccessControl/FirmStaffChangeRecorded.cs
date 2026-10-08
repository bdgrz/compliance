using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.firm-staff.changed", 1)]
public sealed record FirmStaffChangeRecorded(Uuid RequestId, long ExpectedSequence, string Operation,
    string? Reason, FirmStaffMemberView Staff) : DomainEvent;
