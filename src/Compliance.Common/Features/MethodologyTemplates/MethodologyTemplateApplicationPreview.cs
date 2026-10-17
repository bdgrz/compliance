using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Proposed additions/changes and conflicts before applying a template.</summary>
public sealed record MethodologyTemplateApplicationPreview(Uuid TenantId, Uuid ProgramId, string Summary, IReadOnlyList<Uuid> AffectedRecordIds, DateTimeOffset CalculatedAt);
