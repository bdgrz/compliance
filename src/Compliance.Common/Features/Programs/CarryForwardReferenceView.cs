using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

/// <summary>Source-to-successor references for carried work and records.</summary>
public sealed record CarryForwardReferenceView(Uuid TenantId, Uuid ProgramId, Uuid CarryForwardReferenceId, Uuid SourceRecordId, Uuid SuccessorRecordId, string Status);
