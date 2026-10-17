using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

/// <summary>Records the record engagement report issued intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.retention.record-engagement-report-issued", 1)]
public sealed record RecordEngagementReportIssued(Uuid TenantId, Uuid EngagementId, Uuid ReportArtifactId, DateTimeOffset IssuedAt, string IssuerReference) : IRequest, IClientManagementMutationRequest, ICallable;
