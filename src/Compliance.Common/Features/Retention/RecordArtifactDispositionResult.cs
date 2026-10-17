using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

/// <summary>Records the record artifact disposition result intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.retention.record-artifact-disposition-result", 1)]
public sealed record RecordArtifactDispositionResult(Uuid TenantId, Uuid DispositionId, Uuid ArtifactId, string Outcome, string? FailureCode, DateTimeOffset CompletedAt) : IRequest, IClientManagementMutationRequest;
