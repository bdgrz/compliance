using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Exports;

/// <summary>Records the record client export build result intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.exports.record-client-export-build-result", 1)]
public sealed record RecordClientExportBuildResult(Uuid TenantId, Uuid ExportId, string Outcome, Uuid? ArtifactId, string? FailureCode, DateTimeOffset CompletedAt) : IRequest, IClientManagementMutationRequest;
