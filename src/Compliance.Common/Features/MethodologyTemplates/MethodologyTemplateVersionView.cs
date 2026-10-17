using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Immutable template version and provenance.</summary>
public sealed record MethodologyTemplateVersionView(Uuid TenantId, Uuid ProgramId, Uuid MethodologyTemplateVersionId, string Status, long Revision, DateTimeOffset UpdatedAt);
