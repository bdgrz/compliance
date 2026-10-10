using System.Security.Cryptography;
using System.Text.Json;

namespace Bdgrz.Compliance.Features.Applications;

static class ApplicationChangeImpactDigest
{
    public static string Compute(ApplicationChangePreview preview)
    {
        var canonical = preview with
        {
            Changes = preview.Changes.OrderBy(static item => item.Field,
                StringComparer.Ordinal).ToArray(),
            BoundaryReferences = preview.BoundaryReferences
                .OrderBy(static item => item.SubjectType, StringComparer.Ordinal)
                .ThenBy(static item => item.GovernedRecordId.ToString(), StringComparer.Ordinal)
                .ThenBy(static item => item.BoundaryId.ToString(), StringComparer.Ordinal)
                .ThenBy(static item => item.VersionId.ToString(), StringComparer.Ordinal)
                .ThenBy(static item => item.EntryId.ToString(), StringComparer.Ordinal).ToArray(),
            PendingContexts = preview.PendingContexts.Order(StringComparer.Ordinal).ToArray(),
            ControlDraftReferences = preview.ControlDraftReferences
                .OrderBy(static item => item.ControlId.ToString(), StringComparer.Ordinal)
                .ThenBy(static item => item.EntryId.ToString(), StringComparer.Ordinal).ToArray(),
            SystemInstanceReferences = preview.SystemInstanceReferences
                .OrderBy(static item => item.SystemInstanceId.ToString(), StringComparer.Ordinal)
                .ToArray(),
            ImpactDigest = string.Empty,
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(canonical,
            ComplianceCoreJsonContext.Default.ApplicationChangePreview);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
