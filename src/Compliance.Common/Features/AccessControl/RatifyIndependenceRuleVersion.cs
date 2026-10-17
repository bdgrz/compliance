using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Records the ratify independence rule version intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.access-control.ratify-independence-rule-version", 1)]
public sealed record RatifyIndependenceRuleVersion(long Version, string RatificationReference, string Rationale) : IRequest, IPlatformOperatorRequest, ICallable;
