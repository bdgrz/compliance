using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed record ApplicationImportOmissionPreview(Uuid TenantId, Uuid BatchId, string SourceKey,
    string SourceNamespace, string Coverage, long Revision, ulong SourcePosition, string ContentSha256,
    IReadOnlyList<ApplicationImportRetirementRow> MissingRows, IReadOnlyList<string> AcceptanceBlockers);
