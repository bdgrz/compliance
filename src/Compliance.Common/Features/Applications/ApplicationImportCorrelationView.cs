using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed record ApplicationImportCorrelationView(string Decision, Uuid ApplicationId,
    long? ExpectedApplicationRevision, string Reason, Uuid ActorMemberId,
    string ActorDisplay, DateTimeOffset RecordedAt, long Revision);
