using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Application record, source version, and created-record provenance.</summary>
public sealed record TemplateApplicationView(Uuid TenantId, Uuid ProgramId, Uuid TemplateApplicationId, string Status, long Revision, DateTimeOffset UpdatedAt);
