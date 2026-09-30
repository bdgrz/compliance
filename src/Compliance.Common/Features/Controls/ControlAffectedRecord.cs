using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

public sealed record ControlAffectedRecord(Uuid TenantId, string Context, string RecordType,
    Uuid RecordId, Uuid? VersionId, string Reason);
