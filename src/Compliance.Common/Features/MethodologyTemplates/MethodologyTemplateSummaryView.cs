using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Published templates visible to authorized tenant administrators.</summary>
public sealed record MethodologyTemplateSummaryView(Uuid TenantId, Uuid ProgramId, Uuid MethodologyTemplateId, string Name, string Status, DateTimeOffset UpdatedAt);
