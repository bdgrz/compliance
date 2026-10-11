using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>A workforce-person owner and the actor snapshot that recorded the designation.</summary>
public sealed record ControlPersonOwnerDesignationView(Uuid PersonId, Uuid DesignationId,
    string Rationale, ActorReference Actor, DateTimeOffset DesignatedAt);
