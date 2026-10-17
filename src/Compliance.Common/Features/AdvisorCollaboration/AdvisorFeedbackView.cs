using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Feedback and responses for a handoff or its selected records.</summary>
public sealed record AdvisorFeedbackView(Uuid TenantId, Uuid ProgramId, Uuid AdvisorFeedbackId, Uuid HandoffId, Uuid? RecordVersionId, string Kind, string Body, string? Response, DateTimeOffset CreatedAt);
